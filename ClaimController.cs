using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using CMCS.Models;
using CMCS.Services;
using CMCS.Hubs;
using System.Diagnostics;

namespace CMCS.Controllers
{
    [Authorize]
    public class ClaimController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ClaimController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ClaimHub> _hubContext;

        public ClaimController(
            IDataService dataService,
            IWebHostEnvironment environment,
            ILogger<ClaimController> logger,
            UserManager<ApplicationUser> userManager,
            IHubContext<ClaimHub> hubContext)
        {
            _dataService = dataService;
            _environment = environment;
            _logger = logger;
            _userManager = userManager;
            _hubContext = hubContext;
        }


        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get the legacy user ID from the database using email
                var legacyUser = _dataService.GetUserByEmail(currentUser.Email);
                if (legacyUser == null || legacyUser.UserId == 0)
                {
                    _logger.LogWarning("No legacy user found for email: {Email}", currentUser.Email);
                    TempData["Error"] = "User profile not found. Please contact administrator.";
                    return View(new List<Claim>());
                }

                var userId = legacyUser.UserId;
                _logger.LogInformation("Retrieving claims for legacy user ID: {UserId}", userId);

                // Get all claims for this lecturer
                var allClaims = _dataService.GetClaims();
                _logger.LogInformation("Total claims in database: {ClaimCount}", allClaims.Count);

                var userClaims = allClaims
                    .Where(c => c.LecturerId == userId)
                    .OrderByDescending(c => c.SubmittedDate)
                    .ToList();

                _logger.LogInformation("Found {ClaimCount} claims for user {UserId}", userClaims.Count, userId);

                return View(userClaims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving claims for user");
                TempData["Error"] = "Unable to load claims. Please try again.";
                return View(new List<Claim>());
            }
        }


        // GET: /Claim/Create
        public IActionResult Create()
        {
            try
            {
                var model = new ClaimSubmissionViewModel();
                // Pre-populate with some example claim items
                model.ClaimItems.Add(new ClaimItemViewModel
                {
                    Date = DateTime.Now,
                    HoursWorked = 0.5m,
                    Module = "",
                    Description = ""
                });
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading claim creation form");
                TempData["Error"] = "Unable to load claim form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Claim/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClaimSubmissionViewModel model)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                if (!ModelState.IsValid)
                {
                    if (!model.ClaimItems.Any())
                    {
                        model.ClaimItems.Add(new ClaimItemViewModel
                        {
                            Date = DateTime.Now,
                            HoursWorked = 0.5m,
                            Module = "",
                            Description = ""
                        });
                    }
                    return View(model);
                }

                // Get legacy user ID
                var legacyUser = _dataService.GetUserByEmail(currentUser.Email);
                if (legacyUser == null || legacyUser.UserId == 0)
                {
                    TempData["Error"] = "User profile not found.";
                    return View(model);
                }

                // Generate claim ID and create claim
                var claimId = await GenerateClaimIdAsync();

                var claim = new Claim
                {
                    ClaimId = claimId,
                    LecturerId = legacyUser.UserId,
                    ClaimMonth = model.ClaimMonth,
                    TotalHours = model.TotalHours,
                    HourlyRate = model.HourlyRate,
                    Notes = model.Notes ?? string.Empty,
                    Status = "Submitted", // This should make it visible to Coordinator
                    SubmittedDate = DateTime.Now
                };
                claim.CalculateAmount();

                _logger.LogInformation("Creating new claim: ID={ClaimId}, Lecturer={LecturerId}, Status={Status}",
                    claim.ClaimId, claim.LecturerId, claim.Status);

                // Save claim items if provided
                if (model.ClaimItems != null && model.ClaimItems.Any(item => item.HoursWorked > 0))
                {
                    claim.ClaimItems = model.ClaimItems
                        .Where(item => item.HoursWorked > 0)
                        .Select((item, index) => new ClaimItem
                        {
                            ClaimItemId = index + 1,
                            ClaimId = claim.ClaimId,
                            Date = item.Date,
                            HoursWorked = item.HoursWorked,
                            Module = item.Module ?? string.Empty,
                            Description = item.Description ?? string.Empty
                        })
                        .ToList();
                }

                // SAVE THE CLAIM
                _dataService.SaveClaim(claim);
                _logger.LogInformation("Claim saved successfully");

                // REAL-TIME NOTIFICATION: Notify coordinators about new claim
                await _hubContext.Clients.Group("Coordinators").SendAsync("ReceiveNewClaim", claimId, legacyUser.UserId);

                // REAL-TIME NOTIFICATION: Notify lecturer about successful submission
                await _hubContext.Clients.Group($"Lecturer_{legacyUser.UserId}").SendAsync("ReceiveClaimSubmissionSuccess", claimId);

                // Verify the claim was saved and is visible to coordinators
                var savedClaim = _dataService.GetClaim(claimId);
                if (savedClaim != null)
                {
                    _logger.LogInformation("Claim verification: ID={ClaimId}, Status={Status}, Lecturer={LecturerId}",
                        savedClaim.ClaimId, savedClaim.Status, savedClaim.LecturerId);
                }

                TempData["Success"] = $"Claim #{claim.ClaimId} submitted successfully! It will now be reviewed by the Coordinator.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                TempData["Error"] = "Error submitting claim. Please try again.";
                return View(model);
            }
        }
        public IActionResult Details(int id)
        {
            try
            {
                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction(nameof(Index));
                }
                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving claim details");
                TempData["Error"] = "Unable to load claim details.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadDocument(int claimId, IFormFile file, string description)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "Please select a file to upload." });
                }

                // Validate file size (5MB limit)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "File size must be less than 5MB." });
                }

                // Validate file type
                var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".jpg", ".png" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new { success = false, message = "Only PDF, DOCX, XLSX, JPG, and PNG files are allowed." });
                }

                var document = await SaveUploadedFileAsync(file, claimId, description);
                if (document != null)
                {
                    _dataService.SaveDocument(document);
                    return Json(new { success = true, message = "File uploaded successfully!", fileName = document.FileName });
                }

                return Json(new { success = false, message = "Error uploading file." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return Json(new { success = false, message = "Error uploading file. Please try again." });
            }
        }

        private async Task<Document?> SaveUploadedFileAsync(IFormFile file, int claimId, string? description = null)
        {
            try
            {
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "documents");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return new Document
                {
                    DocumentId = await GenerateDocumentIdAsync(),
                    ClaimId = claimId,
                    FileName = file.FileName,
                    FilePath = $"/uploads/documents/{fileName}",
                    FileSize = file.Length,
                    ContentType = file.ContentType ?? "application/octet-stream",
                    UploadedDate = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving uploaded file for claim {ClaimId}", claimId);
                return null;
            }
        }

        private async Task<int> GenerateClaimIdAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var claims = _dataService.GetClaims();
                    return claims.Any() ? claims.Max(c => c.ClaimId) + 1 : 1001; // Start from 1001 for better readability
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating claim ID");
                    // Fallback: use timestamp-based ID
                    return (int)(DateTime.Now.Ticks % 1000000) + 1000;
                }
            });
        }

        private async Task<int> GenerateDocumentIdAsync()
        {
            return await Task.Run(() =>
            {
                var claims = _dataService.GetClaims();
                var allDocuments = claims.SelectMany(c => c.Documents ?? new List<Document>());
                return allDocuments.Any() ? allDocuments.Max(d => d.DocumentId) + 1 : 1;
            });
        }
    }
}