using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Security;
using SixosPwa.Services;
using SixosPwa.Services.Partner;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DaotaoHIS")));

// Add Services
builder.Services.AddScoped<ITaiKhoanService, DbTaiKhoanService>();
builder.Services.AddScoped<AdminStoredProcedureService>();

builder.Services.AddHttpClient();
builder.Services.AddScoped<IPartnerGateway, NoApiGateway>();
builder.Services.AddScoped<IPartnerGateway, UbGateway>();
builder.Services.AddScoped<IPartnerGatewayFactory, PartnerGatewayFactory>();
builder.Services.AddScoped<ILuongCongBenhNhan, LuongCongBenhNhan>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/DangNhap/Login";
        options.LogoutPath = "/DangNhap/DangXuat";
        options.AccessDeniedPath = "/Admin/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(365);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SixosPwaAuthCookie";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    })
    .AddCookie(AdminAuthentication.Scheme, options =>
    {
        options.LoginPath = "/Admin/DangNhap/Login";
        options.LogoutPath = "/Admin/DangNhap/Logout";
        options.AccessDeniedPath = "/Admin/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(365);
        options.Cookie.Name = AdminAuthentication.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();

// ---------------------------------------------------------------------------
// Static files - hai tuy chinh BAT BUOC cho PWA.
// Khuon mau lay tu HisSoft: Projects/master_3/.../HisSoft/Program.cs:389-405
// ---------------------------------------------------------------------------
var contentTypes = new FileExtensionContentTypeProvider();

// 1) ASP.NET Core KHONG biet duoi .webmanifest -> tra ve 404 kieu noi dung la.
//    Trinh duyet gap kieu la thi BO QUA manifest va khong bao gio cho cai dat.
//    Day la loi pho bien nhat khi lam PWA tren ASP.NET.
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes,

    // 2) sw.js va manifest phai luon lay ban moi tu server. Neu de trinh duyet
    //    cache 2 file nay thi sau khi sua service worker, may khach van chay ban
    //    cu - dung cai bay ma ADR 0002 tim cach tranh.
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        if (name.Equals("sw.js", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".webmanifest", StringComparison.OrdinalIgnoreCase))
        {
            var headers = ctx.Context.Response.Headers;
            headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "0";
        }
    }
});

app.UseRouting();

app.UseAuthentication();

// Admin luôn yêu cầu một phiên xác thực riêng, không dùng lại phiên đăng nhập chung.
app.Use(async (context, next) =>
{
    var isAdminArea = context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase);
    var isAdminLogin = context.Request.Path.StartsWithSegments("/Admin/DangNhap", StringComparison.OrdinalIgnoreCase);
    var isAccessDeniedPage = context.Request.Path.StartsWithSegments("/Admin/AccessDenied", StringComparison.OrdinalIgnoreCase);
    var adminAuth = await context.AuthenticateAsync(AdminAuthentication.Scheme);

    if (isAdminArea && !isAdminLogin && !isAccessDeniedPage)
    {
        if (!adminAuth.Succeeded || adminAuth.Principal?.IsInRole("Admin") != true)
        {
            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect("/Admin/DangNhap/Login?returnUrl=" + Uri.EscapeDataString(returnUrl));
            return;
        }
    }

    await next();
});

app.UseAuthorization();

// Vao thang la ra trang chu
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=ThongTinBenhNhan}/{id?}");

app.Run();


