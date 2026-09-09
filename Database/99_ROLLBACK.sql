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

-- --- Nguoc 13 (Dot 4) -------------------------------------------------------
-- KHONG khoi phuc duoc mot cach tuyet doi: tai lieu / dot kham da bi *Go noi*
-- xoa thi nam o bak.GoNoi_*_V001 nhung KHONG do nguoc ve duoc, vi dong
-- DM_BenhNhanCoSo cu da mat ID (IDENTITY khong cap lai so cu). Muon lui thi de
-- HIS day lai — khoa tu nhien (IDCoSo, LoaiTaiLieu, MaNguonHIS) con nguyen ben do.

IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'DM_BenhNhanCoSo_DoiMocXemLich' AND type = 'P')
    DROP PROCEDURE dbo.DM_BenhNhanCoSo_DoiMocXemLich;
GO
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'DM_BenhNhanCoSo_GoNoi' AND type = 'P')
    DROP PROCEDURE dbo.DM_BenhNhanCoSo_GoNoi;
GO
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'DM_BenhNhan_SuaHoSo' AND type = 'P')
    DROP PROCEDURE dbo.DM_BenhNhan_SuaHoSo;
GO
-- DM_BenhNhan_Save tra ve ban KHONG co @GioiTinh: chay lai script 09.
--     Database/09_MOT_TAI_KHOAN_NHIEU_HO_SO.sql  (muc (2))
-- Phai chay TRUOC khi bo cot GioiTinh ben duoi, khong thi thu tuc con tro toi
-- mot cot khong ton tai.

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'NgayXemLichCuoi')
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP COLUMN NgayXemLichCuoi;
GO
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'GioiTinh')
    ALTER TABLE dbo.DM_BenhNhan DROP COLUMN GioiTinh;
GO
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.DM_DoiTacApi') AND name = 'KhoaGoiHIS')
    ALTER TABLE dbo.DM_DoiTacApi DROP COLUMN KhoaGoiHIS;
GO
-- Ban sao luu cua *Go noi* GIU LAI co y — xoa di la mat luon dau vet nhung gi
-- da bi thao. Muon don han:
--     DROP TABLE bak.GoNoi_TaiLieu_V001, bak.GoNoi_DotKham_V001, bak.GoNoi_HoSoCoSo_V001;

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

-- --- Nguoc 09 ---------------------------------------------------------------
-- KHONG khoi phuc duoc: ma tu bia BN-yyyyMMdd-#### da bi don ve NULL o buoc (5)
-- cua script 09. Chung von la ma CONG tu bia chu khong phai ma co so cap, nen
-- mat cung khong mat thong tin that — nhung khong sinh lai duoc dung chuoi cu.
-- Muon lui hoan toan thi phuc hoi tu ban sao luu CSDL.

IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'DM_BenhNhan_XoaHoSo' AND type = 'P')
    DROP PROCEDURE dbo.DM_BenhNhan_XoaHoSo;
GO
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'DM_BenhNhan_NhanChuSoHuu' AND type = 'P')
    DROP PROCEDURE dbo.DM_BenhNhan_NhanChuSoHuu;
GO
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'DM_BenhNhanCoSo_TaoTuKhai' AND type = 'P')
    DROP PROCEDURE dbo.DM_BenhNhanCoSo_TaoTuKhai;
GO
-- DM_BenhNhan_Save tra ve ban khong co @IDTaiKhoan/@NgaySinh/@HoTenKhongDau.
-- Lay lai nguyen ban tu catalog:
--   Projects/Databases/118.69.34.247,8392/HIS_CSKH/Stored/dbo.DM_BenhNhan_Save.sql
-- Cot IDTaiKhoan duoc bo o doan "Nguoc 05" ben duoi, keo theo ca du lieu da do.

-- --- Nguoc 08 ---------------------------------------------------------------
-- Tham so @DaMoTaiLieu cua DM_BenhNhanCoSo_Save la TUY CHON va mac dinh 1, nen
-- de nguyen cung khong hai gi khi lui: moi cho goi deu dung ten tham so, khong
-- dua theo thu tu. Muon sach tuyet doi thi lay lai ban cu bang:
--     git show 32dbb19 -- Database/  (ban truoc dot 2)
-- Cot DaMoTaiLieu duoc bo o doan "Nguoc 05" ben duoi, keo theo ca doan UPDATE.

-- --- Nguoc 05 ---------------------------------------------------------------
-- Sau khi 05 chay xong thi day la INDEX CO LOC => DROP INDEX. Nhung neu 05 moi
-- chay dở thi no van con la UNIQUE CONSTRAINT => phai DROP CONSTRAINT. Do ca
-- hai truong hop, dung doan.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_MaBN'
           AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND is_unique_constraint = 1)
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP CONSTRAINT UK_DM_BenhNhanCoSo_MaBN;
ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_MaBN'
                AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo'))
    DROP INDEX UK_DM_BenhNhanCoSo_MaBN ON dbo.DM_BenhNhanCoSo;
GO
-- Chi siet lai NOT NULL duoc khi khong con dong nao MaBN rong.
-- Dung lai dung hinh dang GOC: hai cai nay von la UNIQUE CONSTRAINT chu khong
-- phai index thuong, nen tra ve bang ADD CONSTRAINT. Dung lai bang
-- CREATE UNIQUE INDEX se ra mot thu khac ten giong, va lan sau ai do go no se
-- lai vap dung Msg 3723 nhu lan nay.
IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo WHERE MaBN IS NULL)
BEGIN
    ALTER TABLE dbo.DM_BenhNhanCoSo ALTER COLUMN MaBN varchar(20) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_MaBN'
                   AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo'))
        ALTER TABLE dbo.DM_BenhNhanCoSo
            ADD CONSTRAINT UK_DM_BenhNhanCoSo_MaBN UNIQUE (IDCoSo, MaBN);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_HoSo'
                   AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo'))
        ALTER TABLE dbo.DM_BenhNhanCoSo
            ADD CONSTRAINT UK_DM_BenhNhanCoSo_HoSo UNIQUE (IDBenhNhan, IDCoSo);
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
