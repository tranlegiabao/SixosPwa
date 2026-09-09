using Microsoft.EntityFrameworkCore;
using SixosPwa.Models;

namespace SixosPwa.Data;

/// <summary>
/// EF chi con dung de DOC (ADR 0008) — moi duong GHI di qua stored procedure
/// trong <see cref="Services.AdminStoredProcedureService"/>. Vi vay o day khong
/// cau hinh gi phuc vu theo doi thay doi ngoai nhung thu can cho truy van.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<DoiTac> DoiTacs => Set<DoiTac>();
    public DbSet<BenhNhan> BenhNhans => Set<BenhNhan>();
    public DbSet<BenhNhanCoSo> BenhNhanCoSos => Set<BenhNhanCoSo>();
    public DbSet<ThietBi> ThietBis => Set<ThietBi>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<ThongBao> ThongBaos => Set<ThongBao>();
    public DbSet<PushDangKy> PushDangKys => Set<PushDangKy>();
    public DbSet<DotKham> DotKhams => Set<DotKham>();
    public DbSet<DMCSKCB> DMCSKCBs => Set<DMCSKCB>();
    public DbSet<CSKCBGioLamViec> CSKCBGioLamViecs => Set<CSKCBGioLamViec>();
    public DbSet<CSKCBCapQuangCao> CSKCBCapQuangCaos => Set<CSKCBCapQuangCao>();
    public DbSet<NDCSKCB> NDCSKCBs => Set<NDCSKCB>();
    public DbSet<QCKCB> QCKCBs => Set<QCKCB>();
    public DbSet<DMNhomCS> DMNhomCSs => Set<DMNhomCS>();
    public DbSet<DMChuDe> DMChuDes => Set<DMChuDe>();
    public DbSet<DoiTacApi> DoiTacApis => Set<DoiTacApi>();
    public DbSet<TaiKhoanDoiTac> TaiKhoanDoiTacs => Set<TaiKhoanDoiTac>();
    public DbSet<TaiLieuBenhNhan> TaiLieuBenhNhans => Set<TaiLieuBenhNhan>();
    public DbSet<KhoaApiCoSo> KhoaApiCoSos => Set<KhoaApiCoSo>();
    public DbSet<DMGioiTinh> DMGioiTinhs => Set<DMGioiTinh>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ------------------------------------------------------------- DM_GioiTinh
        modelBuilder.Entity<DMGioiTinh>().ToTable("DM_GioiTinh");
        modelBuilder.Entity<DMGioiTinh>().HasKey(e => e.MaGioiTinh);
        modelBuilder.Entity<DMGioiTinh>().Property(e => e.MaGioiTinh).HasMaxLength(10).IsRequired();
        modelBuilder.Entity<DMGioiTinh>().Property(e => e.TenGioiTinh).HasMaxLength(50).IsRequired();

        // ---------------------------------------------------------------- DM_DoiTac
        modelBuilder.Entity<DoiTac>().ToTable("DM_DoiTac");
        modelBuilder.Entity<DoiTac>().HasKey(e => e.Id);
        modelBuilder.Entity<DoiTac>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<DoiTac>().Property(e => e.MaDT).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<DoiTac>().Property(e => e.TenDT).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<DoiTac>().Property(e => e.DiaChi).HasMaxLength(255);
        modelBuilder.Entity<DoiTac>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<DoiTac>().Property(e => e.Email).HasMaxLength(100);
        modelBuilder.Entity<DoiTac>().Property(e => e.IdPm).HasColumnName("IDPM");
        modelBuilder.Entity<DoiTac>().Property(e => e.BrandName).HasMaxLength(100);
        modelBuilder.Entity<DoiTac>().Property(e => e.MatKhauDoiTac).HasMaxLength(255);

        // ------------------------------------------------------------- DM_BenhNhan
        modelBuilder.Entity<BenhNhan>().ToTable("DM_BenhNhan");
        modelBuilder.Entity<BenhNhan>().HasKey(e => e.Id);
        modelBuilder.Entity<BenhNhan>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<BenhNhan>().Property(e => e.CCCD).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<BenhNhan>().Property(e => e.TenBN).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<BenhNhan>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<BenhNhan>().Property(e => e.Email).HasMaxLength(100);
        modelBuilder.Entity<BenhNhan>().Property(e => e.DiaChi).HasMaxLength(255);
        modelBuilder.Entity<BenhNhan>().Property(e => e.IdTaiKhoan).HasColumnName("IDTaiKhoan");
        modelBuilder.Entity<BenhNhan>().Property(e => e.NgaySinh);
        modelBuilder.Entity<BenhNhan>().Property(e => e.HoTenKhongDau).HasMaxLength(100);
        modelBuilder.Entity<BenhNhan>().Property(e => e.GioiTinh).HasMaxLength(10);
        modelBuilder.Entity<BenhNhan>().HasIndex(e => e.CCCD).IsUnique();

        // -------------------------------------------------------- DM_BenhNhanCoSo
        modelBuilder.Entity<BenhNhanCoSo>().ToTable("DM_BenhNhanCoSo");
        modelBuilder.Entity<BenhNhanCoSo>().HasKey(e => e.Id);
        modelBuilder.Entity<BenhNhanCoSo>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<BenhNhanCoSo>().Property(e => e.IdBenhNhan).HasColumnName("IDBenhNhan").IsRequired();
        modelBuilder.Entity<BenhNhanCoSo>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        // KHONG IsRequired nua: ho so tu khai chua co ma co so cap (chot 12).
        modelBuilder.Entity<BenhNhanCoSo>().Property(e => e.MaBN).HasMaxLength(20);
        modelBuilder.Entity<BenhNhanCoSo>().Property(e => e.DaMoTaiLieu).IsRequired();
        modelBuilder.Entity<BenhNhanCoSo>().Property(e => e.NgayXemLichCuoi);
        modelBuilder.Entity<BenhNhanCoSo>().HasIndex(e => new { e.IdCoSo, e.MaBN }).IsUnique();

        // -------------------------------------------------------------- HT_ThietBi
        modelBuilder.Entity<ThietBi>().ToTable("HT_ThietBi");
        modelBuilder.Entity<ThietBi>().HasKey(e => e.Id);
        modelBuilder.Entity<ThietBi>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<ThietBi>().Property(e => e.IdTaiKhoan).HasColumnName("IDTaiKhoan").IsRequired();
        modelBuilder.Entity<ThietBi>().Property(e => e.MaThietBi).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<ThietBi>().Property(e => e.TenThietBi).HasMaxLength(255);

        // ------------------------------------------------------------- HT_TaiKhoan
        modelBuilder.Entity<TaiKhoan>().ToTable("HT_TaiKhoan");
        modelBuilder.Entity<TaiKhoan>().HasKey(e => e.Id);
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<TaiKhoan>().Property(e => e.SDT).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Email).HasMaxLength(50);
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Role).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<TaiKhoan>().Property(e => e.MatKhauNoiBo).HasMaxLength(255);
        modelBuilder.Entity<TaiKhoan>().Property(e => e.IdBenhNhan).HasColumnName("IDBenhNhan");
        modelBuilder.Entity<TaiKhoan>().HasIndex(e => e.SDT).IsUnique();

        // ------------------------------------------------------------ HT_ThongBao
        modelBuilder.Entity<ThongBao>().ToTable("HT_ThongBao");
        modelBuilder.Entity<ThongBao>().HasKey(e => e.Id);
        modelBuilder.Entity<ThongBao>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<ThongBao>().Property(e => e.IdNguoiGui).HasColumnName("IDNguoiGui").IsRequired();
        modelBuilder.Entity<ThongBao>().Property(e => e.IdNguoiNhan).HasColumnName("IDNguoiNhan").IsRequired();
        modelBuilder.Entity<ThongBao>().Property(e => e.NoiDung).HasMaxLength(1000).IsRequired();
        modelBuilder.Entity<ThongBao>().Property(e => e.ThoiGian).IsRequired();
        modelBuilder.Entity<ThongBao>().Property(e => e.DaDoc).IsRequired();

        // ---------------------------------------------------------- HT_PushDangKy
        modelBuilder.Entity<PushDangKy>().ToTable("HT_PushDangKy");
        modelBuilder.Entity<PushDangKy>().HasKey(e => e.Id);
        modelBuilder.Entity<PushDangKy>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<PushDangKy>().Property(e => e.IdTaiKhoan).HasColumnName("IDTaiKhoan").IsRequired();
        modelBuilder.Entity<PushDangKy>().Property(e => e.IdThietBi).HasColumnName("IDThietBi");
        modelBuilder.Entity<PushDangKy>().Property(e => e.Endpoint).HasMaxLength(1000).IsRequired();
        modelBuilder.Entity<PushDangKy>().Property(e => e.P256dh).HasMaxLength(500).IsRequired();
        modelBuilder.Entity<PushDangKy>().Property(e => e.Auth).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<PushDangKy>().Property(e => e.ThoiGian).IsRequired();

        // ------------------------------------------------------------- QL_DotKham
        // Thay cho QL_LichSuKham (bang 4 cot dem, da khai tu o script 06): ba so
        // dem cu suy thang tu bang nay bang MIN/MAX/COUNT.
        modelBuilder.Entity<DotKham>().ToTable("QL_DotKham");
        modelBuilder.Entity<DotKham>().HasKey(e => e.Id);
        modelBuilder.Entity<DotKham>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<DotKham>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.IdBenhNhanCoSo).HasColumnName("IDBenhNhanCoSo").IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.MaVaoVien).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.MaBN).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.NgayGioVao).IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.NgayGioRa);
        modelBuilder.Entity<DotKham>().Property(e => e.TenKhoa).HasMaxLength(255);
        modelBuilder.Entity<DotKham>().Property(e => e.TenBacSi).HasMaxLength(255);
        modelBuilder.Entity<DotKham>().Property(e => e.ChanDoan).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<DotKham>().Property(e => e.NgayTao).IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.NgayCapNhat);
        modelBuilder.Entity<DotKham>().HasIndex(e => new { e.IdCoSo, e.MaVaoVien }).IsUnique();

        // -------------------------------------------------------- HT_KhoaApiCoSo
        modelBuilder.Entity<KhoaApiCoSo>().ToTable("HT_KhoaApiCoSo");
        modelBuilder.Entity<KhoaApiCoSo>().HasKey(e => e.Id);
        modelBuilder.Entity<KhoaApiCoSo>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<KhoaApiCoSo>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<KhoaApiCoSo>().Property(e => e.TenKhoa).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<KhoaApiCoSo>().Property(e => e.KhoaBam).HasColumnType("varbinary(32)").IsRequired();
        modelBuilder.Entity<KhoaApiCoSo>().Property(e => e.Active).IsRequired();
        modelBuilder.Entity<KhoaApiCoSo>().Property(e => e.NgayCap).IsRequired();
        modelBuilder.Entity<KhoaApiCoSo>().Property(e => e.NgayHetHan);
        modelBuilder.Entity<KhoaApiCoSo>().Property(e => e.NgayDungCuoi);
        modelBuilder.Entity<KhoaApiCoSo>().HasIndex(e => e.KhoaBam).IsUnique();

        // --------------------------------------------------------------- DM_CSKCB
        modelBuilder.Entity<DMCSKCB>().ToTable("DM_CSKCB");
        modelBuilder.Entity<DMCSKCB>().HasKey(e => e.Id);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<DMCSKCB>().Property(e => e.MaCoSo).HasMaxLength(10).IsRequired();
        modelBuilder.Entity<DMCSKCB>().Property(e => e.TenCoSo).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Slug).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<DMCSKCB>().Property(e => e.IdNhomCS).HasColumnName("IDNhomCS");
        modelBuilder.Entity<DMCSKCB>().Property(e => e.DiaChi).HasMaxLength(255);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.SoToaNha).HasMaxLength(100);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Email).HasMaxLength(100);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.TenTM).HasMaxLength(100);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Img).HasMaxLength(500);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.QuangCao).HasColumnType("decimal(15,0)");
        modelBuilder.Entity<DMCSKCB>().HasIndex(e => e.MaCoSo).IsUnique();
        modelBuilder.Entity<DMCSKCB>().HasIndex(e => e.Slug).IsUnique();

        // ------------------------------------------------- DM_CSKCB_GioLamViec
        modelBuilder.Entity<CSKCBGioLamViec>().ToTable("DM_CSKCB_GioLamViec");
        modelBuilder.Entity<CSKCBGioLamViec>().HasKey(e => e.Id);
        modelBuilder.Entity<CSKCBGioLamViec>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<CSKCBGioLamViec>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<CSKCBGioLamViec>().Property(e => e.GioMoCua).HasColumnType("time(0)");
        modelBuilder.Entity<CSKCBGioLamViec>().Property(e => e.GioDongCua).HasColumnType("time(0)");
        modelBuilder.Entity<CSKCBGioLamViec>().HasIndex(e => new { e.IdCoSo, e.Thu }).IsUnique();

        // ----------------------------------------------- DM_CSKCB_CapQuangCao
        modelBuilder.Entity<CSKCBCapQuangCao>().ToTable("DM_CSKCB_CapQuangCao");
        modelBuilder.Entity<CSKCBCapQuangCao>().HasKey(e => e.Id);
        modelBuilder.Entity<CSKCBCapQuangCao>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<CSKCBCapQuangCao>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<CSKCBCapQuangCao>().HasIndex(e => new { e.IdCoSo, e.Cap }).IsUnique();

        // --------------------------------------------------- DM_CSKCB_NoiDung
        modelBuilder.Entity<NDCSKCB>().ToTable("DM_CSKCB_NoiDung");
        modelBuilder.Entity<NDCSKCB>().HasKey(e => e.Id);
        modelBuilder.Entity<NDCSKCB>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<NDCSKCB>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<NDCSKCB>().Property(e => e.IdChuDe).HasColumnName("IDChuDe").IsRequired();
        modelBuilder.Entity<NDCSKCB>().HasIndex(e => new { e.IdCoSo, e.IdChuDe }).IsUnique();

        // -------------------------------------------------- DM_CSKCB_QuangCao
        modelBuilder.Entity<QCKCB>().ToTable("DM_CSKCB_QuangCao");
        modelBuilder.Entity<QCKCB>().HasKey(e => e.Id);
        modelBuilder.Entity<QCKCB>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<QCKCB>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<QCKCB>().Property(e => e.NoiDung).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<QCKCB>().Property(e => e.Img).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<QCKCB>().HasIndex(e => e.IdCoSo).IsUnique();

        // ----------------------------------------------------------- DM_DoiTacApi
        modelBuilder.Entity<DoiTacApi>().ToTable("DM_DoiTacApi");
        modelBuilder.Entity<DoiTacApi>().HasKey(e => e.Id);
        modelBuilder.Entity<DoiTacApi>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<DoiTacApi>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<DoiTacApi>().Property(e => e.KieuApi).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<DoiTacApi>().Property(e => e.BaseUrl).HasMaxLength(255);
        modelBuilder.Entity<DoiTacApi>().Property(e => e.TrangChu).HasMaxLength(255);
        modelBuilder.Entity<DoiTacApi>().Property(e => e.KhoaGoiHIS).HasMaxLength(500);
        modelBuilder.Entity<DoiTacApi>().HasIndex(e => e.IdCoSo).IsUnique();

        // ------------------------------------------------------ HT_TaiKhoanDoiTac
        modelBuilder.Entity<TaiKhoanDoiTac>().ToTable("HT_TaiKhoanDoiTac");
        modelBuilder.Entity<TaiKhoanDoiTac>().HasKey(e => e.Id);
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.IdTaiKhoan).HasColumnName("IDTaiKhoan").IsRequired();
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.MatKhau).HasMaxLength(255);
        modelBuilder.Entity<TaiKhoanDoiTac>().Property(e => e.MaXacNhanTam).HasMaxLength(10);
        modelBuilder.Entity<TaiKhoanDoiTac>().HasIndex(e => new { e.IdTaiKhoan, e.IdCoSo }).IsUnique();

        // ---------------------------------------------------- QL_TaiLieuBenhNhan
        modelBuilder.Entity<TaiLieuBenhNhan>().ToTable("QL_TaiLieuBenhNhan");
        modelBuilder.Entity<TaiLieuBenhNhan>().HasKey(e => e.Id);
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.IdBenhNhanCoSo).HasColumnName("IDBenhNhanCoSo");
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.MaBN).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.LoaiTaiLieu).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.TenTaiLieu).HasMaxLength(255).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.DuongDanFtp).HasMaxLength(500).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.DungLuongByte).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.NgayKham);
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.GhiChu).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.MaNguonHIS).HasMaxLength(50);
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.PhienBan).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.LaBanMoiNhat).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.NgayTao).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().HasIndex(e => new { e.IdCoSo, e.MaBN });
    }
}
