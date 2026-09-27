using AutoFlow.Web.Data;
using AutoFlow.Web.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Firebase
var firebaseCredentialPath =
    Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

if (string.IsNullOrWhiteSpace(firebaseCredentialPath))
{
    throw new InvalidOperationException(
        "GOOGLE_APPLICATION_CREDENTIALS is not configured.");
}

if (!File.Exists(firebaseCredentialPath))
{
    throw new FileNotFoundException(
        "Firebase service account JSON file was not found.",
        firebaseCredentialPath);
}

FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile(firebaseCredentialPath)
});

// Database
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is missing. " +
        "Set ConnectionStrings__DefaultConnection.");
}

builder.Services.AddDbContext<AutoFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

// Authentication
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultSignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "AutoFlow.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// MVC
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddScoped<SupabaseStorageService>();
builder.Services.AddHttpClient<XenditPaymentService>();

// Reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy =
        new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim("AutoFlowAccess", "true")
            .Build();
});

var app = builder.Build();

// Health check
app.MapGet(
    "/health",
    () =>
        Results.Ok(
            new
            {
                status = "ok",
                service = "AutoFlow"
            }))
    .AllowAnonymous();

// Debug authentication
app.MapGet(
    "/debug-auth",
    (HttpContext context) =>
    {
        return Results.Json(
            new
            {
                IsAuthenticated =
                    context.User.Identity?.IsAuthenticated,

                Name =
                    context.User.Identity?.Name,

                Role =
                    context.User.FindFirst(
                        System.Security.Claims.ClaimTypes.Role)?.Value,

                Email =
                    context.User.FindFirst(
                        System.Security.Claims.ClaimTypes.Email)?.Value,

                AutoFlowAccess =
                    context.User.FindFirst("AutoFlowAccess")?.Value,

                Claims =
                    context.User.Claims.Select(
                        c => new
                        {
                            c.Type,
                            c.Value
                        })
            });
    })
    .RequireAuthorization();

// Error handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// HTTP pipeline
app.UseForwardedHeaders();

// HTTPS redirection
var httpsPort =
    builder.Configuration["ASPNETCORE_HTTPS_PORT"];

if (!string.IsNullOrWhiteSpace(httpsPort) &&
    int.TryParse(httpsPort, out _))
{
    app.UseHttpsRedirection();
}

// Routing
app.UseRouting();

// Authentication
app.UseAuthentication();

// Authorization
app.UseAuthorization();

// AutoFlow RBAC
app.Use(async (context, next) =>
{
    var path =
        context.Request.Path.Value ?? string.Empty;

    if (path.StartsWith(
            "/Auth",
            StringComparison.OrdinalIgnoreCase) ||
        path.Equals(
            "/health",
            StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var controller =
        context.Request.RouteValues["controller"]
        ?.ToString();

    if (string.IsNullOrWhiteSpace(controller))
    {
        await next();
        return;
    }

    var user = context.User;

    if (user.Identity == null ||
        !user.Identity.IsAuthenticated)
    {
        await next();
        return;
    }

    var userRole =
        user.FindFirst(
            System.Security.Claims.ClaimTypes.Role)?.Value;

    if (string.IsNullOrWhiteSpace(userRole))
    {
        context.Response.Redirect("/Auth/AccessDenied");
        return;
    }

    // Only the Administrator can access the dashboard. Other roles land on their primary module.
    if (controller.Equals(
            "Home",
            StringComparison.OrdinalIgnoreCase))
    {
        if (userRole.Equals(
                "Administrator",
                StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var landingPath = userRole switch
        {
            "ServiceAdvisor" => "/Appointments/Index",
            "Technician" => "/JobOrders/Index",
            "Cashier" => "/Billings/Index",
            _ => "/Auth/AccessDenied"
        };

        context.Response.Redirect(landingPath);
        return;
    }

    // Administrator has full access.
    if (user.IsInRole("Administrator"))
    {
        await next();
        return;
    }

    var permissions =
        new Dictionary<
            string,
            Dictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Customers"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["ServiceAdvisor"] = "F",
                    ["Technician"] = "V"
                },

            ["Vehicles"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["ServiceAdvisor"] = "F",
                    ["Technician"] = "V"
                },

            ["Appointments"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["ServiceAdvisor"] = "F"
                },

            ["JobOrders"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["ServiceAdvisor"] = "F",
                    ["Technician"] = "F",
                    ["Cashier"] = "V"
                },

            ["Parts"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Technician"] = "F",
                    ["Cashier"] = "F"
                },

            ["Suppliers"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Cashier"] = "F"
                },

            ["Billings"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Cashier"] = "F"
                },

            ["CustomerFollowUps"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["ServiceAdvisor"] = "F"
                },

            ["ServiceRecords"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["ServiceAdvisor"] = "V",
                    ["Technician"] = "V"
                },

            ["Reports"] =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["ServiceAdvisor"] = "V",
                    ["Cashier"] = "V"
                }
        };

    if (!permissions.TryGetValue(
            controller,
            out var rolePermissions))
    {
        await next();
        return;
    }

    if (!rolePermissions.TryGetValue(
            userRole,
            out var permission))
    {
        context.Response.Redirect("/Auth/AccessDenied");
        return;
    }

    if (permission.Equals(
            "V",
            StringComparison.OrdinalIgnoreCase))
    {
        var method =
            context.Request.Method;

        if (!HttpMethods.IsGet(method) &&
            !HttpMethods.IsHead(method))
        {
            context.Response.Redirect("/Auth/AccessDenied");
            return;
        }

        await next();
        return;
    }

    if (permission.Equals(
            "F",
            StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    context.Response.Redirect("/Auth/AccessDenied");
});

// Static files
app.MapStaticAssets();

// MVC default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();