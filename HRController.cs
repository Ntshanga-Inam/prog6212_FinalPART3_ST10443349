using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using CMCS.Models;
using CMCS.Services;
using CMCS.Hubs;
using System.Text;

namespace CMCS.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly IDataService _dataService;
        private readonly ILogger<HRController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IHubContext<ClaimHub> _hubContext;

        public HRController(
            IDataService dataService,
            ILogger<HRController> logger,
            IWebHostEnvironment environment,
            IHubContext<ClaimHub> hubContext)
        {
            _dataService = dataService;
            _logger = logger;
            _environment = environment;
            _hubContext = hubContext;
        }

        public IActionResult Dashboard()
        {
            try
            {
                var stats = GetHRStatistics();
                ViewBag.Stats = stats;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HR dashboard");
                TempData["Error"] = "Unable to load dashboard.";
                return View();
            }
        }

        public IActionResult ApprovedClaims()
        {
            try
            {
                var approvedClaims = _dataService.GetClaims()
                    .Where(c => c.Status == "Approved")
                    .OrderByDescending(c => c.ApprovedDate)
                    .ToList();

                return View(approvedClaims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading approved claims");
                TempData["Error"] = "Unable to load approved claims.";
                return View(new List<Claim>());
            }
        }

        public IActionResult LecturerManagement()
        {
            try
            {
                var lecturers = _dataService.GetUsers()
                    .Where(u => u.Role == "Lecturer")
                    .ToList();

                return View(lecturers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturers");
                TempData["Error"] = "Unable to load lecturer data.";
                return View(new List<User>());
            }
        }

        public IActionResult Reports()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GeneratePaymentReport([FromBody] ReportRequest request)
        {
            try
            {
                var claims = _dataService.GetClaims()
                    .Where(c => c.Status == "Approved" &&
                               c.ApprovedDate >= request.StartDate &&
                               c.ApprovedDate <= request.EndDate)
                    .ToList();

                var reportData = new PaymentReport
                {
                    GeneratedDate = DateTime.Now,
                    PeriodStart = request.StartDate,
                    PeriodEnd = request.EndDate,
                    TotalClaims = claims.Count,
                    TotalAmount = claims.Sum(c => c.Amount),
                    Claims = claims
                };

                return Json(new { success = true, data = reportData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating payment report");
                return Json(new { success = false, error = "Unable to generate report" });
            }
        }

        [HttpPost]
        public IActionResult GenerateMonthlySummary([FromBody] MonthlyReportRequest request)
        {
            try
            {
                var claims = _dataService.GetClaims()
                    .Where(c => c.ClaimMonth.Year == request.Year &&
                               c.ClaimMonth.Month == request.Month)
                    .ToList();

                var summary = new MonthlySummaryReport
                {
                    Year = request.Year,
                    Month = request.Month,
                    TotalClaims = claims.Count,
                    ApprovedClaims = claims.Count(c => c.Status == "Approved"),
                    PendingClaims = claims.Count(c => c.Status != "Approved" && c.Status != "Rejected"),
                    TotalAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.Amount),
                    ClaimsByStatus = claims.GroupBy(c => c.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    TopLecturers = claims.Where(c => c.Status == "Approved")
                        .GroupBy(c => c.LecturerId)
                        .Select(g => new LecturerSummary
                        {
                            LecturerId = g.Key.ToString(), // Convert int to string
                            TotalAmount = g.Sum(c => c.Amount),
                            TotalHours = g.Sum(c => c.TotalHours),
                            ClaimCount = g.Count()
                        })
                        .OrderByDescending(l => l.TotalAmount)
                        .Take(10)
                        .ToList()
                };

                return Json(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly summary");
                return Json(new { success = false, error = "Unable to generate monthly summary" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportToExcel([FromBody] ExportRequest request)
        {
            try
            {
                var claims = _dataService.GetClaims()
                    .Where(c => c.ApprovedDate >= request.StartDate &&
                               c.ApprovedDate <= request.EndDate &&
                               c.Status == "Approved")
                    .ToList();

                var csvContent = await GenerateCsvAsync(claims);
                var fileName = $"PaymentReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var fileBytes = Encoding.UTF8.GetBytes(csvContent);

                return File(fileBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to Excel");
                return Json(new { success = false, error = "Unable to export data" });
            }
        }

        [HttpPost]
        public IActionResult UpdateLecturer([FromBody] LecturerUpdateRequest request)
        {
            try
            {
                // In a real application, this would update the lecturer in the database
                // For now, we'll simulate the update
                _logger.LogInformation("Updating lecturer {LecturerId} with rate {HourlyRate}",
                    request.LecturerId, request.HourlyRate);

                return Json(new { success = true, message = "Lecturer updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating lecturer {LecturerId}", request.LecturerId);
                return Json(new { success = false, error = "Unable to update lecturer" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayments([FromBody] PaymentProcessRequest request)
        {
            try
            {
                var claims = _dataService.GetClaims()
                    .Where(c => request.ClaimIds.Contains(c.ClaimId) &&
                               c.Status == "Approved")
                    .ToList();

                foreach (var claim in claims)
                {
                    // Simulate payment processing
                    claim.Status = "Paid";
                    _dataService.SaveClaim(claim);

                    // REAL-TIME NOTIFICATION: Notify lecturer about payment
                    await _hubContext.Clients.Group($"Lecturer_{claim.LecturerId}").SendAsync("ReceiveStatusUpdate", claim.ClaimId, "Paid");

                    _logger.LogInformation("Processed payment for claim {ClaimId}, amount: {Amount}",
                        claim.ClaimId, claim.Amount);
                }

                return Json(new
                {
                    success = true,
                    message = $"Successfully processed {claims.Count} payments",
                    totalAmount = claims.Sum(c => c.Amount)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payments");
                return Json(new { success = false, error = "Unable to process payments" });
            }
        }

        private HRStatistics GetHRStatistics()
        {
            var claims = _dataService.GetClaims();
            var approvedClaims = claims.Where(c => c.Status == "Approved").ToList();

            return new HRStatistics
            {
                TotalApprovedClaims = approvedClaims.Count,
                TotalPaymentAmount = approvedClaims.Sum(c => c.Amount),
                PendingProcessing = claims.Count(c => c.Status == "Approved"),
                LecturersCount = _dataService.GetUsers().Count(u => u.Role == "Lecturer"),
                ThisMonthAmount = approvedClaims
                    .Where(c => c.ApprovedDate.HasValue &&
                               c.ApprovedDate.Value.Month == DateTime.Now.Month &&
                               c.ApprovedDate.Value.Year == DateTime.Now.Year)
                    .Sum(c => c.Amount),
                AverageProcessingTime = CalculateAverageProcessingTime(approvedClaims)
            };
        }

        private double CalculateAverageProcessingTime(List<Claim> approvedClaims)
        {
            if (!approvedClaims.Any()) return 0;

            var validClaims = approvedClaims
                .Where(c => c.ApprovedDate.HasValue)
                .ToList();

            if (!validClaims.Any()) return 0;

            var totalDays = validClaims
                .Average(c => (c.ApprovedDate!.Value - c.SubmittedDate).TotalDays);

            return Math.Round(totalDays, 1);
        }

        private async Task<string> GenerateCsvAsync(List<Claim> claims)
        {
            return await Task.Run(() =>
            {
                var csv = new StringBuilder();
                csv.AppendLine("ClaimID,LecturerID,ClaimMonth,TotalHours,HourlyRate,Amount,Status,SubmittedDate,ApprovedDate");

                foreach (var claim in claims)
                {
                    var approvedDate = claim.ApprovedDate?.ToString("yyyy-MM-dd") ?? "N/A";
                    csv.AppendLine($"{claim.ClaimId},{claim.LecturerId},{claim.ClaimMonth:yyyy-MM-dd},{claim.TotalHours},{claim.HourlyRate},{claim.Amount},{claim.Status},{claim.SubmittedDate:yyyy-MM-dd},{approvedDate}");
                }

                return csv.ToString();
            });
        }
    }

    public class ReportRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ReportType { get; set; } = string.Empty;
    }

    public class MonthlyReportRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class ExportRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Format { get; set; } = string.Empty;
    }

    public class LecturerUpdateRequest
    {
        public string LecturerId { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; }
        public string Department { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class PaymentProcessRequest
    {
        public List<int> ClaimIds { get; set; } = new List<int>();
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class HRStatistics
    {
        public int TotalApprovedClaims { get; set; }
        public decimal TotalPaymentAmount { get; set; }
        public int PendingProcessing { get; set; }
        public int LecturersCount { get; set; }
        public decimal ThisMonthAmount { get; set; }
        public double AverageProcessingTime { get; set; }
    }

    public class PaymentReport
    {
        public DateTime GeneratedDate { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalClaims { get; set; }
        public decimal TotalAmount { get; set; }
        public List<Claim> Claims { get; set; } = new List<Claim>();
    }

    public class MonthlySummaryReport
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int PendingClaims { get; set; }
        public decimal TotalAmount { get; set; }
        public Dictionary<string, int> ClaimsByStatus { get; set; } = new Dictionary<string, int>();
        public List<LecturerSummary> TopLecturers { get; set; } = new List<LecturerSummary>();
    }

    public class LecturerSummary
    {
        public string LecturerId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal TotalHours { get; set; }
        public int ClaimCount { get; set; }
    }
}