using Microsoft.EntityFrameworkCore;
using SixosPwa.Models;

namespace SixosPwa.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<DoiTac> DoiTacs => Set<DoiTac>();
    public DbSet<BenhNhan> BenhNhans => Set<BenhNhan>();
    public DbSet<ThietBi> ThietBis => Set<ThietBi>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<ThongBao> ThongBaos => Set<ThongBao>();
    public DbSet<PushDangKy> PushDangKys => Set<PushDangKy>();
    public DbSet<PhongKham> PhongKhams => Set<PhongKham>();
    public DbSet<LichSuKham> LichSuKhams => Set<LichSuKham>();
    public DbSet<DMCSKCB> DMCSKCBs => Set<DMCSKCB>();
    public DbSet<NDCSKCB> NDCSKCBs => Set<NDCSKCB>();
    public DbSet<QCKCB> QCKCBs => Set<QCKCB>();
    public DbSet<DMNhomCS> DMNhomCSs => Set<DMNhomCS>();
    public DbSet<DMChuDe> DMChuDes => Set<DMChuDe>();
    public DbSet<DoiTacApi> DoiTacApis => Set<DoiTacApi>();
    public DbSet<TaiKhoanDoiTac> TaiKhoanDoiTacs => Set<TaiKhoanDoiTac>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure DMDoiTac
        modelBuilder.Entity<DoiTac>().ToTable("DMDoiTac");
        modelBuilder.Entity<DoiTac>().HasKey(e => e.Id);
        modelBuilder.Entity<DoiTac>().Property(e => e.MaDT).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<DoiTac>().Property(e => e.TenDT).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<DoiTac>().Property(e => e.DiaChi).HasMaxLength(255);
        modelBuilder.Entity<DoiTac>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<DoiTac>().Property(e => e.Email).HasMaxLength(100);
        modelBuilder.Entity<DoiTac>().Property(e => e.BrandName).HasMaxLength(100);
        modelBuilder.Entity<DoiTac>().Property(e => e.Password).HasMaxLength(255);

        // Configure DMBenhNhan
        modelBuilder.Entity<BenhNhan>().ToTable("DMBenhNhan");
        modelBuilder.Entity<BenhNhan>().HasKey(e => e.Id);
        modelBuilder.Entity<BenhNhan>().Property(e => e.MaBN).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<BenhNhan>().Property(e => e.MaDT).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<BenhNhan>().Property(e => e.TenBN).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<BenhNhan>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<BenhNhan>().Property(e => e.DiaChi).HasMaxLength(255);
        modelBuilder.Entity<BenhNhan>().Property(e => e.Email).HasMaxLength(100);

        // Configure DMThietBi
        modelBuilder.Entity<ThietBi>().ToTable("DMThietBi");
        modelBuilder.Entity<ThietBi>().HasKey(e => e.Id);
        modelBuilder.Entity<ThietBi>().Property(e => e.MaBN).HasMaxLength(255);
        modelBuilder.Entity<ThietBi>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<ThietBi>().Property(e => e.IdThietBi).HasMaxLength(100);
        modelBuilder.Entity<ThietBi>().Property(e => e.TenThietBi).HasMaxLength(255);

        // Configure TaiKhoan
        modelBuilder.Entity<TaiKhoan>().ToTable("TaiKhoan");
        modelBuilder.Entity<TaiKhoan>().HasKey(e => e.Id);
        modelBuilder.Entity<TaiKhoan>().Property(e => e.SDT).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Role).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Email).HasMaxLength(50);
        modelBuilder.Entity<TaiKhoan>().Property(e => e.CCCD).HasMaxLength(20);

        // Configure ThongBao
        modelBuilder.Entity<ThongBao>().ToTable("ThongBao");
        modelBuilder.Entity<ThongBao>().HasKey(e => e.Id);
        modelBuilder.Entity<ThongBao>().Property(e => e.NoiDung).HasMaxLength(1000).IsRequired();
        modelBuilder.Entity<ThongBao>().Property(e => e.NguoiGui).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<ThongBao>().Property(e => e.NguoiNhan).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<ThongBao>().Property(e => e.ThoiGian).IsRequired();
        modelBuilder.Entity<ThongBao>().Property(e => e.DaDoc).IsRequired();

        // Configure PushDangKy
        modelBuilder.Entity<PushDangKy>().ToTable("PushDangKy");
        modelBuilder.Entity<PushDangKy>().HasKey(e => e.Id);
        modelBuilder.Entity<PushDangKy>().Property(e => e.SDT).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<PushDangKy>().Property(e => e.Endpoint).HasMaxLength(1000).IsRequired();
        modelBuilder.Entity<PushDangKy>().Property(e => e.P256dh).HasMaxLength(500).IsRequired();
        modelBuilder.Entity<PushDangKy>().Property(e => e.Auth).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<PushDangKy>().Property(e => e.IdThietBi).HasMaxLength(100);
        modelBuilder.Entity<PushDangKy>().Property(e => e.ThoiGian).IsRequired();

        // Configure PhongKham
        modelBuilder.Entity<PhongKham>().ToTable("PhongKham");
        modelBuilder.Entity<PhongKham>().HasKey(e => e.Id);
        modelBuilder.Entity<PhongKham>().Property(e => e.MaPhongKham).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<PhongKham>().Property(e => e.TenPhongKham).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<PhongKham>().Property(e => e.DiaChi).HasMaxLength(500);
        modelBuilder.Entity<PhongKham>().Property(e => e.SoDienThoai).HasMaxLength(20);
        modelBuilder.Entity<PhongKham>().Property(e => e.MoTa).HasMaxLength(1000);
        modelBuilder.Entity<PhongKham>().Property(e => e.LogoUrl).HasMaxLength(500);

        // Configure LichSuKham
        modelBuilder.Entity<LichSuKham>().ToTable("LichSuKham");
        modelBuilder.Entity<LichSuKham>().HasKey(e => e.Id);
        modelBuilder.Entity<LichSuKham>().Property(e => e.MaBN).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<LichSuKham>().Property(e => e.PhongKhamId).IsRequired();
        modelBuilder.Entity<LichSuKham>().Property(e => e.NgayKhamDau).IsRequired();
        modelBuilder.Entity<LichSuKham>().Property(e => e.NgayKhamGanNhat).IsRequired();
        modelBuilder.Entity<LichSuKham>().Property(e => e.SoLanKham).IsRequired();
        modelBuilder.Entity<LichSuKham>().Property(e => e.TrangThai).HasMaxLength(50);

        // Configure relationship
        modelBuilder.Entity<LichSuKham>()
            .HasOne(ls => ls.PhongKham)
            .WithMany()
            .HasForeignKey(ls => ls.PhongKhamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure DMCSKCB
        modelBuilder.Entity<DMCSKCB>().ToTable("DMCSKCB");
        modelBuilder.Entity<DMCSKCB>().HasKey(e => e.Id);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.MaCoSo).HasMaxLength(10);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Slug).HasMaxLength(100);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.TenCoSo).HasMaxLength(100);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.DiaChi).HasMaxLength(255);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.LoaiCS).HasMaxLength(20);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.TGLamViec).HasMaxLength(50);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.NgayLamViec).HasMaxLength(50);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.GioMoCua).HasColumnType("time(0)");
        modelBuilder.Entity<DMCSKCB>().Property(e => e.GioDongCua).HasColumnType("time(0)");
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Img).HasMaxLength(500);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.SoToaNha).HasMaxLength(100);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Tinh);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.PhuongXa);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.QuangCao).HasColumnType("decimal(15,0)");

        // Configure ND_CSKCB
        modelBuilder.Entity<NDCSKCB>().ToTable("ND_CSKCB");
        modelBuilder.Entity<NDCSKCB>().HasKey(e => e.Id);
        modelBuilder.Entity<NDCSKCB>().Property(e => e.MaCoSo).HasMaxLength(10);
        modelBuilder.Entity<NDCSKCB>().Property(e => e.TenCoSo).HasMaxLength(100);
        modelBuilder.Entity<NDCSKCB>().Property(e => e.LoaiND).HasMaxLength(20);

        // Configure DM_DoiTacApi — bang dang ky API doi tac (V10)
        modelBuilder.Entity<DoiTacApi>().ToTable("DM_DoiTacApi");
        modelBuilder.Entity<DoiTacApi>().HasKey(e => e.Id);
        modelBuilder.Entity<DoiTacApi>().Property(e => e.MaCoSo).HasMaxLength(10).IsRequired();
        modelBuilder.Entity<DoiTacApi>().Property(e => e.KieuApi).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<DoiTacApi>().Property(e => e.BaseUrl).HasMaxLength(255);
        modelBuilder.Entity<DoiTacApi>().Property(e => e.TrangChu).HasMaxLength(255);
        modelBuilder.Entity<DoiTacApi>().Property(e => e.MaChiNhanh);
        modelBuilder.Entity<DoiTacApi>().HasIndex(e => e.MaCoSo).IsUnique();

        // Configure TaiKhoan_DoiTac — credential cua benh nhan tai tung co so (ADR 0005)
        modelBuilder.Entity<TaiKhoanDoiTac>().ToTable("TaiKhoan_DoiTac");
        modelBuilder.Entity<TaiKhoanDoiTac>().HasKey(e => e.Id);
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.IdTaiKhoan).HasColumnName("IDTaiKhoan").IsRequired();
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.MaCoSo).HasMaxLength(10).IsRequired();
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.MatKhau).HasMaxLength(255);
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.MaXacNhanTam).HasMaxLength(10);
        modelBuilder.Entity<TaiKhoanDoiTac>().HasIndex(e => new { e.IdTaiKhoan, e.MaCoSo }).IsUnique();

        // Configure QC_KCB
        modelBuilder.Entity<QCKCB>().ToTable("QC_KCB");
        modelBuilder.Entity<QCKCB>().HasKey(e => e.Id);
        modelBuilder.Entity<QCKCB>().Property(e => e.MaCoSo).HasMaxLength(10);
        modelBuilder.Entity<QCKCB>().Property(e => e.TenCoSo).HasMaxLength(100);
        modelBuilder.Entity<QCKCB>().Property(e => e.NoiDung).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<QCKCB>().Property(e => e.Img).HasColumnType("nvarchar(max)");
    }
}

