/* =============================================================================
   DOT 1B - buoc B03: doi ten cot IDBenhNhanCoSo -> IDBenhNhan (C14)
   ---------------------------------------------------------------------------
   Cham 2 bang lon nhung KHONG UPDATE DONG NAO: chi doi ten cot + dung lai FK.
   Gia tri trong cot da dung san nho KEEP-ID o B02.

   sp_rename cho COT la an toan (khac voi sp_rename cho BANG o §2.1): no khong
   dung toi ten rang buoc. Nhung FK phai go TRUOC vi dich cu (DM_BenhNhanCoSo)
   sap bi bo o B05.

   BAT DOI XUNG CO Y - dung "don cho gon":
     - COT      : doi thanh IDBenhNhan
     - THAM SO  : @IDBenhNhanCoSo cua QL_DotKham_Save / QL_TaiLieuBenhNhan_Save
                  GIU NGUYEN (hop dong linked server voi HIS)
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NULL
    THROW 50010, 'Chua chay B02 - DM_BenhNhan chua co cot IDCoSo. DUNG LAI.', 1;
GO

/* --- 1. go 2 FK tro toi DM_BenhNhanCoSo --------------------------------- */
IF OBJECT_ID('dbo.FK_QL_DotKham_DM_BenhNhanCoSo') IS NOT NULL
    ALTER TABLE dbo.QL_DotKham DROP CONSTRAINT FK_QL_DotKham_DM_BenhNhanCoSo;
IF OBJECT_ID('dbo.FK_QL_TaiLieuBenhNhan_DM_BenhNhanCoSo') IS NOT NULL
    ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_BenhNhanCoSo;
GO

/* --- 2. doi ten cot ----------------------------------------------------- */
IF COL_LENGTH('dbo.QL_DotKham', 'IDBenhNhanCoSo') IS NOT NULL
    EXEC sp_rename 'dbo.QL_DotKham.IDBenhNhanCoSo', 'IDBenhNhan', 'COLUMN';
IF COL_LENGTH('dbo.QL_TaiLieuBenhNhan', 'IDBenhNhanCoSo') IS NOT NULL
    EXEC sp_rename 'dbo.QL_TaiLieuBenhNhan.IDBenhNhanCoSo', 'IDBenhNhan', 'COLUMN';
GO
/* doi ten index co san cho khop (KHONG tao index moi) */
IF EXISTS (SELECT 1 FROM sys.indexes
            WHERE name = 'IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo'
              AND object_id = OBJECT_ID('dbo.QL_TaiLieuBenhNhan'))
    EXEC sp_rename 'dbo.QL_TaiLieuBenhNhan.IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo',
                   'IX_QL_TaiLieuBenhNhan_IDBenhNhan', 'INDEX';
GO

/* --- 3. chan truoc khi dung FK moi: khong duoc co dong mo coi ----------- */
DECLARE @mocoi int =
    (SELECT COUNT(*) FROM dbo.QL_DotKham t
      WHERE NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan b WHERE b.ID = t.IDBenhNhan))
  + (SELECT COUNT(*) FROM dbo.QL_TaiLieuBenhNhan t
      WHERE NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan b WHERE b.ID = t.IDBenhNhan));
IF @mocoi <> 0
    THROW 50011, 'Con dong tro toi ID khong ton tai trong DM_BenhNhan - DUNG LAI.', 1;
GO

/* --- 4. dung lai FK, lan nay tro toi DM_BenhNhan ------------------------ */
ALTER TABLE dbo.QL_DotKham WITH CHECK
    ADD CONSTRAINT FK_QL_DotKham_DM_BenhNhan
        FOREIGN KEY (IDBenhNhan) REFERENCES dbo.DM_BenhNhan (ID);
GO
ALTER TABLE dbo.QL_TaiLieuBenhNhan WITH CHECK
    ADD CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_BenhNhan
        FOREIGN KEY (IDBenhNhan) REFERENCES dbo.DM_BenhNhan (ID);
GO

/* --- 5. tu kiem --------------------------------------------------------- */
SELECT 'B03 doi ten cot' AS Buoc,
       (SELECT COUNT(*) FROM sys.columns
         WHERE object_id = OBJECT_ID('dbo.QL_DotKham')         AND name = 'IDBenhNhan') AS DotKham_co_cot_phai_1,
       (SELECT COUNT(*) FROM sys.columns
         WHERE object_id = OBJECT_ID('dbo.QL_TaiLieuBenhNhan') AND name = 'IDBenhNhan') AS TaiLieu_co_cot_phai_1,
       (SELECT COUNT(*) FROM sys.foreign_keys
         WHERE name IN ('FK_QL_DotKham_DM_BenhNhan','FK_QL_TaiLieuBenhNhan_DM_BenhNhan')) AS FK_moi_phai_2,
       (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1)                   AS FK_khong_tin_phai_0,
       (SELECT COUNT(*) FROM dbo.QL_DotKham)         AS DotKham_ky_vong_84153,
       (SELECT COUNT(*) FROM dbo.QL_TaiLieuBenhNhan) AS TaiLieu_ky_vong_614636;
GO
