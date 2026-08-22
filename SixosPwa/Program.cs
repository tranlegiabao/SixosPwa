using System.Security.Claims;
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

// Cua doi tac: moi kieu API mot ban cai. Them doi tac o giai doan 2 = them
// mot dong AddScoped o day + mot dong trong bang DM_DoiTacApi. Xem ADR 0003.
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
    .AddCookie(AdminReauthentication.Scheme, options =>
    {
        options.Cookie.Name = AdminReauthentication.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false;
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
            BEGIN
                CREATE TABLE PushDangKy (
                    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
                    SDT         NVARCHAR(50) NOT NULL,
                    Endpoint    NVARCHAR(1000) NOT NULL,
                    P256dh      NVARCHAR(500) NOT NULL,
                    Auth        NVARCHAR(200) NOT NULL,
                    ThoiGian    DATETIME2 NOT NULL DEFAULT GETDATE(),
                    IdThietBi   NVARCHAR(100) NULL
                )
            END
            ELSE
            BEGIN
                IF COL_LENGTH('PushDangKy', 'IdThietBi') IS NULL
                BEGIN
                    ALTER TABLE PushDangKy ADD IdThietBi NVARCHAR(100) NULL
                END
            END
        ");
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PhongKham' AND xtype='U')
            CREATE TABLE PhongKham (
                Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
                MaPhongKham     NVARCHAR(20) NOT NULL,
                TenPhongKham    NVARCHAR(200) NOT NULL,
                DiaChi          NVARCHAR(500) NULL,
                SoDienThoai     NVARCHAR(20) NULL,
                MoTa            NVARCHAR(1000) NULL,
                LogoUrl         NVARCHAR(500) NULL
            )
        ");
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LichSuKham' AND xtype='U')
            CREATE TABLE LichSuKham (
                Id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
                MaBN                NVARCHAR(20) NOT NULL,
                PhongKhamId         BIGINT NOT NULL,
                NgayKhamDau         DATETIME2 NOT NULL DEFAULT GETDATE(),
                NgayKhamGanNhat     DATETIME2 NOT NULL DEFAULT GETDATE(),
                SoLanKham           INT NOT NULL DEFAULT 1,
                TrangThai           NVARCHAR(50) NULL,
                FOREIGN KEY (PhongKhamId) REFERENCES PhongKham(Id)
            )
        ");
        
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DMThietBi' AND xtype='U')
            CREATE TABLE DMThietBi (
                ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
                SDT         VARCHAR(20) NULL,
                MaBN        NVARCHAR(255) NULL,
                IDThietBi   NVARCHAR(100) NULL,
                TrangThai   BIT NOT NULL DEFAULT 1,
                TenThietBi  NVARCHAR(255) NULL
            )
        ");

        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ND_CSKCB' AND xtype='U')
            BEGIN
                CREATE TABLE ND_CSKCB (
                    ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
                    MaCoSo     NVARCHAR(10) NULL,
                    TenCoSo    NVARCHAR(100) NULL,
                    NoiDung    NVARCHAR(MAX) NULL,
                    LoaiND     NVARCHAR(20) NULL
                )
            END
            ELSE IF COL_LENGTH('ND_CSKCB', 'LoaiND') IS NOT NULL
                AND (SELECT max_length FROM sys.columns WHERE object_id = OBJECT_ID('ND_CSKCB') AND name = 'LoaiND') < 40
            BEGIN
                ALTER TABLE ND_CSKCB ALTER COLUMN LoaiND NVARCHAR(20) NULL
            END
        ");

        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='QC_KCB' AND xtype='U')
            BEGIN
                CREATE TABLE QC_KCB (
                    ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
                    MaCoSo     NVARCHAR(10) NULL,
                    TenCoSo    NVARCHAR(100) NULL,
                    NoiDung    NVARCHAR(MAX) NULL,
                    Img        NVARCHAR(MAX) NULL
                )
            END
        ");

        db.Database.ExecuteSqlRaw(@"
            IF OBJECT_ID('DMCSKCB', 'U') IS NOT NULL
                AND COL_LENGTH('DMCSKCB', 'Huyen') IS NULL
            BEGIN
                ALTER TABLE DMCSKCB ADD Huyen INT NULL
            END
        ");
        db.Database.ExecuteSqlRaw(@"
            IF OBJECT_ID('DMCSKCB', 'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('DMCSKCB', 'SoToaNha') IS NULL
                    ALTER TABLE DMCSKCB ADD SoToaNha NVARCHAR(100) NULL

                IF COL_LENGTH('DMCSKCB', 'PhuongXa') IS NULL
                    ALTER TABLE DMCSKCB ADD PhuongXa INT NULL
            END
        ");
        
        // Thêm dữ liệu mẫu phòng khám nếu chưa có
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM PhongKham)
            BEGIN
                INSERT INTO PhongKham (MaPhongKham, TenPhongKham, DiaChi, SoDienThoai, MoTa, LogoUrl) VALUES
                ('PKDK-BM', N'PKDK Bảo Minh', N'Địa chỉ: Nguyễn Văn Trỗi, Phường Phú Hòa, TP. Bến Cát, Tỉnh Bình Dương', '0911-449-115', N'Phòng khám đa khoa Bảo Minh - Giấy phép hoạt động số: 01083/BĐ-GPHĐ. Tiếp nhận tất cả trường hợp khám chữa bệnh BHYT trong và ngoài tỉnh', '/static/logo-baominh.png'),
                ('PKDK-TĐ', N'PKDK Tâm Đức', N'456 Lê Văn Việt, Quận 9, TP.HCM', '028-3777-7888', N'Phòng khám đa khoa Tâm Đức - Chuyên khoa Tim mạch, Nội tổng quát, tầm soát và điều trị bệnh tim mạch', '/static/logo-tamduc.png'),
                ('PKĐK-AĐ', N'PKĐK Ánh Dương', N'789 Hoàng Diệu, Quận 4, TP.HCM', '028-3666-6777', N'Phòng khám đa khoa Ánh Dương - Chuyên khoa Nhi, Sản phụ khoa, chăm sóc sức khỏe toàn diện', '/static/logo-anhdương.png'),
                ('BV-ĐK', N'Bệnh Viện Đa Khoa Saigon', N'125 Lê Lợi, Quận 1, TP.HCM', '028-3829-2071', N'Bệnh viện đa khoa hạng I - Khám bệnh tổng quát, chuyên khoa sâu, cấp cứu 24/7', '/static/logo-bvdk.png'),
                ('PK-TMH', N'PK Tai Mũi Họng Sài Gòn', N'234 Võ Văn Tần, Quận 3, TP.HCM', '028-3930-3456', N'Chuyên khoa Tai Mũi Họng - Điều trị viêm xoang, viêm amidan, polyp mũi bằng công nghệ hiện đại', '/static/logo-tmh.png'),
                ('PK-MẮT', N'PK Mắt Quốc Tế', N'567 Nguyễn Thị Minh Khai, Quận 3, TP.HCM', '028-3822-5678', N'Phòng khám chuyên khoa Mắt - Khám, điều trị các bệnh về mắt, phẫu thuật mắt laser', '/static/logo-mat.png');
                
                -- Thêm lịch sử khám mẫu cho bệnh nhân BN-2026-8892
                INSERT INTO LichSuKham (MaBN, PhongKhamId, NgayKhamDau, NgayKhamGanNhat, SoLanKham, TrangThai) VALUES
                ('BN-2026-8892', 1, '2024-03-15', '2026-08-10', 8, N'Đang theo dõi định kỳ'),
                ('BN-2026-8892', 2, '2025-06-10', '2026-07-20', 3, N'Ổn định'),
                ('BN-2026-8892', 4, '2025-01-05', '2026-05-15', 5, N'Đang điều trị'),
                ('BN-2026-8892', 5, '2024-11-20', '2025-12-10', 2, N'Đã khỏi');
            END
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
    var isAccessDeniedPage = context.Request.Path.StartsWithSegments("/Admin/AccessDenied", StringComparison.OrdinalIgnoreCase);

    if (isAdminArea && !isAccessDeniedPage)
    {
        var adminAuth = await context.AuthenticateAsync(AdminReauthentication.Scheme);
        var currentAccount = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.Identity?.Name;
        var adminAccount = adminAuth.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? adminAuth.Principal?.Identity?.Name;

        if (context.User.Identity?.IsAuthenticated != true
            || !adminAuth.Succeeded
            || string.IsNullOrWhiteSpace(currentAccount)
            || !string.Equals(currentAccount, adminAccount, StringComparison.OrdinalIgnoreCase))
        {
            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect("/DangNhap/Login?returnUrl=" + Uri.EscapeDataString(returnUrl));
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

