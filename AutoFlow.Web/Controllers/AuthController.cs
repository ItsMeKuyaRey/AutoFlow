using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AutoFlow.Web.Controllers;

public class AuthController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Login page
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] =
            string.IsNullOrWhiteSpace(returnUrl)
                ? "/"
                : returnUrl;

        ViewData["FirebaseApiKey"] =
            _configuration["Firebase:ApiKey"];

        ViewData["FirebaseAuthDomain"] =
            _configuration["Firebase:AuthDomain"];

        ViewData["FirebaseProjectId"] =
            _configuration["Firebase:ProjectId"];

        ViewData["FirebaseStorageBucket"] =
            _configuration["Firebase:StorageBucket"];

        ViewData["FirebaseMessagingSenderId"] =
            _configuration["Firebase:MessagingSenderId"];

        ViewData["FirebaseAppId"] =
            _configuration["Firebase:AppId"];

        return View();
    }

    // Firebase login
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirebaseLogin(
        [FromForm] string idToken,
        [FromForm] string? returnUrl = "/Home/Index")
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return BadRequest(new
            {
                success = false,
                message = "Firebase ID token is missing."
            });
        }

        try
        {
            var decodedToken =
                await FirebaseAuth.DefaultInstance
                    .VerifyIdTokenAsync(idToken);

            var email =
                decodedToken.Claims
                    .GetValueOrDefault("email")
                    ?.ToString()
                    ?.Trim()
                    ?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning(
                    "Firebase login failed. No email found for user.");

                return Unauthorized(new
                {
                    success = false,
                    message =
                        "Your Google account does not have an email address."
                });
            }

            _logger.LogInformation(
                "Firebase returned email: [{Email}]",
                email);

            var roleMappings =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["r.buscagan.550108@umindanao.edu.ph"] = "Administrator",
                    ["weworkhardandsmart@gmail.com"] = "ServiceAdvisor",
                    ["reynaldobuscaganjr@gmail.com"] = "Technician",
                    ["jellymonsterqt@gmail.com"] = "Cashier"
                };

            if (!roleMappings.TryGetValue(
                    email,
                    out var assignedRole))
            {
                _logger.LogWarning(
                    "AutoFlow login rejected. No role mapping found for {Email}.",
                    email);

                return Unauthorized(new
                {
                    success = false,
                    message =
                        "This Google account is not authorized to access AutoFlow."
                });
            }

            var validRoles =
                new[]
                {
                    "Administrator",
                    "ServiceAdvisor",
                    "Technician",
                    "Cashier"
                };

            var matchedRole =
                validRoles.FirstOrDefault(
                    role =>
                        role.Equals(
                            assignedRole,
                            StringComparison.OrdinalIgnoreCase));

            if (matchedRole == null)
            {
                _logger.LogWarning(
                    "Invalid AutoFlow role {Role} assigned to {Email}.",
                    assignedRole,
                    email);

                return Unauthorized(new
                {
                    success = false,
                    message =
                        "The assigned AutoFlow role is invalid."
                });
            }

            assignedRole = matchedRole;

            var claims =
                new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        decodedToken.Uid),

                    new Claim(
                        ClaimTypes.Email,
                        email),

                    new Claim(
                        ClaimTypes.Name,
                        email),

                    new Claim(
                        ClaimTypes.Role,
                        assignedRole),

                    new Claim(
                        "AutoFlowAccess",
                        "true")
                };

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var principal =
                new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                principal);

            _logger.LogInformation(
                "AutoFlow login successful for {Email} with role {Role}.",
                email,
                assignedRole);

            if (
                string.IsNullOrWhiteSpace(returnUrl) ||
                !Url.IsLocalUrl(returnUrl)
            )
            {
                returnUrl = "/Home/Index";
            }

            return Ok(new
            {
                success = true,
                role = assignedRole,
                email = email,
                redirectUrl = returnUrl
            });
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogError(
                ex,
                "Firebase authentication failed.");

            return Unauthorized(new
            {
                success = false,
                message =
                    "Firebase authentication failed. Please sign in again."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected AutoFlow authentication error.");

            return StatusCode(500, new
            {
                success = false,
                message =
                    "An unexpected authentication error occurred."
            });
        }
    }

    // Access denied
    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // Logout
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme);

        return RedirectToAction(
            nameof(Login));
    }
}