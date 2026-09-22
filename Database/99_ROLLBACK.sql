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

-- --- Nguoc 29 (MAX(PhienBan) trong pham vi ban moi nhat) ---------------------
-- 🔴 CHAY CAI NAY LA TRA LAI BENH QUET TOAN BANG cua duong GHI. O bang 593k
-- dong do duoc: 28.625 logical reads/luot, ke hoach song song, khoa U tren gan
-- nhu moi trang => 85 deadlock/40 phut va 746/800 luot ghi HONG (mat tai lieu,
-- im lang vi thu tuc nuot loi trong CATCH). Chi chay khi that su can quay ve
-- ban truoc va. Ly le o docs/adr/0034-phien-ban-lay-max-trong-pham-vi-ban-moi-nhat.md.
-- Duoi day la NGUYEN VAN thu tuc TRUOC file 29 (tuc ban sau file 26).
GO
SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO
-- ---------------------------------------------------------------------------
-- Thu tuc luu — VIET DE, GIU NGUYEN chu ky cu roi THEM tham so tuy chon.
-- Code C# hien tai cua khu tai lieu goi khong co @MaNguonHIS van chay duoc.
--
-- Luat TU CHOI duoc chan ngay o day chu khong chi o C#: @IDBenhNhanCoSo NULL
-- hay khong thuoc co so => ResultCode 5. Nhu vay chot 3 van dung ke ca khi
-- tang C# chua kip va.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.QL_TaiLieuBenhNhan_Save
    @ID             BIGINT,
    @IDCoSo         BIGINT,
    @IDBenhNhanCoSo BIGINT = NULL,
    @MaBN           NVARCHAR(50),
    @LoaiTaiLieu    NVARCHAR(50),
    @TenTaiLieu     NVARCHAR(255),
    -- Tran 2000 (file 27). 🔴 Con so nay nam o SAU cho -- doi mot cho ma quen
    -- cac cho kia la quay lai dung benh CAT IM LANG ma chot chan sinh ra de chong.
    @DuongDanFtp    NVARCHAR(2000),
    @DungLuongByte  BIGINT,
    @NgayKham       DATETIME = NULL,
    @GhiChu         NVARCHAR(MAX) = NULL,
    @MaNguonHIS     VARCHAR(50) = NULL,
    @BamNoiDung     CHAR(64) = NULL,
    /* THEM 26: kho chua tep. Mac dinh N'CONG' => moi cho goi cu (C# cua cong)
       giu nguyen hanh vi, khong phai sua mot dong C# nao. Chi che do Tro duong
       ben HIS truyen N'COSO'. */
    @NguonKho       NVARCHAR(20) = N'CONG',
    @IDTaiLieu      BIGINT OUTPUT,
    @ResultCode     INT OUTPUT,
    @ResultMessage  NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDTaiLieu = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @IDCoSo)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Cơ sở khám chữa bệnh không tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- Chot 3: khong co ho so noi thi TU CHOI, khong luu.
        IF @IDBenhNhanCoSo IS NULL
           OR NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                          WHERE ID = @IDBenhNhanCoSo AND IDCoSo = @IDCoSo)
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Mã bệnh nhân này chưa có hồ sơ nào nhận tại cổng.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @ID = 0
        BEGIN
            DECLARE @PhienBan int = 1;

            IF @MaNguonHIS IS NOT NULL
            BEGIN
                -- Cung nguon => day them mot PHIEN BAN, ha co ban moi nhat cua
                -- cac ban truoc. Chan trung tuyet doi (day lai y het noi dung)
                -- la viec cua tang C#, xem NhanTaiLieu.
                SELECT @PhienBan = ISNULL(MAX(PhienBan), 0) + 1
                FROM dbo.QL_TaiLieuBenhNhan WITH (UPDLOCK, HOLDLOCK)
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu AND MaNguonHIS = @MaNguonHIS;

                UPDATE dbo.QL_TaiLieuBenhNhan
                SET LaBanMoiNhat = 0
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu
                  AND MaNguonHIS = @MaNguonHIS AND LaBanMoiNhat = 1;
            END;

            INSERT INTO dbo.QL_TaiLieuBenhNhan (
                IDCoSo, IDBenhNhanCoSo, MaBN, LoaiTaiLieu, TenTaiLieu, DuongDanFtp,
                DungLuongByte, NgayKham, GhiChu, MaNguonHIS, BamNoiDung, NguonKho,
                PhienBan, LaBanMoiNhat, NgayTao)
            VALUES (
                @IDCoSo, @IDBenhNhanCoSo, @MaBN, @LoaiTaiLieu, @TenTaiLieu, @DuongDanFtp,
                @DungLuongByte, @NgayKham, @GhiChu, @MaNguonHIS, @BamNoiDung,
                ISNULL(NULLIF(LTRIM(RTRIM(@NguonKho)), N''), N'CONG'),
                @PhienBan, 1, GETDATE());

            SET @IDTaiLieu = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.QL_TaiLieuBenhNhan WHERE ID = @ID)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Tài liệu không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE dbo.QL_TaiLieuBenhNhan
            SET IDCoSo = @IDCoSo, IDBenhNhanCoSo = @IDBenhNhanCoSo, MaBN = @MaBN,
                LoaiTaiLieu = @LoaiTaiLieu, TenTaiLieu = @TenTaiLieu,
                DuongDanFtp = @DuongDanFtp, DungLuongByte = @DungLuongByte,
                NgayKham = @NgayKham, GhiChu = @GhiChu
            WHERE ID = @ID;

            SET @IDTaiLieu = @ID;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH
END;

GO

-- --- Nguoc 21 (cot NguonKho — che do Tro duong, ADR 0030) -------------------
-- 🔴 Chay cai nay LA MAT dau vet tai lieu nao nam o kho co so. Sau khi bo cot,
-- moi dong deu bi doc nhu o kho cong => 404 im lang. Chi chay khi that su go
-- han che do Tro duong.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_QL_TaiLieuBenhNhan_NguonKho')
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP CONSTRAINT CK_QL_TaiLieuBenhNhan_NguonKho;
GO
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_QL_TaiLieuBenhNhan_NguonKho')
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP CONSTRAINT DF_QL_TaiLieuBenhNhan_NguonKho;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND name = 'NguonKho')
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP COLUMN NguonKho;
GO

-- --- Nguoc 20 (HT_KhoFtpCoSo) -----------------------------------------------
-- Mat khau FTP luu tho nam trong bang nay, xoa la mat — phai hoi lai tung co so.
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'HT_KhoFtpCoSo_GhiNhanThuDat' AND type = 'P')
    DROP PROCEDURE dbo.HT_KhoFtpCoSo_GhiNhanThuDat;
GO
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'HT_KhoFtpCoSo_Save' AND type = 'P')
    DROP PROCEDURE dbo.HT_KhoFtpCoSo_Save;
GO
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HT_KhoFtpCoSo')
    DROP TABLE dbo.HT_KhoFtpCoSo;
GO

-- --- Nguoc 14 (Seed 20 tai khoan test / benh nhan that Dev_Master3) --------
-- Xoa sach cac tai khoan test trich xuat tu Dev_Master3 kem ho so con nguoi
-- va ho so tai co so tuong ung.
DECLARE @SdtTest14 TABLE (SDT varchar(20));
INSERT INTO @SdtTest14 (SDT)
VALUES
-- 20 benh nhan that tu Dev_Master3:
('0348932730'),('0773746879'),('0346960066'),('0987092200'),('0896677433'),
('0962700638'),('0915196167'),('0932243382'),('0357188959'),('0915631258'),
('0969359948'),('0933270921'),('0933432722'),('0798637535'),('0977436455'),
('0707227512'),('0793062239'),('0704450259'),('0342286775'),('0973754877'),
-- Cac SDT dummy test cu (neu co):
('079075005678'),
('0900000001'),('0900000002'),('0900000003'),('0900000004'),('0900000005'),
('0900000006'),('0900000007'),('0900000008'),('0900000009'),('0900000010'),
('0900000011'),('0900000012'),('0900000013'),('0900000014'),('0900000015'),
('0900000016'),('0900000017'),('0900000018'),('0900000019'),('0900000020');

-- 1. Xoa thiet bi dang nhap (HT_ThietBi)
DELETE tb
  FROM dbo.HT_ThietBi tb
  JOIN dbo.HT_TaiKhoan tk ON tk.Id = tb.IDTaiKhoan
 WHERE tk.SDT IN (SELECT SDT FROM @SdtTest14);

-- 2. Go khoa ngoai vong: HT_TaiKhoan.IdBenhNhan tro toi DM_BenhNhan
UPDATE dbo.HT_TaiKhoan
   SET IdBenhNhan = NULL
 WHERE SDT IN (SELECT SDT FROM @SdtTest14);

-- 3. Xoa ho so tai co so (DM_BenhNhanCoSo)
DELETE cs
  FROM dbo.DM_BenhNhanCoSo cs
  JOIN dbo.DM_BenhNhan bn ON bn.ID = cs.IDBenhNhan
  JOIN dbo.HT_TaiKhoan tk ON tk.Id = bn.IDTaiKhoan
 WHERE tk.SDT IN (SELECT SDT FROM @SdtTest14);

-- 4. Xoa con nguoi (DM_BenhNhan)
DELETE bn
  FROM dbo.DM_BenhNhan bn
  JOIN dbo.HT_TaiKhoan tk ON tk.Id = bn.IDTaiKhoan
 WHERE tk.SDT IN (SELECT SDT FROM @SdtTest14);

-- 5. Xoa tai khoan (HT_TaiKhoan)
DELETE FROM dbo.HT_TaiKhoan
 WHERE SDT IN (SELECT SDT FROM @SdtTest14);

PRINT N'Da rollback/xoa sach 20 tai khoan test 14.';
GO

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
