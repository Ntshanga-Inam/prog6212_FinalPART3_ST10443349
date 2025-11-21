using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using CMCS.Models;
using CMCS.Services;
using CMCS.Validators;
using CMCS.Hubs;
using FluentValidation;
using System.Diagnostics;

namespace CMCS.Controllers
{
    [Authorize]
    public class ApprovalController : Controller
    {
        private readonly IDataService _dataService;
        private readonly ILogger<ApprovalController> _logger;
        private readonly IValidator<Claim> _claimValidator;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ClaimHub> _hubContext;

        public ApprovalController(
            IDataService dataService,
            ILogger<ApprovalController> logger,
            IValidator<Claim> claimValidator,
            UserManager<ApplicationUser> userManager,
            IHubContext<ClaimHub> hubContext)
        {
            _dataService = dataService;
            _logger = logger;
            _claimValidator = claimValidator;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // In ApprovalController.cs - Update the Index method:
        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userRole = currentUser.Role;
                var userId = currentUser.Id;

                var claims = _dataService.GetClaims();
                _logger.LogInformation("Total claims in database: {ClaimCount}", claims.Count);
                _logger.LogInformation("Current user role: {UserRole}", userRole);

                var pendingClaims = userRole switch
                {
                    "Coordinator" => claims.Where(c => c.Status == "Submitted" || c.Status == "With Coordinator").ToList(),
                    "Manager" => claims.Where(c => c.Status == "With Manager").ToList(),
                    "HR" => claims.Where(c => c.Status == "Approved").ToList(),
                    _ => new List<Claim>()
                };

                _logger.LogInformation("Found {PendingCount} pending claims for {UserRole}", pendingClaims.Count, userRole);

                ViewBag.UserRole = userRole;
                return View(pendingClaims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading approval queue");
                TempData["Error"] = "Unable to load approval queue.";
                return View(new List<Claim>());
            }
        }

        public IActionResult Review(int id)
        {
            try
            {
                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Automated validation check
                var validationResult = _claimValidator.Validate(claim);
                ViewBag.ValidationResult = validationResult;
                ViewBag.UserRole = User.FindFirst("Role")?.Value ?? "Coordinator";

                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading claim for review");
                TempData["Error"] = "Unable to load claim for review.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string notes, bool bypassValidation = false)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(Index));
                }

                var userRole = currentUser.Role;

                // Get approver ID from legacy user
                var legacyUser = _dataService.GetUserByEmail(currentUser.Email);
                int? approverId = legacyUser?.UserId;

                // Update claim status based on workflow
                string newStatus = claim.Status;
                string successMessage = "";

                if (userRole == "Coordinator" && (claim.Status == "Submitted" || claim.Status == "With Coordinator"))
                {
                    newStatus = "With Manager";
                    successMessage = $"Claim #{id} approved and sent to Manager for final approval.";

                    // REAL-TIME NOTIFICATION: Notify managers
                    await _hubContext.Clients.Group("Managers").SendAsync("ReceiveCoordinatorApproval", id);

                    // REAL-TIME NOTIFICATION: Notify lecturer
                    await _hubContext.Clients.Group($"Lecturer_{claim.LecturerId}").SendAsync("ReceiveStatusUpdate", id, newStatus);
                }
                else if (userRole == "Manager" && claim.Status == "With Manager")
                {
                    newStatus = "Approved";
                    claim.ApprovedDate = DateTime.Now;
                    successMessage = $"Claim #{id} fully approved and sent to HR for processing.";

                    // REAL-TIME NOTIFICATION: Notify HR
                    await _hubContext.Clients.Group("HR").SendAsync("ReceiveManagerApproval", id);

                    // REAL-TIME NOTIFICATION: Notify lecturer
                    await _hubContext.Clients.Group($"Lecturer_{claim.LecturerId}").SendAsync("ReceiveStatusUpdate", id, newStatus);
                }
                else
                {
                    TempData["Error"] = "Invalid approval action for current claim status.";
                    return RedirectToAction(nameof(Review), new { id });
                }

                // Update claim
                claim.Status = newStatus;
                claim.ApprovedBy = approverId;

                _dataService.SaveClaim(claim);

                // Create approval record
                if (approverId.HasValue)
                {
                    var approval = new Approval
                    {
                        ClaimId = id,
                        ApproverId = approverId.Value,
                        ApprovedByRole = userRole,
                        ApprovalDate = DateTime.Now,
                        Notes = notes,
                        Status = "Approved"
                    };
                    _dataService.SaveApproval(approval);
                }

                // REAL-TIME NOTIFICATION: Broadcast status update to all
                await _hubContext.Clients.All.SendAsync("ReceiveClaimStatusUpdate", id, newStatus, userRole);

                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim");
                TempData["Error"] = "Error approving claim. Please try again.";
                return RedirectToAction(nameof(Review), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string notes)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(Index));
                }

                var userId = currentUser.Id;
                var userName = currentUser.UserName ?? "System";

                // Parse userId to int if available
                int? approverId = null;
                if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out int parsedUserId))
                {
                    approverId = parsedUserId;
                }

                claim.Status = "Rejected";
                claim.ApprovedBy = approverId;
                claim.Notes = $"[REJECTED] {notes}";

                _dataService.SaveClaim(claim);

                // Create rejection record
                if (approverId.HasValue)
                {
                    var approval = new Approval
                    {
                        ClaimId = id,
                        ApproverId = approverId.Value,
                        ApprovedByRole = currentUser.Role,
                        ApprovalDate = DateTime.Now,
                        Notes = $"[REJECTED] {notes}",
                        Status = "Rejected"
                    };
                    _dataService.SaveApproval(approval);
                }

                // REAL-TIME NOTIFICATION: Notify lecturer about rejection
                await _hubContext.Clients.Group($"Lecturer_{claim.LecturerId}").SendAsync("ReceiveStatusUpdate", id, "Rejected");

                // REAL-TIME NOTIFICATION: Broadcast status update
                await _hubContext.Clients.All.SendAsync("ReceiveClaimStatusUpdate", id, "Rejected", currentUser.Role);

                _logger.LogInformation("Claim {ClaimId} rejected by {UserName}", id, userName);

                TempData["Success"] = $"Claim #{id} has been rejected.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim");
                TempData["Error"] = "Error rejecting claim. Please try again.";
                return RedirectToAction(nameof(Review), new { id });
            }
        }

        [HttpPost]
        public IActionResult ValidateClaim(int id)
        {
            try
            {
                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    return Json(new { success = false, error = "Claim not found" });
                }

                var validationResult = _claimValidator.Validate(claim);
                var errors = validationResult.Errors.Select(e => new
                {
                    property = e.PropertyName,
                    message = e.ErrorMessage,
                    severity = e.Severity.ToString()
                }).ToList();

                return Json(new
                {
                    success = true,
                    isValid = validationResult.IsValid,
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating claim {ClaimId}", id);
                return Json(new { success = false, error = "Validation error occurred" });
            }
        }

        [HttpGet]
        public IActionResult GetApprovalStats()
        {
            try
            {
                var claims = _dataService.GetClaims();
                var stats = new
                {
                    TotalClaims = claims.Count,
                    PendingApproval = claims.Count(c => c.Status == "Submitted" || c.Status == "With Coordinator" || c.Status == "With Manager"),
                    Approved = claims.Count(c => c.Status == "Approved"),
                    Rejected = claims.Count(c => c.Status == "Rejected"),
                    TotalAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.Amount)
                };

                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving approval statistics");
                return Json(new { error = "Unable to retrieve statistics" });
            }
        }
    }
}