using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DaotaoHIS")));

// Add Services
builder.Services.AddScoped<ITaiKhoanService, DbTaiKhoanService>();

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

// Auto-tạo bảng ThongBao nếu chưa tồn tại (không dùng Migration cho bảng mới này)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SixosPwa.Data.ApplicationDbContext>();
    try
    {
        // Chạy SQL tạo bảng nếu chưa có – an toàn, không ảnh hưởng DB cũ
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ThongBao' AND xtype='U')
            CREATE TABLE ThongBao (
                Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
                NoiDung     NVARCHAR(1000) NOT NULL,
                ThoiGian    DATETIME2 NOT NULL DEFAULT GETDATE(),
                NguoiGui    NVARCHAR(50) NOT NULL,
                NguoiNhan   NVARCHAR(50) NOT NULL,
                DaDoc       BIT NOT NULL DEFAULT 0
            )
        ");
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PushDangKy' AND xtype='U')
            CREATE TABLE PushDangKy (
                Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
                SDT         NVARCHAR(50) NOT NULL,
                Endpoint    NVARCHAR(1000) NOT NULL,
                P256dh      NVARCHAR(500) NOT NULL,
                Auth        NVARCHAR(200) NOT NULL,
                ThoiGian    DATETIME2 NOT NULL DEFAULT GETDATE()
            )
        ");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Không thể tạo bảng ThongBao tự động: {Message}", ex.Message);
    }
}

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

app.Run();

