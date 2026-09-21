/* =============================================================================
   DOT 1B - LUI buoc B06..B09
   ---------------------------------------------------------------------------
   Tra lai: HT_TaiKhoan day du - HT_ThongBao / HT_PushDangKy ban cu -
            DM_DoiTac + cot IDCongTy - bo 2 dong HT_Config moi.

   🔴 PHAI CHAY TRUOC 31_DOT1B_R02_05_lui_gop.sql neu lui ca hai khoi:
   FK moi cua HT_ThongBao/HT_PushDangKy dang tro toi DM_BenhNhan, phai go
   truoc khi dung lai bang do.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('bak3.HT_TaiKhoan_B01') IS NULL OR OBJECT_ID('bak3.DM_DoiTac_B01') IS NULL
    THROW 50100, 'Khong con snapshot bak3 - KHONG LUI DUOC. DUNG LAI.', 1;
GO

/* --- 1. HT_ThongBao / HT_PushDangKy: go FK moi -------------------------- */
IF OBJECT_ID('dbo.FK_HT_ThongBao_NguoiGui')  IS NOT NULL
    ALTER TABLE dbo.HT_ThongBao DROP CONSTRAINT FK_HT_ThongBao_NguoiGui;
IF OBJECT_ID('dbo.FK_HT_ThongBao_NguoiNhan') IS NOT NULL
    ALTER TABLE dbo.HT_ThongBao DROP CONSTRAINT FK_HT_ThongBao_NguoiNhan;
IF OBJECT_ID('dbo.FK_HT_PushDangKy_BenhNhan') IS NOT NULL
    ALTER TABLE dbo.HT_PushDangKy DROP CONSTRAINT FK_HT_PushDangKy_BenhNhan;
GO

/* --- 2. DM_DoiTac + cot IDCongTy --------------------------------------- */
IF OBJECT_ID('dbo.DM_DoiTac') IS NOT NULL DROP TABLE dbo.DM_DoiTac;
GO
CREATE TABLE dbo.DM_DoiTac
(
    ID            bigint        NOT NULL IDENTITY(1,1),
    MaDT          varchar(20)   NOT NULL,
    TenDT         nvarchar(200) NOT NULL,
    DiaChi        nvarchar(255) NULL,
    SDT           varchar(20)   NULL,
    Email         varchar(100)  NULL,
    BrandName     nvarchar(200) NULL,
    MatKhauDoiTac nvarchar(255) NULL,
    NgayTao       datetime      NOT NULL CONSTRAINT DF_DM_DoiTac_NgayTao DEFAULT (GETDATE()),
    CONSTRAINT PK_DM_DoiTac PRIMARY KEY CLUSTERED (ID)
);
GO
SET IDENTITY_INSERT dbo.DM_DoiTac ON;
INSERT INTO dbo.DM_DoiTac (ID, MaDT, TenDT, DiaChi, SDT, Email, BrandName, MatKhauDoiTac, NgayTao)
SELECT ID, MaDT, TenDT, DiaChi, SDT, Email, BrandName, MatKhauDoiTac, NgayTao FROM bak3.DM_DoiTac_B01;
SET IDENTITY_INSERT dbo.DM_DoiTac OFF;
GO
CREATE UNIQUE NONCLUSTERED INDEX UK_DM_DoiTac_MaDT ON dbo.DM_DoiTac (MaDT);
GO
DECLARE @m bigint = (SELECT ISNULL(MAX(ID),0) FROM dbo.DM_DoiTac);
DBCC CHECKIDENT ('dbo.DM_DoiTac', RESEED, @m);
GO
IF COL_LENGTH('dbo.DM_CSKCB', 'IDCongTy') IS NULL
    ALTER TABLE dbo.DM_CSKCB ADD IDCongTy bigint NULL;
GO
UPDATE k SET k.IDCongTy = s.IDCongTy
FROM dbo.DM_CSKCB k JOIN bak3.DM_CSKCB_CongTy_B01 s ON s.ID = k.ID;
GO
ALTER TABLE dbo.DM_CSKCB WITH CHECK
    ADD CONSTRAINT FK_DM_CSKCB_CongTy FOREIGN KEY (IDCongTy) REFERENCES dbo.DM_DoiTac (ID);
GO
IF COL_LENGTH('dbo.DM_CSKCB', 'TenCongTy') IS NOT NULL
    ALTER TABLE dbo.DM_CSKCB DROP COLUMN TenCongTy;
GO

/* --- 3. HT_TaiKhoan day du --------------------------------------------- */
IF OBJECT_ID('dbo.CK_HT_TaiKhoan_Role') IS NOT NULL
    ALTER TABLE dbo.HT_TaiKhoan DROP CONSTRAINT CK_HT_TaiKhoan_Role;
GO
DELETE FROM dbo.HT_TaiKhoan;
GO
SET IDENTITY_INSERT dbo.HT_TaiKhoan ON;
INSERT INTO dbo.HT_TaiKhoan (ID, SDT, Email, Role, MatKhauNoiBo, NgayTao)
SELECT ID, SDT, Email, Role, MatKhauNoiBo, NgayTao FROM bak3.HT_TaiKhoan_B01;
SET IDENTITY_INSERT dbo.HT_TaiKhoan OFF;
GO
DECLARE @mt bigint = (SELECT ISNULL(MAX(ID),0) FROM dbo.HT_TaiKhoan);
DBCC CHECKIDENT ('dbo.HT_TaiKhoan', RESEED, @mt);
GO
ALTER TABLE dbo.HT_TaiKhoan WITH CHECK
    ADD CONSTRAINT CK_HT_TaiKhoan_Role CHECK (Role IN ('Admin','BenhNhan','DoiTac'));
GO

/* --- 4. HT_ThongBao / HT_PushDangKy ban cu ----------------------------- */
IF OBJECT_ID('dbo.HT_ThongBao') IS NOT NULL DROP TABLE dbo.HT_ThongBao;
GO
CREATE TABLE dbo.HT_ThongBao
(
    ID          bigint         NOT NULL IDENTITY(1,1),
    IDNguoiGui  bigint         NOT NULL,
    IDNguoiNhan bigint         NOT NULL,
    NoiDung     nvarchar(2000) NOT NULL,
    ThoiGian    datetime       NOT NULL CONSTRAINT DF_HT_ThongBao_ThoiGian DEFAULT (GETDATE()),
    DaDoc       bit            NOT NULL CONSTRAINT DF_HT_ThongBao_DaDoc    DEFAULT ((0)),
    CONSTRAINT PK_HT_ThongBao PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT CK_HT_ThongBao_KhacNhau CHECK (IDNguoiGui <> IDNguoiNhan)
);
GO
SET IDENTITY_INSERT dbo.HT_ThongBao ON;
INSERT INTO dbo.HT_ThongBao (ID, IDNguoiGui, IDNguoiNhan, NoiDung, ThoiGian, DaDoc)
SELECT ID, IDNguoiGui, IDNguoiNhan, NoiDung, ThoiGian, DaDoc FROM bak3.HT_ThongBao_B01;
SET IDENTITY_INSERT dbo.HT_ThongBao OFF;
GO
DECLARE @mb bigint = (SELECT ISNULL(MAX(ID),0) FROM dbo.HT_ThongBao);
DBCC CHECKIDENT ('dbo.HT_ThongBao', RESEED, @mb);
GO
ALTER TABLE dbo.HT_ThongBao WITH CHECK
    ADD CONSTRAINT FK_HT_ThongBao_NguoiGui  FOREIGN KEY (IDNguoiGui)  REFERENCES dbo.HT_TaiKhoan (ID);
ALTER TABLE dbo.HT_ThongBao WITH CHECK
    ADD CONSTRAINT FK_HT_ThongBao_NguoiNhan FOREIGN KEY (IDNguoiNhan) REFERENCES dbo.HT_TaiKhoan (ID);
GO

IF COL_LENGTH('dbo.HT_PushDangKy', 'IDBenhNhan') IS NOT NULL
   AND COL_LENGTH('dbo.HT_PushDangKy', 'IDTaiKhoan') IS NULL
    EXEC sp_rename 'dbo.HT_PushDangKy.IDBenhNhan', 'IDTaiKhoan', 'COLUMN';
GO
DELETE FROM dbo.HT_PushDangKy;
GO
SET IDENTITY_INSERT dbo.HT_PushDangKy ON;
INSERT INTO dbo.HT_PushDangKy (ID, IDTaiKhoan, Endpoint, P256dh, Auth, ThoiGian)
SELECT ID, IDTaiKhoan, Endpoint, P256dh, Auth, ThoiGian FROM bak3.HT_PushDangKy_B01;
SET IDENTITY_INSERT dbo.HT_PushDangKy OFF;
GO
DECLARE @mp bigint = (SELECT ISNULL(MAX(ID),0) FROM dbo.HT_PushDangKy);
DBCC CHECKIDENT ('dbo.HT_PushDangKy', RESEED, @mp);
GO
ALTER TABLE dbo.HT_PushDangKy WITH CHECK
    ADD CONSTRAINT FK_HT_PushDangKy_TaiKhoan FOREIGN KEY (IDTaiKhoan) REFERENCES dbo.HT_TaiKhoan (ID);
GO

/* --- 5. bo 2 dong HT_Config moi ---------------------------------------- */
DELETE FROM dbo.HT_Config WHERE MaChucNang IN ('MOT_HO_SO', 'LOG_DON_LAN_CUOI');
GO

/* --- 6. tu kiem --------------------------------------------------------- */
SELECT 'R06-09 lui' AS Buoc,
       (SELECT COUNT(*) FROM dbo.HT_TaiKhoan)    AS TaiKhoan_ky_vong_13154,
       (SELECT COUNT(*) FROM dbo.DM_DoiTac)      AS DoiTac_ky_vong_2,
       (SELECT COUNT(*) FROM dbo.HT_ThongBao)    AS ThongBao_ky_vong_112,
       (SELECT COUNT(*) FROM dbo.HT_PushDangKy)  AS Push_ky_vong_29,
       (SELECT COUNT(*) FROM dbo.HT_Config)      AS Config_ky_vong_1,
       (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1) AS FK_khong_tin_phai_0;
GO
