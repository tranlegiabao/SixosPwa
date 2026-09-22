using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Services;
using SixosPwa.Services.Partner;

namespace SixosPwa.Controllers;

[AllowAnonymous]
public class PwaController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IFtpService _ftp;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PwaController> _logger;

    public PwaController(
        ApplicationDbContext dbContext,
        IFtpService ftp,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IWebHostEnvironment env,
        ILogger<PwaController> logger)
    {
        _dbContext = dbContext;
        _ftp = ftp;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _env = env;
        _logger = logger;
    }

    [HttpGet("/manifest.webmanifest")]
    [HttpGet("/manifest.json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetManifest([FromQuery] string? coSo = null)
    {
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        var slug = ResolveCoSoSlug(coSo);
        DMCSKCB? clinic = null;

        if (!string.IsNullOrWhiteSpace(slug))
        {
            clinic = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == slug || x.MaCoSo == slug);
        }

        object manifestObj;

        if (clinic != null && !string.IsNullOrWhiteSpace(clinic.TenCoSo))
        {
            var tenCoSo = clinic.TenCoSo.Trim();
            var clinicSlug = !string.IsNullOrWhiteSpace(clinic.Slug) ? clinic.Slug : slug!;
            var startUrl = $"/DangNhap/Login?coSo={Uri.EscapeDataString(clinicSlug)}";

            manifestObj = new
            {
                id = $"/?coSo={Uri.EscapeDataString(clinicSlug)}",
                name = tenCoSo,
                short_name = tenCoSo,
                description = $"Cổng thông tin và quản lý hồ sơ bệnh nhân {tenCoSo}",
                lang = "vi",
                dir = "ltr",
                start_url = startUrl,
                scope = "/",
                display = "standalone",
                orientation = "any",
                background_color = "#ffffff",
                theme_color = "#3854A4",
                icons = new[]
                {
                    new
                    {
                        src = $"/pwa/icon/192.png?coSo={Uri.EscapeDataString(clinicSlug)}",
                        sizes = "192x192",
                        type = "image/png",
                        purpose = "any"
                    },
                    new
                    {
                        src = $"/pwa/icon/512.png?coSo={Uri.EscapeDataString(clinicSlug)}",
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "any"
                    },
                    new
                    {
                        src = $"/pwa/icon/maskable-512.png?coSo={Uri.EscapeDataString(clinicSlug)}",
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "maskable"
                    }
                }
            };
        }
        else
        {
            manifestObj = new
            {
                id = "/",
                name = "HisSoft — Sixos",
                short_name = "HisSoft",
                description = "Phần mềm quản lý phòng khám / bệnh viện HisSoft của Sixos.",
                lang = "vi",
                dir = "ltr",
                start_url = "/",
                scope = "/",
                display = "standalone",
                orientation = "any",
                background_color = "#ffffff",
                theme_color = "#3854A4",
                icons = new[]
                {
                    new
                    {
                        src = "/static/icon-192.png",
                        sizes = "192x192",
                        type = "image/png",
                        purpose = "any"
                    },
                    new
                    {
                        src = "/static/icon-512.png",
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "any"
                    },
                    new
                    {
                        src = "/static/icon-maskable-512.png",
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "maskable"
                    }
                }
            };
        }

        var json = JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return Content(json, "application/manifest+json; charset=utf-8");
    }

    [HttpGet("/pwa/icon/{tenIcon}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetIcon(string tenIcon, [FromQuery] string? coSo = null)
    {
        var slug = ResolveCoSoSlug(coSo);
        var normalizedIcon = tenIcon.ToLowerInvariant().Trim();

        int targetSize = 192;
        bool isMaskable = false;

        if (normalizedIcon.Contains("180")) targetSize = 180;
        else if (normalizedIcon.Contains("512")) targetSize = 512;
        else if (normalizedIcon.Contains("192")) targetSize = 192;

        if (normalizedIcon.Contains("maskable")) isMaskable = true;

        var cacheKey = $"PWA_ICON_{slug ?? "default"}_{targetSize}_{(isMaskable ? "maskable" : "std")}";
        if (_cache.TryGetValue(cacheKey, out byte[]? cachedBytes) && cachedBytes != null && cachedBytes.Length > 0)
        {
            return File(cachedBytes, "image/png");
        }

        // 1. Kiem tra tep tinh san co trong static (vd static/icon-pkdk-thien-nam-192.png)
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var staticName = isMaskable
                ? $"icon-{slug}-maskable-{targetSize}.png"
                : (targetSize == 180 ? $"apple-touch-icon-{slug}-180.png" : $"icon-{slug}-{targetSize}.png");

            var staticPath = Path.Combine(_env.WebRootPath, "static", staticName);
            if (System.IO.File.Exists(staticPath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(staticPath);
                _cache.Set(cacheKey, bytes, TimeSpan.FromHours(24));
                return File(bytes, "image/png");
            }
        }

        // 2. Lay logo tu DB cua co so va tao icon dynamic
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var clinic = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == slug || x.MaCoSo == slug);

            if (clinic != null && !string.IsNullOrWhiteSpace(clinic.Logo))
            {
                var logoUrl = Uri.UnescapeDataString(clinic.Logo.Trim());
                byte[]? rawLogoBytes = await LayBytesAnhLogoAsync(logoUrl);

                if (rawLogoBytes != null && rawLogoBytes.Length > 0)
                {
                    try
                    {
                        var processedBytes = TaoIconVuong(rawLogoBytes, targetSize, isMaskable);
                        if (processedBytes != null && processedBytes.Length > 0)
                        {
                            _cache.Set(cacheKey, processedBytes, TimeSpan.FromHours(24));
                            return File(processedBytes, "image/png");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lỗi tạo icon PWA cho cơ sở {Slug}", slug);
                    }
                }
            }
        }

        // 3. Fallback ve icon mac dinh cua HisSoft
        var fallbackFile = isMaskable
            ? "icon-maskable-512.png"
            : (targetSize == 180 ? "apple-touch-icon-180.png" : (targetSize == 512 ? "icon-512.png" : "icon-192.png"));

        var fallbackPath = Path.Combine(_env.WebRootPath, "static", fallbackFile);
        if (System.IO.File.Exists(fallbackPath))
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(fallbackPath);
            return File(bytes, "image/png");
        }

        return NotFound();
    }

    private string? ResolveCoSoSlug(string? queryCoSo)
    {
        if (!string.IsNullOrWhiteSpace(queryCoSo))
        {
            return queryCoSo.Trim();
        }

        var cookieSlug = Request.Cookies["pwa_co_so"];
        if (!string.IsNullOrWhiteSpace(cookieSlug))
        {
            return cookieSlug.Trim();
        }

        var claimMaCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        if (!string.IsNullOrWhiteSpace(claimMaCoSo))
        {
            return claimMaCoSo.Trim();
        }

        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrWhiteSpace(referer))
        {
            try
            {
                var uri = new Uri(referer);
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                if (query.TryGetValue("coSo", out var csVal) && !string.IsNullOrWhiteSpace(csVal))
                {
                    return csVal.ToString().Trim();
                }

                var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segs.Length >= 2 && string.Equals(segs[0], "DangKyOnline", StringComparison.OrdinalIgnoreCase))
                {
                    return segs[1].Trim();
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private async Task<byte[]?> LayBytesAnhLogoAsync(string logoUrl)
    {
        try
        {
            if (logoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                logoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var client = _httpClientFactory.CreateClient();
                return await client.GetByteArrayAsync(logoUrl);
            }

            if (logoUrl.StartsWith("/anh/", StringComparison.OrdinalIgnoreCase))
            {
                var ftpPath = KhoAnh.DuongDanFtpTuUrl(logoUrl);
                if (ftpPath != null)
                {
                    using var stream = await _ftp.DownloadAsync(ftpPath);
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    return ms.ToArray();
                }
            }

            if (logoUrl.StartsWith("/static/", StringComparison.OrdinalIgnoreCase))
            {
                var relPath = logoUrl["/static/".Length..].Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(_env.WebRootPath, "static", relPath);
                if (System.IO.File.Exists(fullPath))
                {
                    return await System.IO.File.ReadAllBytesAsync(fullPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đọc được ảnh logo từ nguồn {LogoUrl}", logoUrl);
        }

        return null;
    }

    // Dùng SkiaSharp chứ KHÔNG phải System.Drawing: System.Drawing chỉ chạy trên Windows
    // kể từ .NET 6, và không đọc nổi WebP — đo 22/09 thấy 3/11 cơ sở mất logo riêng chỉ vì
    // nguồn ảnh trả về `image/webp` (ADR 0043). SkiaSharp đọc WebP sẵn và chạy mọi nền tảng.
    private static byte[]? TaoIconVuong(byte[] rawBytes, int size, bool isMaskable)
    {
        using var srcBmp = SKBitmap.Decode(rawBytes);
        // Decode trả null khi byte không phải ảnh nhận ra được (trang chặn hotlink trả HTML
        // với HTTP 200, SVG, định dạng lạ). Trả null để chỗ gọi rơi về icon mặc định.
        if (srcBmp == null || srcBmp.Width <= 0 || srcBmp.Height <= 0)
        {
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Tỷ lệ lề an toàn:
        // maskable icon cần padding 15-20% quanh logo để tránh bị bo tròn / cắt xén
        double marginFactor = isMaskable ? 0.20 : 0.06;
        int availW = (int)(size * (1.0 - 2 * marginFactor));
        int availH = (int)(size * (1.0 - 2 * marginFactor));

        double ratio = Math.Min((double)availW / srcBmp.Width, (double)availH / srcBmp.Height);
        int drawW = (int)(srcBmp.Width * ratio);
        int drawH = (int)(srcBmp.Height * ratio);

        int offsetX = (size - drawW) / 2;
        int offsetY = (size - drawH) / 2;

        // Mitchell cubic — logo gần như luôn bị THU NHỎ (512px xuống 192/180px); lấy mẫu
        // kiểu điểm gần nhất làm viền chữ trong logo răng cưa thấy rõ ở cỡ icon.
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        using var srcImg = SKImage.FromBitmap(srcBmp);
        canvas.DrawImage(
            srcImg,
            new SKRect(offsetX, offsetY, offsetX + drawW, offsetY + drawH),
            sampling);
        canvas.Flush();

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray();
    }
}
