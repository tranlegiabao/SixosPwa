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
        modelBuilder.Entity<ThietBi>().Property(e => e.MaBN).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<ThietBi>().Property(e => e.SDT).HasMaxLength(20);
        modelBuilder.Entity<ThietBi>().Property(e => e.IdThietBi).HasMaxLength(100);

        // Configure TaiKhoan
        modelBuilder.Entity<TaiKhoan>().ToTable("TaiKhoan");
        modelBuilder.Entity<TaiKhoan>().HasKey(e => e.Id);
        modelBuilder.Entity<TaiKhoan>().Property(e => e.SDT).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<TaiKhoan>().Property(e => e.Role).HasMaxLength(50).IsRequired();
    }
}