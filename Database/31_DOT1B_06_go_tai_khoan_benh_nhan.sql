/* =============================================================================
   DOT 1B - buoc B06: HT_TaiKhoan chi con Admin
   ---------------------------------------------------------------------------
   🔴 CHAY SAU B07 (neo lai HT_ThongBao/HT_PushDangKy) VA SAU B08 (don vai tro
   DoiTac). Chay som hon se vuong FK.

   Vi sao bo gan nhu chi la xoa rac: 13.000/13.154 tai khoan la SEED GIA
   (SDT dang 099xxxxxxxxx), chi 154 la SDT that. HT_TaiKhoan da co UNIQUE(SDT)
   nen "tai khoan" hom nay DA dong nghia "mot SDT" - bang chi la lop trung gian.
   Tu 1B, khoa gom ho so benh nhan la DM_BenhNhan.SDT (C1).

   DAO ADR 0027: bat bien "dang nhap duoc thi phai co HT_TaiKhoan" chuyen sang
   "phai co dong DM_BenhNhan tai co so do" (C7b).
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('bak3.HT_TaiKhoan_B01') IS NULL
    THROW 50060, 'Chua chay B01 snapshot - DUNG LAI.', 1;
GO

/* --- 1. CHAN roi XOA, cung mot batch ------------------------------------ */
DECLARE @conFkTaiKhoanBN int =
    (SELECT COUNT(*) FROM sys.foreign_keys
      WHERE referenced_object_id = OBJECT_ID('dbo.HT_TaiKhoan')
        AND name NOT IN ('FK_HT_ThongBao_NguoiGui'));   -- chi con NguoiGui duoc phep
DECLARE @conDoiTac int = (SELECT COUNT(*) FROM dbo.HT_TaiKhoan WHERE Role = 'DoiTac');
DECLARE @soAdmin   int = (SELECT COUNT(*) FROM dbo.HT_TaiKhoan WHERE Role = 'Admin');

IF @conFkTaiKhoanBN > 0
BEGIN
    RAISERROR('B06 DUNG LAI: con %d FK tro toi HT_TaiKhoan ngoai NguoiGui - chay B07 truoc.',
              16, 1, @conFkTaiKhoanBN);
END
ELSE IF @conDoiTac > 0
BEGIN
    RAISERROR('B06 DUNG LAI: con %d dong Role=DoiTac - chay B08 truoc.', 16, 1, @conDoiTac);
END
ELSE IF @soAdmin < 1
BEGIN
    RAISERROR('B06 DUNG LAI: khong tim thay tai khoan Admin nao - xoa se khoa cua khu Admin.',
              16, 1);
END
ELSE
BEGIN
    DELETE FROM dbo.HT_TaiKhoan WHERE Role <> 'Admin';
    SELECT 'B06: da xoa tai khoan khong phai Admin.' AS KetQua, @@ROWCOUNT AS SoDongXoa;
END
GO

/* --- 2. siet CHECK Role ve dung Admin ----------------------------------- */
IF OBJECT_ID('dbo.CK_HT_TaiKhoan_Role') IS NOT NULL
    ALTER TABLE dbo.HT_TaiKhoan DROP CONSTRAINT CK_HT_TaiKhoan_Role;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.HT_TaiKhoan WHERE Role <> 'Admin')
    ALTER TABLE dbo.HT_TaiKhoan WITH CHECK
        ADD CONSTRAINT CK_HT_TaiKhoan_Role CHECK (Role = 'Admin');
GO

/* --- 3. bo cot DM_BenhNhan.IDTaiKhoan ----------------------------------- */
/* Cot nay khong co FK, chi la cot. Sau B02 no khong con trong bang moi nua -
   cau duoi chi de chay dung ca khi thu tu bi dao. */
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDTaiKhoan') IS NOT NULL
    ALTER TABLE dbo.DM_BenhNhan DROP COLUMN IDTaiKhoan;
GO

/* --- 4. tu kiem --------------------------------------------------------- */
SELECT 'B06 go tai khoan benh nhan' AS Buoc,
       (SELECT COUNT(*) FROM dbo.HT_TaiKhoan)                            AS TaiKhoan_con_ky_vong_1,
       (SELECT COUNT(*) FROM dbo.HT_TaiKhoan WHERE Role <> 'Admin')      AS KhongPhaiAdmin_phai_0,
       (SELECT COUNT(*) FROM bak3.HT_TaiKhoan_B01)                       AS TaiKhoan_truoc_13154,
       (SELECT COUNT(*) FROM sys.columns
         WHERE object_id = OBJECT_ID('dbo.DM_BenhNhan') AND name = 'IDTaiKhoan') AS ConCotIDTaiKhoan_phai_0,
       (SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_HT_TaiKhoan_Role') AS CoCkRole_phai_1;
GO
