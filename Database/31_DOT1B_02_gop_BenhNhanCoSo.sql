/* =============================================================================
   DOT 1B - buoc B02: dung lai dbo.DM_BenhNhan (gop DM_BenhNhanCoSo vao)
   ---------------------------------------------------------------------------
   KEEP-ID: khoa chinh moi = DM_BenhNhanCoSo.ID CU (giu nguyen gia tri)
     -> QL_TaiLieuBenhNhan (614.636) va QL_DotKham (84.153) KHONG phai UPDATE
        dong nao; B03 chi sp_rename COT + dung lai FK.
     -> An toan vi sau dot A khong con cot nao trong CSDL tro DM_BenhNhan.ID
        ngoai DM_BenhNhanCoSo.IDBenhNhan sap bo.

   RIENG BUOC NAY KHONG DUNG sp_rename DE DOI CHO BANG CU (bai hoc dot 2, V003):
   sp_rename KHONG doi ten rang buoc -> bang cu doi ten van giu ten PK cu ->
   tao bang moi cung ten rang buoc -> Msg 2714 -> bang khong duoc tao.
   => Cach lam: bang moi mang ten rang buoc TAM (_Moi), copy du lieu,
      DROP bang cu, doi ten BANG roi doi ten TUNG RANG BUOC tuong minh.

   CHAY LAI DUOC: neu dang do thi chay lai tu dau.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('bak3.DM_BenhNhan_B01') IS NULL
    THROW 50001, 'Chua chay 31_DOT1B_01_snapshot.sql - DUNG LAI.', 1;
IF OBJECT_ID('dbo.DM_BenhNhanCoSo') IS NULL
    THROW 50002, 'DM_BenhNhanCoSo khong con - B02 da chay roi?', 1;
GO
/* don du dang cua lan chay truoc */
IF OBJECT_ID('dbo.DM_BenhNhan_Moi') IS NOT NULL DROP TABLE dbo.DM_BenhNhan_Moi;
GO

/* --- 1. bang moi, ten rang buoc TAM ------------------------------------- */
CREATE TABLE dbo.DM_BenhNhan_Moi
(
    ID              bigint         NOT NULL IDENTITY(1,1),
    IDCoSo          bigint         NULL,          -- NULL = TRANG THAI QUA DO (PA-C1)
    MaBN            varchar(20)    NULL,
    CCCD            varchar(20)    NOT NULL,
    TenBN           nvarchar(200)  NOT NULL,
    SDT             varchar(20)    NULL,          -- khoa gom ho so (C1)
    Email           varchar(100)   NULL,
    DiaChi          nvarchar(255)  NULL,
    NgaySinh        datetime       NULL,
    HoTenKhongDau   nvarchar(200)  NULL,
    GioiTinh        varchar(10)    NULL,
    DaMoTaiLieu     bit            NOT NULL CONSTRAINT DF_DM_BenhNhan_DaMoTaiLieu_Moi DEFAULT ((0)),
    NgayXemLichCuoi datetime       NULL,
    NgayTao         datetime       NOT NULL CONSTRAINT DF_DM_BenhNhan_NgayTao_Moi     DEFAULT (GETDATE()),
    CONSTRAINT PK_DM_BenhNhan_Moi PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT CK_DM_BenhNhan_GioiTinh_Moi CHECK
        (GioiTinh IS NULL OR GioiTinh IN ('1','2','3','Nam',N'Nữ','Nu',N'Không xác định','KXD'))
);
GO

/* --- 2. copy: moi dong DM_BenhNhanCoSo -> 1 dong, GIU NGUYEN ID ---------- */
SET IDENTITY_INSERT dbo.DM_BenhNhan_Moi ON;

INSERT INTO dbo.DM_BenhNhan_Moi
    (ID, IDCoSo, MaBN, CCCD, TenBN, SDT, Email, DiaChi, NgaySinh, HoTenKhongDau,
     GioiTinh, DaMoTaiLieu, NgayXemLichCuoi, NgayTao)
SELECT cs.ID, cs.IDCoSo, cs.MaBN,
       b.CCCD, b.TenBN, b.SDT, b.Email, b.DiaChi, b.NgaySinh, b.HoTenKhongDau, b.GioiTinh,
       cs.DaMoTaiLieu, cs.NgayXemLichCuoi,
       cs.NgayTao          -- NgayTao cua dong CO SO, khong phai cap con-nguoi
FROM dbo.DM_BenhNhanCoSo cs
JOIN dbo.DM_BenhNhan b ON b.ID = cs.IDBenhNhan;

/* 20 dong DM_BenhNhan khong co ho so co so nao -> khong co ID nguon,
   cap ID MOI tren MAX(DM_BenhNhanCoSo.ID), IDCoSo = NULL */
DECLARE @moc bigint = (SELECT ISNULL(MAX(ID), 0) FROM dbo.DM_BenhNhanCoSo);

INSERT INTO dbo.DM_BenhNhan_Moi
    (ID, IDCoSo, MaBN, CCCD, TenBN, SDT, Email, DiaChi, NgaySinh, HoTenKhongDau,
     GioiTinh, DaMoTaiLieu, NgayXemLichCuoi, NgayTao)
SELECT @moc + ROW_NUMBER() OVER (ORDER BY b.ID),
       NULL, NULL,
       b.CCCD, b.TenBN, b.SDT, b.Email, b.DiaChi, b.NgaySinh, b.HoTenKhongDau, b.GioiTinh,
       0, NULL, b.NgayTao
FROM dbo.DM_BenhNhan b
WHERE NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo cs WHERE cs.IDBenhNhan = b.ID);

SET IDENTITY_INSERT dbo.DM_BenhNhan_Moi OFF;
GO

/* --- 3+4. CHAN roi PHA, TRONG CUNG MOT BATCH ---------------------------- */
/* 🔴 Chot phai nam CUNG BATCH voi cau pha. THROW/RETURN chi thoat BATCH:
   dat chot truoc GO roi DROP o batch sau thi chot la DO TRANG TRI - batch sau
   van chay. Da dap that ngay 17-09 (2 chot cung keu ma 18 bang van bi xoa). */
DECLARE @nguon int = (SELECT COUNT(*) FROM dbo.DM_BenhNhanCoSo)
                   + (SELECT COUNT(*) FROM dbo.DM_BenhNhan b
                      WHERE NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo cs WHERE cs.IDBenhNhan = b.ID));
DECLARE @dich  int = (SELECT COUNT(*) FROM dbo.DM_BenhNhan_Moi);

IF @nguon <> @dich OR OBJECT_ID('bak3.DM_BenhNhan_B01') IS NULL
BEGIN
    RAISERROR('B02 DUNG LAI: bang moi %d dong, nguon %d dong (hoac thieu snapshot bak3). KHONG pha bang cu.',
              16, 1, @dich, @nguon);
END
ELSE
BEGIN
    IF OBJECT_ID('dbo.FK_DM_BenhNhanCoSo_BenhNhan') IS NOT NULL
        ALTER TABLE dbo.DM_BenhNhanCoSo DROP CONSTRAINT FK_DM_BenhNhanCoSo_BenhNhan;
    DROP TABLE dbo.DM_BenhNhan;      -- keo theo PK/UK/CK cu, khong de lai ten trung
END
GO

/* --- 5. doi ten BANG roi doi ten TUNG RANG BUOC tuong minh -------------- */
/* Tung batch duoi day TU CHOT lay, de buoc 3+4 that bai thi chung khong chay. */
IF OBJECT_ID('dbo.DM_BenhNhan') IS NULL AND OBJECT_ID('dbo.DM_BenhNhan_Moi') IS NOT NULL
BEGIN
    EXEC sp_rename 'dbo.DM_BenhNhan_Moi',                'DM_BenhNhan';
    EXEC sp_rename 'dbo.PK_DM_BenhNhan_Moi',             'PK_DM_BenhNhan',             'OBJECT';
    EXEC sp_rename 'dbo.CK_DM_BenhNhan_GioiTinh_Moi',    'CK_DM_BenhNhan_GioiTinh',    'OBJECT';
    EXEC sp_rename 'dbo.DF_DM_BenhNhan_DaMoTaiLieu_Moi', 'DF_DM_BenhNhan_DaMoTaiLieu', 'OBJECT';
    EXEC sp_rename 'dbo.DF_DM_BenhNhan_NgayTao_Moi',     'DF_DM_BenhNhan_NgayTao',     'OBJECT';
END
GO

/* --- 6. khoa / rang buoc dich ------------------------------------------- */
/* Be nguyen UK_DM_BenhNhanCoSo_MaBN. Them ve IDCoSo IS NOT NULL vi IDCoSo nay
   da NULL duoc, ma SQL Server coi cac NULL la BANG NHAU trong unique index. */
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhan_MaBN'
                                               AND object_id = OBJECT_ID('dbo.DM_BenhNhan'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_DM_BenhNhan_MaBN
        ON dbo.DM_BenhNhan (IDCoSo, MaBN)
        WHERE MaBN IS NOT NULL AND IDCoSo IS NOT NULL;
GO
/* Thay UK_DM_BenhNhan_CCCD cu (unique CCCD toan he). Ve IDCoSo IS NOT NULL la
   BAT BUOC - thieu no thi hai dong neo cung CCCD se dam nhau. */
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhan_CCCD'
                                               AND object_id = OBJECT_ID('dbo.DM_BenhNhan'))
    CREATE UNIQUE NONCLUSTERED INDEX UK_DM_BenhNhan_CCCD
        ON dbo.DM_BenhNhan (IDCoSo, CCCD)
        WHERE IDCoSo IS NOT NULL AND CCCD <> '11111111111' AND CCCD <> '111111111111';
GO
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NOT NULL
   AND OBJECT_ID('dbo.FK_DM_BenhNhan_CoSo') IS NULL
    ALTER TABLE dbo.DM_BenhNhan WITH CHECK
        ADD CONSTRAINT FK_DM_BenhNhan_CoSo FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB (ID);
GO
/* KHONG tao UNIQUE(SDT, IDCoSo): 807 nhom vi pham san -> cau nay se no.
   Luat R2 nam o tang ung dung/stored, khong co luoi an toan o DB.
   KHONG them index nao khac (luat du an). */

/* --- 7. reseed IDENTITY ------------------------------------------------- */
DECLARE @max bigint = (SELECT ISNULL(MAX(ID), 0) FROM dbo.DM_BenhNhan);
DBCC CHECKIDENT ('dbo.DM_BenhNhan', RESEED, @max);
GO

/* --- 8. tu kiem --------------------------------------------------------- */
SELECT 'B02 gop' AS Buoc,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan)                          AS Tong_ky_vong_24189,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan WHERE IDCoSo IS NULL)     AS Neo_ky_vong_20,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan WHERE MaBN IS NOT NULL)   AS CoMaBN_ky_vong_16918,
       (SELECT MAX(ID)  FROM dbo.DM_BenhNhan)                          AS MaxID_ky_vong_24501,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan d
          JOIN bak3.DM_BenhNhanCoSo_B01 s ON s.ID = d.ID
         WHERE ISNULL(d.MaBN,'~') <> ISNULL(s.MaBN,'~')
            OR d.IDCoSo <> s.IDCoSo)                                   AS Lech_theo_ID_phai_0;
GO
