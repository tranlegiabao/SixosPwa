using Microsoft.EntityFrameworkCore;
using SixosPwa.Models;

namespace SixosPwa.Data;

/// <summary>
/// Database Context cho ứng dụng
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<BenhNhan> BenhNhans { get; set; }
    public DbSet<TaiKhoan> TaiKhoans { get; set; }
    public DbSet<LichSuTinNhan> LichSuTinNhans { get; set; }
    public DbSet<MauTinNhan> MauTinNhans { get; set; }
    public DbSet<HoaDon> HoaDons { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cấu hình BenhNhan
        modelBuilder.Entity<BenhNhan>(entity =>
        {
            entity.ToTable("BenhNhan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MaBenhNhan).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HoTen).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SoDienThoai).HasMaxLength(20).IsRequired();
            entity.Property(e => e.GioiTinh).HasMaxLength(10);
            entity.Property(e => e.DiaChi).HasMaxLength(500);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.HasIndex(e => e.SoDienThoai).IsUnique();
        });

        // Cấu hình TaiKhoan
        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.ToTable("TaiKhoan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenDangNhap).HasMaxLength(100).IsRequired();
            entity.Property(e => e.MatKhau).HasMaxLength(200).IsRequired();
            entity.Property(e => e.HoTen).HasMaxLength(200).IsRequired();
            entity.Property(e => e.LoaiTaiKhoan).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.TenDangNhap).IsUnique();
        });

        // Cấu hình LichSuTinNhan
        modelBuilder.Entity<LichSuTinNhan>(entity =>
        {
            entity.ToTable("LichSuTinNhan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NoiDung).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.LoaiTinNhan).HasMaxLength(100);
            entity.Property(e => e.DoiTac).HasMaxLength(200);
            entity.Property(e => e.TrangThai).HasMaxLength(50);
            entity.Property(e => e.GhiChu).HasMaxLength(500);
            
            // Relationship
            entity.HasOne(e => e.BenhNhan)
                .WithMany()
                .HasForeignKey(e => e.BenhNhanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình MauTinNhan
        modelBuilder.Entity<MauTinNhan>(entity =>
        {
            entity.ToTable("MauTinNhan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenMau).HasMaxLength(200).IsRequired();
            entity.Property(e => e.LoaiTinNhan).HasMaxLength(100);
            entity.Property(e => e.NoiDung).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.MoTa).HasMaxLength(500);
        });

        // Cấu hình HoaDon
        modelBuilder.Entity<HoaDon>(entity =>
        {
            entity.ToTable("HoaDon");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MaHoaDon).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DichVu).HasMaxLength(500).IsRequired();
            entity.Property(e => e.TongTien).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DaThanhToan).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ConLai).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TrangThai).HasMaxLength(50);
            
            // Relationship
            entity.HasOne(e => e.BenhNhan)
                .WithMany()
                .HasForeignKey(e => e.BenhNhanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed data mẫu cho TaiKhoan
        modelBuilder.Entity<TaiKhoan>().HasData(
            new TaiKhoan { Id = 1, TenDangNhap = "0999999999", MatKhau = "admin123", HoTen = "Quản trị viên", LoaiTaiKhoan = "Admin", KichHoat = true },
            new TaiKhoan { Id = 2, TenDangNhap = "0988888888", MatKhau = "doitac123", HoTen = "Phòng khám ABC", LoaiTaiKhoan = "DoiTac", KichHoat = true }
        );

        // Seed data mẫu cho MauTinNhan
        modelBuilder.Entity<MauTinNhan>().HasData(
            new MauTinNhan { Id = 1, TenMau = "Nhắc lịch khám", LoaiTinNhan = "NhacLichKham", NoiDung = "Chào {TenBenhNhan}, nhắc lịch khám: {NgayKham} lúc {GioKham}. Bác sĩ: {TenBacSi}. Vui lòng đến trước 15 phút.", MoTa = "Template nhắc lịch khám cơ bản", KichHoat = true },
            new MauTinNhan { Id = 2, TenMau = "Kết quả xét nghiệm", LoaiTinNhan = "KetQuaXetNghiem", NoiDung = "Chào {TenBenhNhan}, kết quả {LoaiXetNghiem} đã sẵn sàng. Xem tại: {Link}", MoTa = "Thông báo kết quả xét nghiệm", KichHoat = true },
            new MauTinNhan { Id = 3, TenMau = "Nhắc uống thuốc", LoaiTinNhan = "NhacUongThuoc", NoiDung = "Nhắc nhở: Đã đến giờ uống thuốc {TenThuoc}. Liều dùng: {LieuDung}. Chúc mau khỏe!", MoTa = "Nhắc nhở uống thuốc đúng giờ", KichHoat = true },
            new MauTinNhan { Id = 4, TenMau = "Cảm ơn sau khám", LoaiTinNhan = "CamOn", NoiDung = "Cảm ơn {TenBenhNhan} đã tin tưởng sử dụng dịch vụ. Chúc bạn sớm bình phục!", MoTa = "Tin nhắn cảm ơn sau khi khám", KichHoat = true }
        );
    }
}
