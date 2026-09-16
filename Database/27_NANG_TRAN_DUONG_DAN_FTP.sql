-- ============================================================================
-- 27 — Nang tran DuongDanFtp: 500 -> 2000 ky tu
--
-- Vi sao: duong tro toi Kho phieu co so la duong do MACH KY SO ben HIS dat ra
-- (URLKySo, kieu nvarchar(MAX)), khong phai do cong dat. Do song 16/09: duong
-- dai nhat dang co 95 ky tu, nhung ten tep do nguoi dung go + ten benh nhan co
-- dau + nhieu tang thu muc thi 500 la mot tran CHAT MA KHONG CO LY DO.
-- Nguoi dung chot nang len tam 2000-3000; chon 2000.
--
-- 🔴 CON SO 2000 NAY NAM O BON CHO. Doi mot cho ma quen ba cho kia la quay lai
--    dung benh ma chot chan sinh ra de chong -- CAT IM LANG o mat xich yeu nhat,
--    benh nhan bam ra 404 ma khong ai biet vi sao:
--      (1) cot   dbo.QL_TaiLieuBenhNhan.DuongDanFtp            <- file nay
--      (2) tham so @DuongDanFtp cua QL_TaiLieuBenhNhan_Save    <- file 26
--      (3) EF  ApplicationDbContext.cs  HasMaxLength(2000)     <- C#
--      (4) AdminStoredProcedureService.cs  AddParameter(..2000)<- C#
--    Va o ben HIS con hai cho nua an theo:
--      (5) @DuongDanDaCo cua S00_SPWA_DoHienTrang              <- file 23
--      (6) chot chan LEN(@DuongDanFtp) trong S00_UploadOnline  <- ho so task/12
--
-- Cot nay KHONG nam trong index nao (UK_..._Nguon la tren
-- (IDCoSo, LoaiTaiLieu, MaNguonHIS), IX_..._Bam la tren BamNoiDung) nen noi rong
-- khong dung toi tran 1700 byte cua khoa index.
--
-- Chay lai duoc. Chay TRUOC file 26.
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO
SET NOCOUNT ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan')
              AND name = 'DuongDanFtp' AND max_length = 500 * 2)
BEGIN
    -- Giu nguyen NOT NULL: cot dang la bat buoc, noi rong khong duoc lam no long ra.
    ALTER TABLE dbo.QL_TaiLieuBenhNhan ALTER COLUMN DuongDanFtp nvarchar(2000) NOT NULL;
    PRINT N'  [+] Da noi DuongDanFtp 500 -> 2000.';
END
ELSE PRINT N'  [=] DuongDanFtp khong con la nvarchar(500) -- khong dung toi.';
GO

-- Doi chieu: phai ra 2000.
SELECT Cot = c.name,
       Kieu = t.name,
       SoKyTu = CASE WHEN c.max_length = -1 THEN -1 ELSE c.max_length / 2 END,
       c.is_nullable
  FROM sys.columns c
  JOIN sys.types t ON t.user_type_id = c.user_type_id
 WHERE c.object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND c.name = 'DuongDanFtp';
GO
