using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.StaticFiles;
using SixosPwa.Services;
using SixosPwa.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Dang ky SignalR
builder.Services.AddSignalR();

// Dang ky dich vu SMS
builder.Services.AddScoped<ISmsService, TwilioSmsService>();

// Dang ky dich vu benh nhan
builder.Services.AddSingleton<IBenhNhanService, InMemoryBenhNhanService>();
builder.Services.AddSingleton<ILichSuTinNhanService, InMemoryLichSuTinNhanService>();
builder.Services.AddSingleton<IMauTinNhanService, InMemoryMauTinNhanService>();
builder.Services.AddSingleton<IHoaDonService, InMemoryHoaDonService>();

// Dang ky dich vu tai khoan
builder.Services.AddSingleton<ITaiKhoanService, InMemoryTaiKhoanService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/DangNhap/Login";
        options.LogoutPath = "/DangNhap/DangXuat";
        options.ExpireTimeSpan = TimeSpan.FromDays(365);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SixosPwaAuthCookie";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

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
app.UseAuthorization();

// Vao thang la ra man dang nhap - cung chinh la start_url trong manifest.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=DangNhap}/{action=Login}/{id?}");

// Map SignalR Hub
app.MapHub<ThongBaoHub>("/thongBaoHub");

app.Run();

