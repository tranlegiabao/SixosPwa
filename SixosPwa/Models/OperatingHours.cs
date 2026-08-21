using System.Globalization;

namespace SixosPwa.Models;

public sealed class OperatingHours
{
    public OperatingHours(string days, string openTime, string closeTime)
    {
        Days = days;
        OpenTime = openTime;
        CloseTime = closeTime;
    }

    public string Days { get; }
    public string OpenTime { get; }
    public string CloseTime { get; }

    public static OperatingHours Default => new("Thứ 2 - Chủ nhật", "07:00", "17:00");

    public static string Encode(string days, string openTime, string closeTime) =>
        $"{days}|{openTime}-{closeTime}";

    public static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(
            value,
            new[] { "HH:mm", "H:mm" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);

    public static bool IsValidRange(string openTime, string closeTime) =>
        TryParseTime(openTime, out var open) &&
        TryParseTime(closeTime, out var close) &&
        close > open;

    public static bool TryParse(string? storedValue, out OperatingHours? hours)
    {
        hours = null;
        if (string.IsNullOrWhiteSpace(storedValue)) return false;

        var value = storedValue.Trim();
        string? days;
        string? openTime;
        string? closeTime;

        var encodedParts = value.Split('|', 2, StringSplitOptions.TrimEntries);
        if (encodedParts.Length == 2)
        {
            days = encodedParts[0];
            if (!TrySplitTimeRange(encodedParts[1], out openTime, out closeTime)) return false;
        }
        else
        {
            var legacyParts = value.Split(',', 2, StringSplitOptions.TrimEntries);
            if (legacyParts.Length != 2) return false;

            if (TrySplitTimeRange(legacyParts[0], out openTime, out closeTime))
            {
                days = legacyParts[1];
            }
            else if (TrySplitTimeRange(legacyParts[1], out openTime, out closeTime))
            {
                days = legacyParts[0];
            }
            else
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(days) ||
            !IsValidRange(openTime!, closeTime!)) return false;

        hours = new OperatingHours(days.Trim(), openTime!, closeTime!);
        return true;
    }

    private static bool TrySplitTimeRange(string value, out string? openTime, out string? closeTime)
    {
        openTime = null;
        closeTime = null;

        var normalized = value.Replace('–', '-').Replace('—', '-');
        var separator = normalized.IndexOf('-');
        if (separator <= 0 || separator >= normalized.Length - 1) return false;

        var open = normalized[..separator].Trim();
        var close = normalized[(separator + 1)..].Trim();
        if (!TryParseTime(open, out var openValue) || !TryParseTime(close, out var closeValue)) return false;

        openTime = openValue.ToString("HH:mm", CultureInfo.InvariantCulture);
        closeTime = closeValue.ToString("HH:mm", CultureInfo.InvariantCulture);
        return true;
    }
}
