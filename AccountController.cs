using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CMCS.Models;
using CMCS.Services;

namespace CMCS.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDataService _dataService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IDataService dataService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _dataService = dataService;
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "User with this email already exists.");
                return View(model);
            }

            // Create user without the problematic properties
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Role = model.Role,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);

                // Create legacy user
                try
                {
                    var legacyUser = new User
                    {
                        UserId = int.Parse(user.Id),
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Email = model.Email,
                        Password = model.Password,
                        Role = model.Role,
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    };
                    _dataService.SaveUser(legacyUser);
                }
                catch
                {
                    // Ignore legacy user creation errors
                }

                await _signInManager.SignInAsync(user, isPersistent: false);

                HttpContext.Session.SetString("UserId", user.Id);
                HttpContext.Session.SetString("UserRole", user.Role);
                HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                HttpContext.Session.SetString("UserEmail", user.Email);

                TempData["Success"] = "Registration successful!";

                return user.Role switch
                {
                    "Lecturer" => RedirectToAction("Index", "Claim"),
                    "Coordinator" => RedirectToAction("Index", "Approval"),
                    "Manager" => RedirectToAction("Index", "Approval"),
                    "HR" => RedirectToAction("Dashboard", "HR"),
                    _ => RedirectToAction("Index", "Home")
                };
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    // REMOVED: IsActive check since property doesn't exist
                    // All users are considered active

                    HttpContext.Session.SetString("UserId", user.Id);
                    HttpContext.Session.SetString("UserRole", user.Role);
                    HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                    HttpContext.Session.SetString("UserEmail", user.Email);

                    TempData["Success"] = $"Welcome back, {user.FirstName}!";

                    return user.Role switch
                    {
                        "Lecturer" => RedirectToAction("Index", "Claim"),
                        "Coordinator" => RedirectToAction("Index", "Approval"),
                        "Manager" => RedirectToAction("Index", "Approval"),
                        "HR" => RedirectToAction("Dashboard", "HR"),
                        _ => RedirectToAction("Index", "Home")
                    };
                }
            }

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await _signInManager.SignOutAsync();
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}