using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ABCRetail.AzureStorage.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IAppLogger _appLogger;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IAuthService authService, IAppLogger appLogger, ILogger<AccountController> logger)
        {
            _authService = authService;
            _appLogger = appLogger;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email and password are required.");
                return View();
            }

            var customer = await _authService.LoginAsync(email, password);

            if (customer == null)
            {
                await _appLogger.LogAsync("Warning", "Account", $"Failed login attempt for email: {email}");
                ModelState.AddModelError("", "Invalid email or password.");
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, customer.CustomerId ?? customer.RowKey),
                new Claim(ClaimTypes.Name, $"{customer.FirstName} {customer.LastName}"),
                new Claim(ClaimTypes.Email, customer.Email ?? ""),
                new Claim(ClaimTypes.Role, customer.Role ?? "Customer"),
                new Claim("CustomerId", customer.CustomerId ?? customer.RowKey)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            await _appLogger.LogAsync("Information", "Account",
                $"User logged in: {customer.Email} as {customer.Role}", customer.CustomerId);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (customer.Role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Customer customer, string confirmPassword)
        {
            ModelState.Remove("CustomerId");
            ModelState.Remove("PartitionKey");
            ModelState.Remove("RowKey");
            ModelState.Remove("ETag");
            ModelState.Remove("Timestamp");
            ModelState.Remove("Role");

            if (customer.Password != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                return View(customer);
            }

            if (ModelState.IsValid)
            {
                customer.Role = "Customer";
                var success = await _authService.RegisterAsync(customer);

                if (success)
                {
                    await _appLogger.LogAsync("Information", "Account",
                        $"New customer registered: {customer.Email}", customer.CustomerId);
                    TempData["SuccessMessage"] = "Registration successful! Please log in.";
                    return RedirectToAction("Login");
                }

                await _appLogger.LogAsync("Warning", "Account",
                    $"Registration failed - email exists: {customer.Email}");
                ModelState.AddModelError("", "Email already exists. Please use a different email.");
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user = User.Identity?.Name ?? "Unknown";
            var userId = User.FindFirst("CustomerId")?.Value;

            await _appLogger.LogAsync("Information", "Account",
                $"User logged out: {user}", userId);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}