/* =============================================================================
   DOT 1B - buoc B05: DROP TABLE dbo.DM_BenhNhanCoSo
   ---------------------------------------------------------------------------
   CHI CHAY SAU KHI B04 bao DAT o MOI phep. Day la buoc khong lui duoc bang
   lenh don - muon lui phai dung R05 (dung lai tu bak3.DM_BenhNhanCoSo_B01).

   🔴 Chot nam CUNG BATCH voi cau DROP. THROW/RETURN chi thoat BATCH, dat chot
   truoc GO roi DROP o batch sau la DO TRANG TRI.
   🔴 Chot phai HEP DUNG CHIEU: hoi "da hop nhat xong chua", khong hoi
   "bang con ton tai khong" (cau sau luon dung nen khong chan duoc gi).

   Sau buoc nay stored DM_BenhNhanCoSo_Save van con nhung than dang tro toi bang
   vua bo -> B10 viet lai. Tat app trong suot qua trinh di tru.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* 🔴 int chu KHONG phai bit: RAISERROR khong nhan bit lam tham so thay the %d
   (loi "Cannot specify bit data type as a substitution parameter"), va do la loi
   BIEN DICH — ca batch khong chay, ke ca nhanh ELSE. Da dap that 19-09. */
DECLARE @coSnapshot    int = CASE WHEN OBJECT_ID('bak3.DM_BenhNhanCoSo_B01') IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @daGopCot      int = CASE WHEN COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NOT NULL
                                   AND COL_LENGTH('dbo.DM_BenhNhan', 'MaBN')   IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @daDoiTenCot   int = CASE WHEN COL_LENGTH('dbo.QL_DotKham', 'IDBenhNhan')         IS NOT NULL
                                   AND COL_LENGTH('dbo.QL_TaiLieuBenhNhan', 'IDBenhNhan') IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @conFkCu       int = (SELECT COUNT(*) FROM sys.foreign_keys
                               WHERE referenced_object_id = OBJECT_ID('dbo.DM_BenhNhanCoSo'));
DECLARE @soDongDich    int = (SELECT COUNT(*) FROM dbo.DM_BenhNhan);
DECLARE @soDongNguon   int = (SELECT COUNT(*) FROM bak3.DM_BenhNhanCoSo_B01);
DECLARE @moCoi         int = (SELECT COUNT(*) FROM dbo.QL_DotKham t
                               WHERE NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan b WHERE b.ID = t.IDBenhNhan))
                           + (SELECT COUNT(*) FROM dbo.QL_TaiLieuBenhNhan t
                               WHERE NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan b WHERE b.ID = t.IDBenhNhan));

IF OBJECT_ID('dbo.DM_BenhNhanCoSo') IS NULL
BEGIN
    SELECT 'B05: DM_BenhNhanCoSo da khong con - bo qua.' AS KetQua;
END
ELSE IF @coSnapshot = 0 OR @daGopCot = 0 OR @daDoiTenCot = 0
     OR @conFkCu > 0 OR @moCoi > 0 OR @soDongDich < @soDongNguon
BEGIN
    RAISERROR('B05 DUNG LAI - chua du dieu kien bo bang. snapshot=%d gopCot=%d doiTenCot=%d fkCu=%d moCoi=%d dich=%d nguon=%d',
              16, 1, @coSnapshot, @daGopCot, @daDoiTenCot, @conFkCu, @moCoi, @soDongDich, @soDongNguon);
END
ELSE
BEGIN
    DROP TABLE dbo.DM_BenhNhanCoSo;
    SELECT 'B05: da bo dbo.DM_BenhNhanCoSo.' AS KetQua;
END
GO

/* --- tu kiem ------------------------------------------------------------ */
SELECT 'B05 bo DM_BenhNhanCoSo' AS Buoc,
       (SELECT COUNT(*) FROM sys.tables
         WHERE SCHEMA_NAME(schema_id) = 'dbo')                       AS SoBangDbo_ky_vong_15,
       (SELECT COUNT(*) FROM sys.tables
         WHERE SCHEMA_NAME(schema_id) = 'dbo' AND name = 'DM_BenhNhanCoSo') AS ConBangCu_phai_0,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan)                        AS DM_BenhNhan_ky_vong_24189;
GO
