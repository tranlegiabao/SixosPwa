/* =============================================================================
   DOT A - buoc A2/A3: DM_CSKCB moi (32 cot) gop 4 bang 1:1 vao lam mot.
   KHONG dung sp_rename cho RANG BUOC (dot 2 da gay Msg 2714 vi the).
   Cach an toan: bang moi KHONG mang rang buoc nao -> bo bang cu (rang buoc cu
   di theo bang) -> sp_rename BANG -> moi tao rang buoc voi ten chinh thuc.

   Gop vao: DM_DoiTacApi (1:1) - DM_CSKCB_QuangCao (1:1) - HT_KhoFtpCoSo (1:1)
            HT_KhoaApiCoSo (bo han, giu 1 khoa/co so - ADR moi supersede 0022)
   Doi ten: Active->HienThiCongKhai  Img->AnhBia  QuangCao->QcSoTienDaTra
            IDDoiTac->IDCongTy  TrangChu->KetNoi_UrlChuyenHuong
            BaseUrl->KetNoi_BaseUrlHIS  KhoaGoiHIS->KetNoi_KhoaGoiHIS
   Bo cot: TenTM, SoToaNha (CSKCB) - KieuApi, MaChiNhanh (DoiTacApi)
           TenKhoa, NgayDungCuoi, Active (KhoaApiCoSo)

   Duong lui: bak2.*_A0 da chup nguyen van o A01 -> xem R02.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.DM_CSKCB_MOI') IS NOT NULL DROP TABLE dbo.DM_CSKCB_MOI;
GO
CREATE TABLE dbo.DM_CSKCB_MOI
(
    ID                     bigint         IDENTITY(1,1) NOT NULL,
    MaCoSo                 varchar(10)    NOT NULL,
    TenCoSo                nvarchar(200)  NOT NULL,
    Slug                   varchar(100)   NOT NULL,
    IDNhomCS               bigint         NULL,
    DiaChi                 nvarchar(255)  NULL,
    Tinh                   int            NULL,
    PhuongXa               int            NULL,
    SDT                    varchar(20)    NULL,
    Email                  varchar(100)   NULL,
    AnhBia                 nvarchar(500)  NULL,
    Logo                   nvarchar(max)  NULL,
    HienThiCongKhai        bit            NOT NULL,
    QcSoTienDaTra          decimal(15,0)  NULL,
    QcNoiDung              nvarchar(max)  NULL,
    QcAnh                  nvarchar(max)  NULL,
    NgayTao                datetime       NOT NULL,
    NgayCapNhat            datetime       NULL,
    IDCongTy               bigint         NULL,
    KetNoi_UrlChuyenHuong  nvarchar(255)  NULL,
    KetNoi_BaseUrlHIS      nvarchar(255)  NULL,
    KetNoi_KhoaGoiHIS      nvarchar(500)  NULL,
    KetNoi_Active          bit            NOT NULL,
    KhoaBam                varbinary(32)  NULL,
    Khoa_NgayCap           datetime       NULL,
    Khoa_NgayHetHan        datetime       NULL,
    Ftp_Host               nvarchar(200)  NULL,
    Ftp_TaiKhoan           nvarchar(100)  NULL,
    Ftp_MatKhau            nvarchar(200)  NULL,
    Ftp_ThuMucGoc          nvarchar(200)  NULL,
    Ftp_Active             bit            NOT NULL,
    Ftp_NgayThuDat         datetime       NULL
);
GO
/* ---- copy du lieu, giu nguyen gia tri ID (6 bang con dang tro vao) ----- */
SET IDENTITY_INSERT dbo.DM_CSKCB_MOI ON;

INSERT INTO dbo.DM_CSKCB_MOI
    (ID, MaCoSo, TenCoSo, Slug, IDNhomCS, DiaChi, Tinh, PhuongXa, SDT, Email,
     AnhBia, Logo, HienThiCongKhai, QcSoTienDaTra, QcNoiDung, QcAnh,
     NgayTao, NgayCapNhat, IDCongTy,
     KetNoi_UrlChuyenHuong, KetNoi_BaseUrlHIS, KetNoi_KhoaGoiHIS, KetNoi_Active,
     KhoaBam, Khoa_NgayCap, Khoa_NgayHetHan,
     Ftp_Host, Ftp_TaiKhoan, Ftp_MatKhau, Ftp_ThuMucGoc, Ftp_Active, Ftp_NgayThuDat)
SELECT
     c.ID, c.MaCoSo, c.TenCoSo, c.Slug, c.IDNhomCS, c.DiaChi, c.Tinh, c.PhuongXa, c.SDT, c.Email,
     c.Img, c.Logo, c.Active, c.QuangCao, qc.NoiDung, qc.Img,
     c.NgayTao, c.NgayCapNhat, c.IDDoiTac,
     api.TrangChu, api.BaseUrl, api.KhoaGoiHIS, ISNULL(api.Active, CONVERT(bit,1)),
     k.KhoaBam, k.NgayCap, k.NgayHetHan,
     f.Host, f.TaiKhoan, f.MatKhau, ISNULL(f.ThuMucGoc, N''), ISNULL(f.Active, CONVERT(bit,0)), f.NgayThuDat
FROM dbo.DM_CSKCB c
LEFT JOIN dbo.DM_DoiTacApi      api ON api.IDCoSo = c.ID
LEFT JOIN dbo.DM_CSKCB_QuangCao qc  ON qc.IDCoSo  = c.ID
LEFT JOIN dbo.HT_KhoFtpCoSo     f   ON f.IDCoSo   = c.ID
/* Co so co NHIEU khoa API (that: IDCoSo=8 co 2) => giu DUNG MOT: uu tien
   Active=1, roi NgayCap moi nhat. Mat kha nang xoay khoa khong gian doan la
   cai gia da duoc chot o V12. */
OUTER APPLY (
    SELECT TOP 1 kk.KhoaBam, kk.NgayCap, kk.NgayHetHan
    FROM dbo.HT_KhoaApiCoSo kk
    WHERE kk.IDCoSo = c.ID
    ORDER BY kk.Active DESC, kk.NgayCap DESC, kk.ID DESC
) k;

SET IDENTITY_INSERT dbo.DM_CSKCB_MOI OFF;
GO
/* ---- DOI CHIEU TRUOC khi dung toi bat cu bang cu nao ------------------ */
DECLARE @loi nvarchar(4000) = NULL;

IF (SELECT COUNT(*) FROM dbo.DM_CSKCB_MOI) <> (SELECT COUNT(*) FROM dbo.DM_CSKCB)
    SET @loi = N'Lech so dong CSKCB';
IF @loi IS NULL AND EXISTS (SELECT 1 FROM dbo.DM_CSKCB c
    JOIN dbo.DM_CSKCB_MOI m ON m.ID = c.ID
    WHERE m.MaCoSo <> c.MaCoSo OR m.TenCoSo <> c.TenCoSo OR m.Slug <> c.Slug
       OR m.HienThiCongKhai <> c.Active
       OR ISNULL(m.QcSoTienDaTra,-1) <> ISNULL(c.QuangCao,-1))
    SET @loi = N'Lech noi dung cot goc';
IF @loi IS NULL AND (SELECT COUNT(*) FROM dbo.DM_DoiTacApi) <>
                    (SELECT COUNT(*) FROM dbo.DM_CSKCB_MOI m JOIN dbo.DM_DoiTacApi a ON a.IDCoSo = m.ID)
    SET @loi = N'Co dong DM_DoiTacApi khong khop co so nao';
IF @loi IS NULL AND (SELECT COUNT(*) FROM dbo.DM_CSKCB_QuangCao) <>
                    (SELECT COUNT(*) FROM dbo.DM_CSKCB_MOI m JOIN dbo.DM_CSKCB_QuangCao q ON q.IDCoSo = m.ID)
    SET @loi = N'Co dong DM_CSKCB_QuangCao khong khop co so nao';
IF @loi IS NULL AND (SELECT COUNT(*) FROM dbo.HT_KhoFtpCoSo) <>
                    (SELECT COUNT(*) FROM dbo.DM_CSKCB_MOI m JOIN dbo.HT_KhoFtpCoSo f ON f.IDCoSo = m.ID)
    SET @loi = N'Co dong HT_KhoFtpCoSo khong khop co so nao';
IF @loi IS NULL AND (SELECT COUNT(DISTINCT IDCoSo) FROM dbo.HT_KhoaApiCoSo) <>
                    (SELECT COUNT(*) FROM dbo.DM_CSKCB_MOI WHERE KhoaBam IS NOT NULL)
    SET @loi = N'Lech so co so co khoa API';
IF @loi IS NULL AND (SELECT COUNT(*) FROM bak2.DM_CSKCB_A0) <> (SELECT COUNT(*) FROM dbo.DM_CSKCB)
    SET @loi = N'Ban chup A01 khong khop hien trang, chay lai A01 truoc';

IF @loi IS NOT NULL
BEGIN
    RAISERROR(N'A02 DUNG LAI: %s', 16, 1, @loi);
    RETURN;
END;
SELECT N'A02 doi chieu: DAT' AS KetQua, (SELECT COUNT(*) FROM dbo.DM_CSKCB_MOI) AS SoCoSo;
GO
/* ---- go khoa ngoai tro vao DM_CSKCB ----------------------------------- */
ALTER TABLE dbo.DM_BenhNhanCoSo       DROP CONSTRAINT FK_DM_BenhNhanCoSo_CoSo;
ALTER TABLE dbo.DM_CSKCB_CapQuangCao  DROP CONSTRAINT FK_DM_CSKCB_CapQuangCao_CoSo;
ALTER TABLE dbo.DM_CSKCB_GioLamViec   DROP CONSTRAINT FK_DM_CSKCB_GioLamViec_CoSo;
ALTER TABLE dbo.DM_CSKCB_NoiDung      DROP CONSTRAINT FK_DM_CSKCB_NoiDung_CoSo;
ALTER TABLE dbo.QL_DotKham            DROP CONSTRAINT FK_QL_DotKham_DM_CSKCB;
ALTER TABLE dbo.QL_TaiLieuBenhNhan    DROP CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_CSKCB;
/* 🔴 De sot lan dau chay: HT_TaiKhoanDoiTac tuy bi XOA o A04 (chay sau) nhung
   khoa ngoai cua no VAN chan DROP TABLE dbo.DM_CSKCB o day. Go truoc; ban than
   bang thi de A04 lo. Khong tra lai khoa nay - bang sap chet. */
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HT_TaiKhoanDoiTac_CoSo')
    ALTER TABLE dbo.HT_TaiKhoanDoiTac DROP CONSTRAINT FK_HT_TaiKhoanDoiTac_CoSo;
GO
/* ---- bo 4 bang da gop + bang cu (rang buoc cu di theo bang) ----------- */
DROP TABLE dbo.DM_DoiTacApi;
DROP TABLE dbo.DM_CSKCB_QuangCao;
DROP TABLE dbo.HT_KhoFtpCoSo;
DROP TABLE dbo.HT_KhoaApiCoSo;
DROP TABLE dbo.DM_CSKCB;
GO
EXEC sp_rename 'dbo.DM_CSKCB_MOI', 'DM_CSKCB';
GO
/* ---- gio moi dat rang buoc voi TEN CHINH THUC -------------------------- */
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT PK_DM_CSKCB PRIMARY KEY CLUSTERED (ID);
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT UK_DM_CSKCB_MaCoSo UNIQUE (MaCoSo);
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT UK_DM_CSKCB_Slug   UNIQUE (Slug);
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT DF_DM_CSKCB_HienThiCongKhai DEFAULT ((0))       FOR HienThiCongKhai;
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT DF_DM_CSKCB_NgayTao         DEFAULT (GETDATE()) FOR NgayTao;
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT DF_DM_CSKCB_KetNoiActive    DEFAULT ((1))       FOR KetNoi_Active;
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT DF_DM_CSKCB_FtpActive       DEFAULT ((0))       FOR Ftp_Active;
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT DF_DM_CSKCB_FtpThuMucGoc    DEFAULT (N'')       FOR Ftp_ThuMucGoc;
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT FK_DM_CSKCB_NhomCS  FOREIGN KEY (IDNhomCS) REFERENCES dbo.DM_NhomCS(ID);
ALTER TABLE dbo.DM_CSKCB ADD CONSTRAINT FK_DM_CSKCB_CongTy  FOREIGN KEY (IDCongTy) REFERENCES dbo.DM_DoiTac(ID);
GO
/* Khoa API duy nhat toan he - thay UK_HT_KhoaApiCoSo_KhoaBam cu.
   KhoaCoSoAttribute tra theo KhoaBam tren MOI dong nen no PHAI duy nhat. */
CREATE UNIQUE NONCLUSTERED INDEX UK_DM_CSKCB_KhoaBam
    ON dbo.DM_CSKCB(KhoaBam) WHERE KhoaBam IS NOT NULL;
GO
/* ---- tra lai khoa ngoai tro vao DM_CSKCB ------------------------------- */
ALTER TABLE dbo.DM_BenhNhanCoSo      WITH CHECK ADD CONSTRAINT FK_DM_BenhNhanCoSo_CoSo        FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.DM_CSKCB_CapQuangCao WITH CHECK ADD CONSTRAINT FK_DM_CSKCB_CapQuangCao_CoSo   FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.DM_CSKCB_GioLamViec  WITH CHECK ADD CONSTRAINT FK_DM_CSKCB_GioLamViec_CoSo    FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.DM_CSKCB_NoiDung     WITH CHECK ADD CONSTRAINT FK_DM_CSKCB_NoiDung_CoSo       FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.QL_DotKham           WITH CHECK ADD CONSTRAINT FK_QL_DotKham_DM_CSKCB         FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.QL_TaiLieuBenhNhan   WITH CHECK ADD CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_CSKCB FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
GO
/* ---- tu kiem cuoi file ------------------------------------------------- */
SELECT 'A02' AS Buoc,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB)                                     AS SoCoSo,
       (SELECT COUNT(*) FROM bak2.DM_CSKCB_A0)                                 AS SoCoSoTruoc,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE KetNoi_BaseUrlHIS IS NOT NULL) AS CoBaseUrl,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE KhoaBam IS NOT NULL)           AS CoKhoaApi,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE Ftp_Host IS NOT NULL)          AS CoFtp,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE QcNoiDung IS NOT NULL)         AS CoQcNoiDung,
       (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'dbo')  AS SoBangDbo;
GO
