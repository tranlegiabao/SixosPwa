namespace SixosPwa.Security;

public static class AdminReauthentication
{
    public const string Scheme = "AdminReauthentication";
    public const string CookieName = "SixosPwaAdminReauth";

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
