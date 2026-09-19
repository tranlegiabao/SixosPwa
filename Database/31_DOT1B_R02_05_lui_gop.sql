/* =============================================================================
   DOT 1B - LUI buoc B02..B05 (dung lai DM_BenhNhan cu + DM_BenhNhanCoSo)
   ---------------------------------------------------------------------------
   Bon buoc B02-B05 LIEN KHOA nhau nen lui thanh MOT KHOI, khong lui le duoc:
     - B03 doi ten cot sang IDBenhNhan, tro toi bang B02 dung
     - B05 bo bang cu, muon co lai phai dung tu snapshot B01
   Dieu kien: bak3.*_B01 con nguyen (B01 chua bi don).

   Sau khi chay file nay PHAI chay lai 31_DOT1B_R10_stored.sql de tra 30 stored
   ve ban cu (neu da chay B10).
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('bak3.DM_BenhNhan_B01') IS NULL OR OBJECT_ID('bak3.DM_BenhNhanCoSo_B01') IS NULL
    THROW 50050, 'Khong con snapshot bak3 - KHONG LUI DUOC. DUNG LAI.', 1;
GO

/* --- 1. go FK moi ------------------------------------------------------- */
IF OBJECT_ID('dbo.FK_QL_DotKham_DM_BenhNhan') IS NOT NULL
    ALTER TABLE dbo.QL_DotKham DROP CONSTRAINT FK_QL_DotKham_DM_BenhNhan;
IF OBJECT_ID('dbo.FK_QL_TaiLieuBenhNhan_DM_BenhNhan') IS NOT NULL
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_BenhNhan;
IF OBJECT_ID('dbo.FK_DM_BenhNhan_CoSo') IS NOT NULL
    ALTER TABLE dbo.DM_BenhNhan DROP CONSTRAINT FK_DM_BenhNhan_CoSo;
GO

/* --- 2. tra ten cot ve IDBenhNhanCoSo ----------------------------------- */
IF COL_LENGTH('dbo.QL_DotKham', 'IDBenhNhan') IS NOT NULL
   AND COL_LENGTH('dbo.QL_DotKham', 'IDBenhNhanCoSo') IS NULL
    EXEC sp_rename 'dbo.QL_DotKham.IDBenhNhan', 'IDBenhNhanCoSo', 'COLUMN';
IF COL_LENGTH('dbo.QL_TaiLieuBenhNhan', 'IDBenhNhan') IS NOT NULL
   AND COL_LENGTH('dbo.QL_TaiLieuBenhNhan', 'IDBenhNhanCoSo') IS NULL
    EXEC sp_rename 'dbo.QL_TaiLieuBenhNhan.IDBenhNhan', 'IDBenhNhanCoSo', 'COLUMN';
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QL_TaiLieuBenhNhan_IDBenhNhan'
                                       AND object_id = OBJECT_ID('dbo.QL_TaiLieuBenhNhan'))
    EXEC sp_rename 'dbo.QL_TaiLieuBenhNhan.IX_QL_TaiLieuBenhNhan_IDBenhNhan',
                   'IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo', 'INDEX';
GO

/* --- 3. bo bang gop roi dung lai 2 bang cu ------------------------------ */
IF OBJECT_ID('dbo.DM_BenhNhan')     IS NOT NULL DROP TABLE dbo.DM_BenhNhan;
IF OBJECT_ID('dbo.DM_BenhNhanCoSo') IS NOT NULL DROP TABLE dbo.DM_BenhNhanCoSo;
GO

CREATE TABLE dbo.DM_BenhNhan
(
    ID            bigint        NOT NULL IDENTITY(1,1),
    CCCD          varchar(20)   NOT NULL,
    TenBN         nvarchar(200) NOT NULL,
    SDT           varchar(20)   NULL,
    Email         varchar(100)  NULL,
    DiaChi        nvarchar(255) NULL,
    NgayTao       datetime      NOT NULL CONSTRAINT DF_DM_BenhNhan_NgayTao DEFAULT (GETDATE()),
    NgaySinh      datetime      NULL,
    HoTenKhongDau nvarchar(200) NULL,
    IDTaiKhoan    bigint        NULL,
    GioiTinh      varchar(10)   NULL,
    CONSTRAINT PK_DM_BenhNhan PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT CK_DM_BenhNhan_GioiTinh CHECK
        (GioiTinh IS NULL OR GioiTinh IN ('1','2','3','Nam',N'Nữ','Nu',N'Không xác định','KXD'))
);
GO
CREATE TABLE dbo.DM_BenhNhanCoSo
(
    ID              bigint      NOT NULL IDENTITY(1,1),
    IDBenhNhan      bigint      NOT NULL,
    IDCoSo          bigint      NOT NULL,
    MaBN            varchar(20) NULL,
    NgayTao         datetime    NOT NULL CONSTRAINT DF_DM_BenhNhanCoSo_NgayTao     DEFAULT (GETDATE()),
    DaMoTaiLieu     bit         NOT NULL CONSTRAINT DF_DM_BenhNhanCoSo_DaMoTaiLieu DEFAULT ((0)),
    NgayXemLichCuoi datetime    NULL,
    CONSTRAINT PK_DM_BenhNhanCoSo PRIMARY KEY CLUSTERED (ID)
);
GO

SET IDENTITY_INSERT dbo.DM_BenhNhan ON;
INSERT INTO dbo.DM_BenhNhan
    (ID, CCCD, TenBN, SDT, Email, DiaChi, NgayTao, NgaySinh, HoTenKhongDau, IDTaiKhoan, GioiTinh)
SELECT ID, CCCD, TenBN, SDT, Email, DiaChi, NgayTao, NgaySinh, HoTenKhongDau, IDTaiKhoan, GioiTinh
FROM bak3.DM_BenhNhan_B01;
SET IDENTITY_INSERT dbo.DM_BenhNhan OFF;
GO
SET IDENTITY_INSERT dbo.DM_BenhNhanCoSo ON;
INSERT INTO dbo.DM_BenhNhanCoSo
    (ID, IDBenhNhan, IDCoSo, MaBN, NgayTao, DaMoTaiLieu, NgayXemLichCuoi)
SELECT ID, IDBenhNhan, IDCoSo, MaBN, NgayTao, DaMoTaiLieu, NgayXemLichCuoi
FROM bak3.DM_BenhNhanCoSo_B01;
SET IDENTITY_INSERT dbo.DM_BenhNhanCoSo OFF;
GO

/* --- 4. dung lai khoa / FK cu ------------------------------------------- */
CREATE UNIQUE NONCLUSTERED INDEX UK_DM_BenhNhan_CCCD
    ON dbo.DM_BenhNhan (CCCD)
    WHERE CCCD <> '11111111111' AND CCCD <> '111111111111';
GO
CREATE UNIQUE NONCLUSTERED INDEX UK_DM_BenhNhanCoSo_MaBN
    ON dbo.DM_BenhNhanCoSo (IDCoSo, MaBN)
    WHERE MaBN IS NOT NULL;
GO
ALTER TABLE dbo.DM_BenhNhanCoSo WITH CHECK
    ADD CONSTRAINT FK_DM_BenhNhanCoSo_BenhNhan FOREIGN KEY (IDBenhNhan) REFERENCES dbo.DM_BenhNhan (ID);
ALTER TABLE dbo.DM_BenhNhanCoSo WITH CHECK
    ADD CONSTRAINT FK_DM_BenhNhanCoSo_CoSo     FOREIGN KEY (IDCoSo)     REFERENCES dbo.DM_CSKCB (ID);
GO
ALTER TABLE dbo.QL_DotKham WITH CHECK
    ADD CONSTRAINT FK_QL_DotKham_DM_BenhNhanCoSo
        FOREIGN KEY (IDBenhNhanCoSo) REFERENCES dbo.DM_BenhNhanCoSo (ID);
ALTER TABLE dbo.QL_TaiLieuBenhNhan WITH CHECK
    ADD CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_BenhNhanCoSo
        FOREIGN KEY (IDBenhNhanCoSo) REFERENCES dbo.DM_BenhNhanCoSo (ID);
GO

/* --- 5. reseed ---------------------------------------------------------- */
DECLARE @m1 bigint = (SELECT ISNULL(MAX(ID),0) FROM dbo.DM_BenhNhan);
DECLARE @m2 bigint = (SELECT ISNULL(MAX(ID),0) FROM dbo.DM_BenhNhanCoSo);
DBCC CHECKIDENT ('dbo.DM_BenhNhan',     RESEED, @m1);
DBCC CHECKIDENT ('dbo.DM_BenhNhanCoSo', RESEED, @m2);
GO

/* --- 6. tu kiem --------------------------------------------------------- */
SELECT 'R02-05 lui gop' AS Buoc,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan)     AS BenhNhan_ky_vong_20178,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhanCoSo) AS BenhNhanCoSo_ky_vong_24169,
       (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1) AS FK_khong_tin_phai_0,
       (SELECT COUNT(*) FROM dbo.QL_DotKham t
         WHERE NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo c WHERE c.ID = t.IDBenhNhanCoSo)) AS DotKham_mo_coi_phai_0;
GO
