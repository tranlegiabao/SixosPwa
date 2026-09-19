/* =============================================================================
   A02b - chay TIEP tu cho A02 dung (lan chay dau 18-09).
   A02 da lam xong: tao DM_CSKCB_MOI + copy + doi chieu DAT + go 6 khoa ngoai
   + bo 4 bang da gop. No DUNG o `DROP TABLE dbo.DM_CSKCB` vi con mot khoa ngoai
   chua ai go: FK_HT_TaiKhoanDoiTac_CoSo (bang do den A04 moi bi xoa).
   A02 da duoc va; file nay chi de hoan tat lan chay dang do.
   Chay lai A02 tu dau (da va) cung ra ket qua nhu vay.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HT_TaiKhoanDoiTac_CoSo')
    ALTER TABLE dbo.HT_TaiKhoanDoiTac DROP CONSTRAINT FK_HT_TaiKhoanDoiTac_CoSo;
GO
/* ---- chot an toan truoc khi bo bang cu --------------------------------- */
IF (SELECT COUNT(*) FROM dbo.DM_CSKCB_MOI) <> (SELECT COUNT(*) FROM dbo.DM_CSKCB)
   OR (SELECT COUNT(*) FROM bak2.DM_CSKCB_A0) <> (SELECT COUNT(*) FROM dbo.DM_CSKCB)
BEGIN
    RAISERROR(N'A02b DUNG LAI: DM_CSKCB_MOI / ban chup A01 khong khop bang cu.', 16, 1);
    RETURN;
END;
GO
DROP TABLE dbo.DM_CSKCB;
GO
EXEC sp_rename 'dbo.DM_CSKCB_MOI', 'DM_CSKCB';
GO
/* ---- rang buoc voi TEN CHINH THUC (bang moi sinh ra tran, khong ke thua) */
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
CREATE UNIQUE NONCLUSTERED INDEX UK_DM_CSKCB_KhoaBam
    ON dbo.DM_CSKCB(KhoaBam) WHERE KhoaBam IS NOT NULL;
GO
ALTER TABLE dbo.DM_BenhNhanCoSo      WITH CHECK ADD CONSTRAINT FK_DM_BenhNhanCoSo_CoSo        FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.DM_CSKCB_CapQuangCao WITH CHECK ADD CONSTRAINT FK_DM_CSKCB_CapQuangCao_CoSo   FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.DM_CSKCB_GioLamViec  WITH CHECK ADD CONSTRAINT FK_DM_CSKCB_GioLamViec_CoSo    FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.DM_CSKCB_NoiDung     WITH CHECK ADD CONSTRAINT FK_DM_CSKCB_NoiDung_CoSo       FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.QL_DotKham           WITH CHECK ADD CONSTRAINT FK_QL_DotKham_DM_CSKCB         FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
ALTER TABLE dbo.QL_TaiLieuBenhNhan   WITH CHECK ADD CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_CSKCB FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB(ID);
GO
/* ---- tu kiem ----------------------------------------------------------- */
SELECT 'A02b' AS Buoc,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB)                                     AS SoCoSo,
       (SELECT COUNT(*) FROM bak2.DM_CSKCB_A0)                                 AS SoCoSoTruoc,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE KetNoi_BaseUrlHIS IS NOT NULL) AS CoBaseUrl,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE KetNoi_UrlChuyenHuong IS NOT NULL) AS CoUrlChuyenHuong,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE KhoaBam IS NOT NULL)           AS CoKhoaApi,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE Ftp_Host IS NOT NULL)          AS CoFtp,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE QcNoiDung IS NOT NULL)         AS CoQcNoiDung,
       (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'dbo')  AS SoBangDbo;

/* doi chieu NOI DUNG voi ban chup A01 - moi dong cu phai khop dung mot dong moi */
SELECT 'A02b doi chieu voi ban chup A01' AS Buoc,
       (SELECT COUNT(*) FROM bak2.DM_CSKCB_A0 a JOIN dbo.DM_CSKCB m ON m.ID = a.ID
         WHERE m.MaCoSo = a.MaCoSo AND m.TenCoSo = a.TenCoSo AND m.Slug = a.Slug
           AND m.HienThiCongKhai = a.Active
           AND ISNULL(m.QcSoTienDaTra,-1) = ISNULL(a.QuangCao,-1)
           AND ISNULL(m.AnhBia,N'') = ISNULL(a.Img,N'')) AS SoDongKhopHoanToan,
       (SELECT COUNT(*) FROM bak2.DM_CSKCB_A0)           AS SoDongCu;

/* 4 nhom gop: moi dong cu phai tim thay dung mot co so */
SELECT 'A02b gop DoiTacApi' AS Buoc, COUNT(*) AS SoDongCu,
       SUM(CASE WHEN ISNULL(m.KetNoi_BaseUrlHIS,N'')     = ISNULL(a.BaseUrl,N'')
                 AND ISNULL(m.KetNoi_UrlChuyenHuong,N'') = ISNULL(a.TrangChu,N'')
                 AND ISNULL(m.KetNoi_KhoaGoiHIS,N'')     = ISNULL(a.KhoaGoiHIS,N'')
                 AND m.KetNoi_Active = a.Active THEN 1 ELSE 0 END) AS SoDongKhop
FROM bak2.DM_DoiTacApi_A0 a JOIN dbo.DM_CSKCB m ON m.ID = a.IDCoSo;

SELECT 'A02b gop QuangCao' AS Buoc, COUNT(*) AS SoDongCu,
       SUM(CASE WHEN ISNULL(m.QcNoiDung,N'') = ISNULL(a.NoiDung,N'')
                 AND ISNULL(m.QcAnh,N'')     = ISNULL(a.Img,N'') THEN 1 ELSE 0 END) AS SoDongKhop
FROM bak2.DM_CSKCB_QuangCao_A0 a JOIN dbo.DM_CSKCB m ON m.ID = a.IDCoSo;

SELECT 'A02b gop KhoFtp' AS Buoc, COUNT(*) AS SoDongCu,
       SUM(CASE WHEN ISNULL(m.Ftp_Host,N'')      = ISNULL(a.Host,N'')
                 AND ISNULL(m.Ftp_TaiKhoan,N'')  = ISNULL(a.TaiKhoan,N'')
                 AND ISNULL(m.Ftp_MatKhau,N'')   = ISNULL(a.MatKhau,N'')
                 AND ISNULL(m.Ftp_ThuMucGoc,N'') = ISNULL(a.ThuMucGoc,N'')
                 AND m.Ftp_Active = a.Active THEN 1 ELSE 0 END) AS SoDongKhop
FROM bak2.HT_KhoFtpCoSo_A0 a JOIN dbo.DM_CSKCB m ON m.ID = a.IDCoSo;

/* Khoa API: moi CO SO tung co khoa gio phai co dung mot KhoaBam, va khoa do
   phai la mot trong cac khoa cu cua chinh co so ay. */
SELECT 'A02b gop KhoaApi' AS Buoc,
       (SELECT COUNT(DISTINCT IDCoSo) FROM bak2.HT_KhoaApiCoSo_A0)   AS SoCoSoCoKhoaCu,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE KhoaBam IS NOT NULL) AS SoCoSoCoKhoaMoi,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB m WHERE m.KhoaBam IS NOT NULL
          AND EXISTS (SELECT 1 FROM bak2.HT_KhoaApiCoSo_A0 a
                       WHERE a.IDCoSo = m.ID AND a.KhoaBam = m.KhoaBam)) AS SoKhoaTruyNguyenDuoc;
GO
