-- ============================================================================
-- 99 — ROLLBACK doi xung cho 01..06. Chay NGUOC thu tu: 06 -> 01.
--
-- KHONG khoi phuc duoc mot cach tuyet doi:
--   * Tep PDF cua cac dong mo coi da xoa o buoc 04 van con tren FTP (script
--     khong xoa tep), duong dan nam trong bak.QL_TaiLieuBenhNhan_MoCoi_V001.
--   * Khoa API tho da bi bo khoi DM_CSKCB o buoc 01. Bang bam KHONG doi nguoc
--     ra khoa tho duoc — muon quay lai phai CAP KHOA MOI cho 3 co so do.
--     Doan duoi chi tra lai COT rong.
-- ============================================================================

-- --- Nguoc 06 ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QL_LichSuKham')
   AND EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = 'bak' AND t.name = 'LichSuKham_V001')
BEGIN
    CREATE TABLE dbo.QL_LichSuKham (
        ID              bigint IDENTITY(1,1) NOT NULL,
        IDBenhNhanCoSo  bigint   NOT NULL,
        NgayKhamDau     datetime NOT NULL,
        NgayKhamGanNhat datetime NOT NULL,
        SoLanKham       int      NOT NULL,
        TrangThai       nvarchar(50) NULL,
        CONSTRAINT PK_QL_LichSuKham PRIMARY KEY (ID)
    );

    SET IDENTITY_INSERT dbo.QL_LichSuKham ON;
    INSERT INTO dbo.QL_LichSuKham (ID, IDBenhNhanCoSo, NgayKhamDau, NgayKhamGanNhat, SoLanKham, TrangThai)
    SELECT ID, IDBenhNhanCoSo, NgayKhamDau, NgayKhamGanNhat, SoLanKham, TrangThai FROM bak.LichSuKham_V001;
    SET IDENTITY_INSERT dbo.QL_LichSuKham OFF;
END;
GO

-- --- Nguoc 05 ---------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_MaBN'
           AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo'))
    DROP INDEX UK_DM_BenhNhanCoSo_MaBN ON dbo.DM_BenhNhanCoSo;
GO
-- Chi siet lai NOT NULL duoc khi khong con dong nao MaBN rong.
IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo WHERE MaBN IS NULL)
BEGIN
    ALTER TABLE dbo.DM_BenhNhanCoSo ALTER COLUMN MaBN varchar(20) NOT NULL;
    CREATE UNIQUE NONCLUSTERED INDEX UK_DM_BenhNhanCoSo_MaBN ON dbo.DM_BenhNhanCoSo (IDCoSo, MaBN);
    CREATE UNIQUE NONCLUSTERED INDEX UK_DM_BenhNhanCoSo_HoSo ON dbo.DM_BenhNhanCoSo (IDBenhNhan, IDCoSo);
END;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'DaMoTaiLieu')
BEGIN
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP CONSTRAINT DF_DM_BenhNhanCoSo_DaMoTaiLieu;
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP COLUMN DaMoTaiLieu;
END;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'NgaySinh_ChupTuHIS')
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP COLUMN NgaySinh_ChupTuHIS;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'TenBN_ChupTuHIS')
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP COLUMN TenBN_ChupTuHIS;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'IDTaiKhoan')
    ALTER TABLE dbo.DM_BenhNhan DROP COLUMN IDTaiKhoan;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'HoTenKhongDau')
    ALTER TABLE dbo.DM_BenhNhan DROP COLUMN HoTenKhongDau;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'NgaySinh')
    ALTER TABLE dbo.DM_BenhNhan DROP COLUMN NgaySinh;
GO

-- --- Nguoc 04 ---------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_QL_TaiLieuBenhNhan_Nguon')
    DROP INDEX UK_QL_TaiLieuBenhNhan_Nguon ON dbo.QL_TaiLieuBenhNhan;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND name = 'LaBanMoiNhat')
BEGIN
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP CONSTRAINT DF_QL_TaiLieuBenhNhan_LaBanMoiNhat;
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP COLUMN LaBanMoiNhat;
END;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND name = 'PhienBan')
BEGIN
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP CONSTRAINT DF_QL_TaiLieuBenhNhan_PhienBan;
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP COLUMN PhienBan;
END;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND name = 'MaNguonHIS')
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP COLUMN MaNguonHIS;
GO
-- Cung cai bay nhu o 04, chieu nguoc: phai go index bam vao cot truoc khi doi.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo'
           AND object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan'))
    DROP INDEX IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo ON dbo.QL_TaiLieuBenhNhan;
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN'
           AND object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan'))
    DROP INDEX IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN ON dbo.QL_TaiLieuBenhNhan;
GO
ALTER TABLE dbo.QL_TaiLieuBenhNhan ALTER COLUMN MaBN nvarchar(50) NOT NULL;
GO
ALTER TABLE dbo.QL_TaiLieuBenhNhan ALTER COLUMN IDBenhNhanCoSo bigint NULL;
GO
-- Dung lai DUNG hinh dang goc trong script cua dong nghiep, ke ca bo loc.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN'
               AND object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan'))
    CREATE NONCLUSTERED INDEX IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN
        ON dbo.QL_TaiLieuBenhNhan (IDCoSo, MaBN);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo'
               AND object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan'))
    CREATE NONCLUSTERED INDEX IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo
        ON dbo.QL_TaiLieuBenhNhan (IDBenhNhanCoSo)
        WHERE IDBenhNhanCoSo IS NOT NULL;
GO
-- Tra lai cac dong mo coi da don o buoc 04.
IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
           WHERE s.name = 'bak' AND t.name = 'QL_TaiLieuBenhNhan_MoCoi_V001')
BEGIN
    INSERT INTO dbo.QL_TaiLieuBenhNhan
        (IDCoSo, IDBenhNhanCoSo, MaBN, LoaiTaiLieu, TenTaiLieu, DuongDanFtp, DungLuongByte, NgayKham, GhiChu, NgayTao)
    SELECT IDCoSo, IDBenhNhanCoSo, MaBN, LoaiTaiLieu, TenTaiLieu, DuongDanFtp, DungLuongByte, NgayKham, GhiChu, NgayTao
    FROM bak.QL_TaiLieuBenhNhan_MoCoi_V001 b
    WHERE NOT EXISTS (SELECT 1 FROM dbo.QL_TaiLieuBenhNhan d WHERE d.DuongDanFtp = b.DuongDanFtp);
END;
GO

-- --- Nguoc 03 ---------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'QL_DotKham_Save' AND type = 'P')
    DROP PROCEDURE dbo.QL_DotKham_Save;
GO
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QL_DotKham')
    DROP TABLE dbo.QL_DotKham;
GO

-- --- Nguoc 02 ---------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'HT_LogApiCoSo_Ghi' AND type = 'P')
    DROP PROCEDURE dbo.HT_LogApiCoSo_Ghi;
GO
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HT_LogApiCoSo')
    DROP TABLE dbo.HT_LogApiCoSo;
GO

-- --- Nguoc 01 ---------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'HT_KhoaApiCoSo_Save' AND type = 'P')
    DROP PROCEDURE dbo.HT_KhoaApiCoSo_Save;
GO
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HT_KhoaApiCoSo')
    DROP TABLE dbo.HT_KhoaApiCoSo;
GO
-- Tra lai cot rong. Khoa tho da mat — phai cap lai cho 3 co so.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_CSKCB') AND name = 'ApiKey')
    ALTER TABLE dbo.DM_CSKCB ADD ApiKey nvarchar(100) NULL;
GO
