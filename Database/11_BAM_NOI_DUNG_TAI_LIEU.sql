/* =============================================================================
   11_BAM_NOI_DUNG_TAI_LIEU.sql
   Day lai tai lieu CUNG NOI DUNG thi KHONG de phien ban moi nua.
   DB: HIS_CSKH. Chay lai duoc. datetime + GETDATE().

   VI SAO: hop dong §A① noi "cung (co so, loai, maNguonHIS) => khong de dong
   trung; noi dung doi => them phien ban". Nghiem thu 08/09 do duoc: day lai y
   het van de phien ban moi (PhienBan 1->6 sau 6 lan day). Hien thi van dung
   (chi mot dong LaBanMoiNhat = 1) nhung MOI LAN THUA DE LAI MOT FILE PDF TREN
   FTP VINH VIEN.

   CACH LAM: luu BAM SHA-256 cua chinh noi dung PDF. Truoc khi day len FTP,
   tang service hoi bam nay; trung thi tra ve ban dang co, KHONG upload, KHONG
   them dong.

   🔴 KHONG dung DungLuongByte thay bam cho re. Hai PDF khac noi dung ma trung
   kich thuoc thi ket qua moi bi BO IM LANG -- voi tai lieu y te, danh doi do
   te hon han cai no chua.

   🔴 Dong CU co BamNoiDung = NULL: "chua biet bam", KHONG so duoc => cu day
   nhu cu (them phien ban). An toan, khong bao gio bo nham. Lan day sau bam
   duoc dien vao va tu do so duoc.
   ============================================================================= */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
GO

/* --- 1. Cot bam ----------------------------------------------------------- */
IF COL_LENGTH('dbo.QL_TaiLieuBenhNhan', 'BamNoiDung') IS NULL
    ALTER TABLE dbo.QL_TaiLieuBenhNhan ADD BamNoiDung char(64) NULL;
GO

/* Index de tra "ban moi nhat cua nguon nay co dung bam do khong" cho nhanh.
   Co LOC theo LaBanMoiNhat = 1 vi chi ban moi nhat moi duoc dem ra so. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_QL_TaiLieuBenhNhan_Bam'
                 AND object_id = OBJECT_ID('dbo.QL_TaiLieuBenhNhan'))
    CREATE INDEX IX_QL_TaiLieuBenhNhan_Bam
        ON dbo.QL_TaiLieuBenhNhan (IDCoSo, LoaiTaiLieu, MaNguonHIS, BamNoiDung)
        WHERE LaBanMoiNhat = 1;
GO

/* --- 2. Cua hoi TRUNG NOI DUNG -------------------------------------------
   Goi TRUOC khi upload FTP. Tra ve dong dang co neu trung, khong thi tra rong.
   Tach rieng thu tuc thay vi nhet vao _Save: quyet dinh "co upload khong" phai
   xay ra TRUOC khi dung toi kho tep -- cung nguyen tac cua V4 (quyet dinh xong
   moi dung kho). Nhet vao _Save la tep da nam tren FTP roi. */
IF OBJECT_ID('dbo.QL_TaiLieuBenhNhan_TimTrungNoiDung', 'P') IS NOT NULL
    DROP PROCEDURE dbo.QL_TaiLieuBenhNhan_TimTrungNoiDung;
GO
CREATE PROCEDURE dbo.QL_TaiLieuBenhNhan_TimTrungNoiDung
    @IDCoSo      bigint,
    @LoaiTaiLieu nvarchar(50),
    @MaNguonHIS  nvarchar(100),
    @BamNoiDung  char(64)
AS
BEGIN
    SET NOCOUNT ON;

    /* Thieu bat ky manh nao cua khoa thi KHONG ket luan duoc la trung.
       Tra rong => tang goi cu day nhu cu. */
    IF @MaNguonHIS IS NULL OR LEN(LTRIM(RTRIM(@MaNguonHIS))) = 0
       OR @BamNoiDung IS NULL OR LEN(LTRIM(RTRIM(@BamNoiDung))) = 0
        RETURN;

    SELECT TOP 1
           ID, IDBenhNhanCoSo, MaBN, LoaiTaiLieu, TenTaiLieu,
           DuongDanFtp, DungLuongByte, PhienBan, NgayTao
      FROM dbo.QL_TaiLieuBenhNhan WITH (NOLOCK)
     WHERE IDCoSo       = @IDCoSo
       AND LoaiTaiLieu  = @LoaiTaiLieu
       AND MaNguonHIS   = @MaNguonHIS
       AND BamNoiDung   = @BamNoiDung
       AND LaBanMoiNhat = 1
     ORDER BY ID DESC;
END
GO

/* --- 3. Doi chieu ---------------------------------------------------------
   Cot BamNoiDung va tham so @BamNoiDung cua _Save nam trong
   04_ALTER_QL_TaiLieuBenhNhan.sql (noi da ALTER bang va viet de stored) => thu
   tu chay tu nhien 04 roi 11. Cau duoi bao ngay neu ai do chi chay 11. */
IF COL_LENGTH('dbo.QL_TaiLieuBenhNhan', 'BamNoiDung') IS NULL
    RAISERROR (N'Thiếu cột BamNoiDung — chạy lại 04_ALTER_QL_TaiLieuBenhNhan.sql trước.', 16, 1);
GO
IF NOT EXISTS (SELECT 1 FROM sys.parameters
               WHERE object_id = OBJECT_ID('dbo.QL_TaiLieuBenhNhan_Save') AND name = '@BamNoiDung')
    RAISERROR (N'QL_TaiLieuBenhNhan_Save chưa có @BamNoiDung — chạy lại 04 trước.', 16, 1);
GO

SELECT 'Cot BamNoiDung'            AS Muc, ISNULL(CAST(COL_LENGTH('dbo.QL_TaiLieuBenhNhan','BamNoiDung') AS varchar(5)), '(chua co)') AS GiaTri
UNION ALL
SELECT 'Tham so @BamNoiDung', CAST(COUNT(*) AS varchar(5)) FROM sys.parameters
 WHERE object_id = OBJECT_ID('dbo.QL_TaiLieuBenhNhan_Save') AND name = '@BamNoiDung'
UNION ALL
SELECT 'Thu tuc tim trung', CAST(COUNT(*) AS varchar(5)) FROM sys.procedures
 WHERE name = 'QL_TaiLieuBenhNhan_TimTrungNoiDung'
UNION ALL
SELECT 'Dong chua co bam', CAST(COUNT(*) AS varchar(5)) FROM dbo.QL_TaiLieuBenhNhan WHERE BamNoiDung IS NULL;
GO
