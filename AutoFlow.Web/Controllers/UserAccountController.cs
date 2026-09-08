using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class UserAccountsController : Controller
{
    private static readonly List<UserAccountViewModel> Users =
        new()
        {
            new UserAccountViewModel
            {
                Email = "r.buscagan.550108@umindanao.edu.ph",
                Role = "Administrator",
                Status = "Active"
            },
            new UserAccountViewModel
            {
                Email = "weworkhardandsmart@gmail.com",
                Role = "ServiceAdvisor",
                Status = "Active"
            },
            new UserAccountViewModel
            {
                Email = "reynaldobuscaganjr@gmail.com",
                Role = "Technician",
                Status = "Active"
            },
            new UserAccountViewModel
            {
                Email = "jellymonsterqt@gmail.com",
                Role = "Cashier",
                Status = "Active"
            }
        };

    [HttpGet]
    public IActionResult Index()
    {
        return View(Users);
    }
}

public class UserAccountViewModel
{
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";
}