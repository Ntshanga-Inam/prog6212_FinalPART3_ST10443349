using CMCS.Models;
using CMCS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CMCS.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDataService _dataService;

        public HomeController(UserManager<ApplicationUser> userManager, IDataService dataService)
        {
            _userManager = userManager;
            _dataService = dataService;
        }

        public IActionResult Index()
        {
            // Simulate user role for demonstration
            ViewBag.UserRole = "Lecturer"; // Change to "Coordinator" or "Manager" to see different views
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                ViewBag.UserName = $"{currentUser.FirstName} {currentUser.LastName}";
                ViewBag.UserRole = currentUser.Role;

                // Get REAL claims data based on user role
                var allClaims = _dataService.GetClaims();
                List<Claim> userClaims = new List<Claim>();

                if (currentUser.Role == "Lecturer")
                {
                    var legacyUser = _dataService.GetUserByEmail(currentUser.Email ?? string.Empty);
                    if (legacyUser != null && legacyUser.UserId != 0)
                    {
                        userClaims = allClaims.Where(c => c.LecturerId == legacyUser.UserId).ToList();
                    }
                }
                else if (currentUser.Role == "Coordinator")
                {
                    userClaims = allClaims.Where(c => c.Status == "Submitted" || c.Status == "With Coordinator").ToList();
                }
                else if (currentUser.Role == "Manager")
                {
                    userClaims = allClaims.Where(c => c.Status == "With Manager").ToList();
                }
                else if (currentUser.Role == "HR")
                {
                    userClaims = allClaims.Where(c => c.Status == "Approved").ToList();
                }

                // Pass real data to view
                ViewBag.TotalClaims = userClaims.Count;
                ViewBag.ApprovedClaims = userClaims.Count(c => c.Status == "Approved");
                ViewBag.PendingClaims = userClaims.Count(c => c.Status == "Submitted" || c.Status == "With Coordinator" || c.Status == "With Manager");
                ViewBag.TotalAmount = userClaims.Where(c => c.Status == "Approved").Sum(c => c.Amount);
                ViewBag.RecentClaims = userClaims.OrderByDescending(c => c.SubmittedDate).Take(5).ToList();

                return View();
            }
            catch (Exception ex)
            {
                // Log the exception (you can add logging here)
                // For now, set default values if error occurs
                ViewBag.TotalClaims = 0;
                ViewBag.ApprovedClaims = 0;
                ViewBag.PendingClaims = 0;
                ViewBag.TotalAmount = 0;
                ViewBag.RecentClaims = new List<Claim>();
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var allClaims = _dataService.GetClaims();
                List<Claim> userClaims = new List<Claim>();

                if (currentUser != null)
                {
                    if (currentUser.Role == "Lecturer")
                    {
                        var legacyUser = _dataService.GetUserByEmail(currentUser.Email ?? string.Empty);
                        if (legacyUser != null && legacyUser.UserId != 0)
                        {
                            userClaims = allClaims.Where(c => c.LecturerId == legacyUser.UserId).ToList();
                        }
                    }
                    else if (currentUser.Role == "Coordinator")
                    {
                        userClaims = allClaims.Where(c => c.Status == "Submitted" || c.Status == "With Coordinator").ToList();
                    }
                    else if (currentUser.Role == "Manager")
                    {
                        userClaims = allClaims.Where(c => c.Status == "With Manager").ToList();
                    }
                    else if (currentUser.Role == "HR")
                    {
                        userClaims = allClaims.Where(c => c.Status == "Approved").ToList();
                    }
                    else
                    {
                        userClaims = allClaims;
                    }
                }
                else
                {
                    userClaims = allClaims;
                }

                var stats = new
                {
                    success = true,
                    totalClaims = userClaims.Count,
                    approvedClaims = userClaims.Count(c => c.Status == "Approved"),
                    pendingClaims = userClaims.Count(c => c.Status == "Submitted" || c.Status == "With Coordinator" || c.Status == "With Manager"),
                    totalAmount = userClaims.Where(c => c.Status == "Approved").Sum(c => c.Amount),
                    recentClaims = userClaims.OrderByDescending(c => c.SubmittedDate).Take(5).Select(c => new
                    {
                        claimId = c.ClaimId,
                        claimMonth = c.ClaimMonth,
                        totalHours = c.TotalHours,
                        amount = c.Amount,
                        status = c.Status,
                        submittedDate = c.SubmittedDate
                    })
                };

                return Json(stats);
            }
            catch (Exception ex)
            {
                // Log the exception properly in a real application
                return Json(new { success = false, error = "Unable to load dashboard statistics" });
            }
        }

       

        public class DashboardViewModel
        {
            public int TotalClaims { get; set; }
            public int ApprovedClaims { get; set; }
            public int PendingClaims { get; set; }
            public decimal TotalAmount { get; set; }
            public List<Claim> RecentClaims { get; set; } = new List<Claim>();
        }
    }
}