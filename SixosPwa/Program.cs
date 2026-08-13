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

