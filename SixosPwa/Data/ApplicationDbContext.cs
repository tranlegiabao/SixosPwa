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

    public DbSet<BenhNhan> BenhNhans => Set<BenhNhan>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<ThongBao> ThongBaos => Set<ThongBao>();
    public DbSet<PushDangKy> PushDangKys => Set<PushDangKy>();
    public DbSet<DotKham> DotKhams => Set<DotKham>();
    public DbSet<DMCSKCB> DMCSKCBs => Set<DMCSKCB>();
    public DbSet<CSKCBGioLamViec> CSKCBGioLamViecs => Set<CSKCBGioLamViec>();
    public DbSet<CSKCBCapQuangCao> CSKCBCapQuangCaos => Set<CSKCBCapQuangCao>();
    public DbSet<NDCSKCB> NDCSKCBs => Set<NDCSKCB>();
    public DbSet<DMNhomCS> DMNhomCSs => Set<DMNhomCS>();
    public DbSet<DMChuDe> DMChuDes => Set<DMChuDe>();
    public DbSet<TaiLieuBenhNhan> TaiLieuBenhNhans => Set<TaiLieuBenhNhan>();
    public DbSet<HTConfig> HTConfigs => Set<HTConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ------------------------------------------------------------- DM_BenhNhan
        modelBuilder.Entity<BenhNhan>().ToTable("DM_BenhNhan");
        modelBuilder.Entity<BenhNhan>().HasKey(e => e.Id);
        modelBuilder.Entity<BenhNhan>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<BenhNhan>().Property(e => e.CCCD).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<BenhNhan>().Property(e => e.TenBN).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<BenhNhan>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<BenhNhan>().Property(e => e.Email).HasMaxLength(100);
        modelBuilder.Entity<BenhNhan>().Property(e => e.DiaChi).HasMaxLength(255);
        modelBuilder.Entity<BenhNhan>().Property(e => e.IdCoSo).HasColumnName("IDCoSo");
        modelBuilder.Entity<BenhNhan>().Property(e => e.MaBN).HasMaxLength(20);
        modelBuilder.Entity<BenhNhan>().Property(e => e.DaMoTaiLieu).IsRequired();
        modelBuilder.Entity<BenhNhan>().Property(e => e.NgayXemLichCuoi);
        modelBuilder.Entity<BenhNhan>().Property(e => e.NgaySinh);
        modelBuilder.Entity<BenhNhan>().Property(e => e.HoTenKhongDau).HasMaxLength(100);
        modelBuilder.Entity<BenhNhan>().Property(e => e.GioiTinh).HasMaxLength(10);
        // 🔴 UNIQUE gio la (IDCoSo, CCCD) va (IDCoSo, MaBN), deu LOC IDCoSo IS NOT NULL:
        //    SQL Server coi cac NULL la BANG NHAU trong unique index, thieu ve loc thi
        //    hai dong neo cung CCCD se dam nhau. EF khong dien ta duoc filter nen chi
        //    khai bao de truy van hieu khoa; nguon su that la B02.
        modelBuilder.Entity<BenhNhan>().HasIndex(e => new { e.IdCoSo, e.CCCD });
        modelBuilder.Entity<BenhNhan>().HasIndex(e => new { e.IdCoSo, e.MaBN });

        // ------------------------------------------------------------- HT_TaiKhoan
        modelBuilder.Entity<TaiKhoan>().ToTable("HT_TaiKhoan");
        modelBuilder.Entity<TaiKhoan>().HasKey(e => e.Id);
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<TaiKhoan>().Property(e => e.SDT).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Email).HasMaxLength(50);
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Role).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<TaiKhoan>().Property(e => e.MatKhauNoiBo).HasMaxLength(255);
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
        modelBuilder.Entity<PushDangKy>().Property(e => e.IdBenhNhan).HasColumnName("IDBenhNhan").IsRequired();
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
        modelBuilder.Entity<DotKham>().Property(e => e.IdBenhNhan).HasColumnName("IDBenhNhan").IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.MaVaoVien).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.NgayGioVao).IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.TenKhoa).HasMaxLength(255);
        modelBuilder.Entity<DotKham>().Property(e => e.TenBacSi).HasMaxLength(255);
        modelBuilder.Entity<DotKham>().Property(e => e.NgayTao).IsRequired();
        modelBuilder.Entity<DotKham>().Property(e => e.NgayCapNhat);
        modelBuilder.Entity<DotKham>().HasIndex(e => new { e.IdCoSo, e.MaVaoVien }).IsUnique();

        // --------------------------------------------------------------- DM_CSKCB
        modelBuilder.Entity<DMCSKCB>().ToTable("DM_CSKCB");
        modelBuilder.Entity<DMCSKCB>().HasKey(e => e.Id);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<DMCSKCB>().Property(e => e.MaCoSo).HasMaxLength(10).IsRequired();
        modelBuilder.Entity<DMCSKCB>().Property(e => e.TenCoSo).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Slug).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<DMCSKCB>().Property(e => e.TenCongTy).HasMaxLength(200);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.IdNhomCS).HasColumnName("IDNhomCS");
        modelBuilder.Entity<DMCSKCB>().Property(e => e.DiaChi).HasMaxLength(255);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Email).HasMaxLength(100);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.AnhBia).HasMaxLength(500);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.QcSoTienDaTra).HasColumnType("decimal(15,0)");
        modelBuilder.Entity<DMCSKCB>().Property(e => e.QcNoiDung).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<DMCSKCB>().Property(e => e.QcAnh).HasColumnType("nvarchar(max)");
        // Ket noi sang he HIS cua co so (gop tu DM_DoiTacApi).
        modelBuilder.Entity<DMCSKCB>().Property(e => e.KetNoi_UrlChuyenHuong).HasMaxLength(255);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.KetNoi_BaseUrlHIS).HasMaxLength(255);
        // Kho FTP cua co so (gop tu HT_KhoFtpCoSo, ADR 0030).
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Ftp_Host).HasMaxLength(200);
        modelBuilder.Entity<DMCSKCB>().Property(e => e.Ftp_ThuMucGoc).HasMaxLength(200);
        // 🔴 KhoaBam / Ftp_TaiKhoan / Ftp_MatKhau / KetNoi_KhoaGoiHIS CO Y khong
        //    duoc khai o Models/DMCSKCB.cs — dung them mapping cho chung o day.
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

        // ---------------------------------------------------- QL_TaiLieuBenhNhan
        modelBuilder.Entity<TaiLieuBenhNhan>().ToTable("QL_TaiLieuBenhNhan");
        modelBuilder.Entity<TaiLieuBenhNhan>().HasKey(e => e.Id);
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.IdCoSo).HasColumnName("IDCoSo").IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.IdBenhNhan).HasColumnName("IDBenhNhan");
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.LoaiTaiLieu).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.TenTaiLieu).HasMaxLength(255).IsRequired();
        // 🔴 Ba so DANG LECH, co y giu nguyen 500: cot khai nvarchar(1000) trong DB
        // nhung tham so stored ben HIS chan o 500, va day cung 500. Chua can vi
        // duong dai nhat do duoc la 95 ky tu (135/135 duong URLKySo tren
        // Dev_Master3, 12/09).
        // 🔴 TRAN 2000 NAM O SAU CHO -- doi mot cho ma quen cac cho kia la quay lai
        // dung benh CAT IM LANG ma chot chan sinh ra de chong (benh nhan bam ra 404
        // ma khong ai biet vi sao). Danh sach day du o
        // Database/27_NANG_TRAN_DUONG_DAN_FTP.sql.
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.DuongDanFtp).HasMaxLength(2000).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.NguonKho).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.NgayKham);
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.MaNguonHIS).HasMaxLength(50);
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.PhienBan).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.LaBanMoiNhat).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().Property(e => e.NgayTao).IsRequired();
        modelBuilder.Entity<TaiLieuBenhNhan>().HasIndex(e => e.IdBenhNhan);

        // ------------------------------------------------------------ HT_Config
        modelBuilder.Entity<HTConfig>().ToTable("HT_Config");
        modelBuilder.Entity<HTConfig>().HasKey(e => e.Id);
        modelBuilder.Entity<HTConfig>().Property(e => e.Id).HasColumnName("ID");
        modelBuilder.Entity<HTConfig>().Property(e => e.MaChucNang).HasMaxLength(50);
        modelBuilder.Entity<HTConfig>().Property(e => e.Ghichu).HasMaxLength(500);
        modelBuilder.Entity<HTConfig>().Property(e => e.Ngay).HasColumnType("date");
        modelBuilder.Entity<HTConfig>().Property(e => e.Nhom).HasMaxLength(20);
    }
}
