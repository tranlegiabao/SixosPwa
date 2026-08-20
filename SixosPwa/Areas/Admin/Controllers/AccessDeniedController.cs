using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SixosPwa.Areas.Admin.Controllers;

[Area("Admin")]
[AllowAnonymous]
public sealed class AccessDeniedController : Controller
{
    public IActionResult Index() => View();
}
