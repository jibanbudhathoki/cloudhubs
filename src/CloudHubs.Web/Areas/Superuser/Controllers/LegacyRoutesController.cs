using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CloudHubs.Web.Areas.Superuser.Controllers;

[Area("Superuser")]
[Route("super-admin")]
[AllowAnonymous]
public class LegacyRoutesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => Redirect("/su/");

    [HttpGet("login")]
    public IActionResult Login() => Redirect("/su/login");

    [HttpGet("organization")]
    public IActionResult Organizations() => Redirect("/su/organization");

    [HttpGet("projects")]
    public IActionResult Projects() => Redirect("/su/projects");

    [HttpGet("users")]
    public IActionResult Users() => Redirect("/su/users");
}
