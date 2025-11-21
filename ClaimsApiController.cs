using Microsoft.AspNetCore.Mvc;
using CMCS.Models;
using CMCS.Services;
using CMCS.Validators;
using FluentValidation;
using System.Text.Json;

namespace CMCS.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClaimsApiController : ControllerBase
    {
        private readonly IDataService _dataService;
        private readonly IValidator<Claim> _claimValidator;
        private readonly ILogger<ClaimsApiController> _logger;

        public ClaimsApiController(
            IDataService dataService,
            IValidator<Claim> claimValidator,
            ILogger<ClaimsApiController> logger)
        {
            _dataService = dataService;
            _claimValidator = claimValidator;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetClaims([FromQuery] string? status = null, [FromQuery] string? role = null)
        {
            try
            {
                var claims = _dataService.GetClaims();

                // Filter if needed
                if (!string.IsNullOrEmpty(status))
                {
                    claims = claims.Where(c => c.Status == status).ToList();
                }

                // Convert to DTOs to avoid circular references
                var claimDtos = claims.Select(c => new ClaimDto
                {
                    ClaimId = c.ClaimId,
                    LecturerId = c.LecturerId,
                    ClaimMonth = c.ClaimMonth,
                    TotalHours = c.TotalHours,
                    HourlyRate = c.HourlyRate,
                    Amount = c.Amount,
                    Notes = c.Notes,
                    Status = c.Status,
                    SubmittedDate = c.SubmittedDate,
                    ApprovedDate = c.ApprovedDate,
                    ApprovedBy = c.ApprovedBy,
                    LecturerName = c.Lecturer?.FirstName + " " + c.Lecturer?.LastName,
                    ClaimItems = c.ClaimItems.Select(ci => new ClaimItemDto
                    {
                        ClaimItemId = ci.ClaimItemId,
                        ClaimId = ci.ClaimId,
                        Date = ci.Date,
                        HoursWorked = ci.HoursWorked,
                        Module = ci.Module,
                        Description = ci.Description
                    }).ToList()
                }).ToList();

                return Ok(claimDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving claims via API");
                return StatusCode(500, new { error = "Unable to retrieve claims" });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetClaim(int id)
        {
            try
            {
                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    return NotFound(new { error = "Claim not found" });
                }

                return Ok(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving claim {ClaimId} via API", id);
                return StatusCode(500, new { error = "Unable to retrieve claim" });
            }
        }

        [HttpPost("{id}/validate")]
        public IActionResult ValidateClaim(int id)
        {
            try
            {
                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    return NotFound(new { error = "Claim not found" });
                }

                var validationResult = _claimValidator.Validate(claim);
                var validationResponse = new
                {
                    isValid = validationResult.IsValid,
                    errors = validationResult.Errors.Select(e => new
                    {
                        property = e.PropertyName,
                        message = e.ErrorMessage,
                        severity = e.Severity.ToString()
                    })
                };

                return Ok(validationResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating claim {ClaimId} via API", id);
                return StatusCode(500, new { error = "Unable to validate claim" });
            }
        }

        [HttpPost("{id}/approve")]
        public IActionResult ApproveClaim(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    return NotFound(new { error = "Claim not found" });
                }

                // Validate the claim first
                var validationResult = _claimValidator.Validate(claim);
                if (!validationResult.IsValid && request.StrictValidation)
                {
                    return BadRequest(new
                    {
                        error = "Claim validation failed",
                        validationErrors = validationResult.Errors.Select(e => new
                        {
                            property = e.PropertyName,
                            message = e.ErrorMessage,
                            severity = e.Severity.ToString()
                        })
                    });
                }

                // Update claim status based on workflow
                var userRole = request.ApproverRole ?? "Unknown"; // Provide default value
                var (newStatus, nextStep) = GetNextStatus(claim.Status, userRole, true);

                claim.Status = newStatus;
                claim.ApprovedBy = request.ApproverId; // Use ApprovedBy instead of ApprovedBy2
                if (newStatus == "Approved")
                {
                    claim.ApprovedDate = DateTime.Now;
                }

                // Save approval record
                var approval = new Approval
                {
                    ClaimId = id,
                    ApproverId = request.ApproverId,
                    ApprovedByRole = userRole,
                    ApprovalDate = DateTime.Now,
                    Notes = request.Notes ?? string.Empty, // Handle null case
                    Status = "Approved"
                };

                _dataService.SaveClaim(claim);
                _dataService.SaveApproval(approval);

                _logger.LogInformation("Claim {ClaimId} approved by {ApproverRole}", id, userRole);

                return Ok(new
                {
                    success = true,
                    message = $"Claim approved and moved to {nextStep}",
                    newStatus = claim.Status,
                    nextStep = nextStep
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving claim {ClaimId} via API", id);
                return StatusCode(500, new { error = "Unable to approve claim" });
            }
        }

        [HttpPost("{id}/reject")]
        public IActionResult RejectClaim(int id, [FromBody] RejectionRequest request)
        {
            try
            {
                var claim = _dataService.GetClaim(id);
                if (claim == null)
                {
                    return NotFound(new { error = "Claim not found" });
                }

                claim.Status = "Rejected";
                claim.ApprovedBy = request.ApproverId; // Use ApprovedBy instead of ApprovedBy2
                claim.Notes = $"[REJECTED] {request.Reason}";

                // Save rejection record
                var approval = new Approval
                {
                    ClaimId = id,
                    ApproverId = request.ApproverId,
                    ApprovedByRole = request.ApproverRole ?? "Unknown", // Provide default value
                    ApprovalDate = DateTime.Now,
                    Notes = request.Reason ?? string.Empty, // Handle null case
                    Status = "Rejected"
                };

                _dataService.SaveClaim(claim);
                _dataService.SaveApproval(approval);

                _logger.LogInformation("Claim {ClaimId} rejected by {ApproverRole}", id, request.ApproverRole ?? "Unknown");

                return Ok(new
                {
                    success = true,
                    message = "Claim rejected successfully",
                    newStatus = claim.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting claim {ClaimId} via API", id);
                return StatusCode(500, new { error = "Unable to reject claim" });
            }
        }

        [HttpGet("workflow/{status}")]
        public IActionResult GetWorkflowActions(string status)
        {
            var actions = GetAvailableActions(status);
            return Ok(actions);
        }

        private (string newStatus, string nextStep) GetNextStatus(string currentStatus, string userRole, bool isApproved)
        {
            return (currentStatus, userRole, isApproved) switch
            {
                ("Submitted", "Coordinator", true) => ("With Manager", "Manager Review"),
                ("With Coordinator", "Coordinator", true) => ("With Manager", "Manager Review"),
                ("With Manager", "Manager", true) => ("Approved", "HR Processing"),
                (_, _, false) => ("Rejected", "Closed"),
                _ => (currentStatus, "No Change")
            };
        }

        private List<string> GetAvailableActions(string currentStatus)
        {
            return currentStatus switch
            {
                "Submitted" or "With Coordinator" => new List<string> { "Approve", "Reject", "Request Changes" },
                "With Manager" => new List<string> { "Approve", "Reject", "Escalate" },
                "Approved" => new List<string> { "Process Payment" },
                "Rejected" => new List<string> { "Reopen", "Archive" },
                _ => new List<string>()
            };
        }
    }

    public class ApprovalRequest
    {
        public int ApproverId { get; set; }
        public string ApproverRole { get; set; } = string.Empty; // Initialize with default value
        public string Notes { get; set; } = string.Empty; // Initialize with default value
        public bool StrictValidation { get; set; } = true;
    }

    public class RejectionRequest
    {
        public int ApproverId { get; set; }
        public string ApproverRole { get; set; } = string.Empty; // Initialize with default value
        public string Reason { get; set; } = string.Empty; // Initialize with default value
    }
}