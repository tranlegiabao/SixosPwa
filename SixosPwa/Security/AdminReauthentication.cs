namespace SixosPwa.Security;

public static class AdminAuthentication
{
    public const string Scheme = "AdminAuthentication";
    public const string CookieName = "SixosPwaAdminCookie";

    public static bool IsAdminReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return returnUrl.Length == "/Admin".Length
            || returnUrl["/Admin".Length] is '/' or '?';
    }
}
