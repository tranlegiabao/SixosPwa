using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixosPwa.Security;

namespace SixosPwa.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
public abstract class AdminControllerBase : Controller
{
    protected static readonly string[] AllowedRoles = { "Admin", "BenhNhan" };
    protected const int DefaultPageSize = 12;

    protected void Success(string message) => TempData["AdminSuccess"] = message;
    protected void Error(string message) => TempData["AdminError"] = message;

    protected static int SafePage(int page) => Math.Max(1, page);

    protected static string NormalizeRole(string? role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "BenhNhan";
}
