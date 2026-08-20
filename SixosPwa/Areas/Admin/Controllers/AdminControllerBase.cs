using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SixosPwa.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public abstract class AdminControllerBase : Controller
{
    protected static readonly string[] AllowedRoles = { "Admin", "DoiTac", "User" };
    protected const int DefaultPageSize = 12;

    protected void Success(string message) => TempData["AdminSuccess"] = message;
    protected void Error(string message) => TempData["AdminError"] = message;

    protected static int SafePage(int page) => Math.Max(1, page);
}
