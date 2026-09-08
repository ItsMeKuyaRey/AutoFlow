using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class SystemSettingsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}