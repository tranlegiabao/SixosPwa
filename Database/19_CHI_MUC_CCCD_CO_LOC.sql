-- ============================================================================
-- 19 - Chi muc duy nhat tren CCCD phai CO LOC, bo hai ma gia ra ngoai
--
-- CHAY TREN: HIS_CSKH (co so du lieu cua CONG). KHONG chay ben HIS.
--
-- 🔴 File nay ghi UTF-8 CO BOM. Dung go BOM di (xem ADR 0026 - da mat mot ngay
--    vi chuyen do). Chay bang sqlcmd thi them co: sqlcmd -f 65001 -i <file>
--
-- CHAY LAI DUOC bao nhieu lan cung duoc.
--
-- VAN DE
--   `UK_DM_BenhNhan_CCCD` dang la UNIQUE **khong loc** tren cot CCCD. HIS danh dau
--   "benh nhan khong co can cuoc" bang ma gia 11111111111 / 111111111111, va benh
--   nhan go dung ma do vao o CCCD bat buoc cua cong. Hau qua: moi ma gia chi MOT
--   ho so giu duoc tren toan he thong. Do that 10/09: ca hai suat da bi chiem
--   (ID 13 giu 111111111111 tu 25/08, ID 67 giu 11111111111 tu 09/09), nen nguoi
--   thu ba khong co can cuoc KHONG TAO NOI ho so, va con doc phai mot cau sai su
--   that: "So can cuoc nay da thuoc ve mot nguoi khac.".
--
-- CACH VA
--   Dung chi muc duy nhat CO LOC: van chan trung can cuoc THAT, nhung tha hai ma
--   gia ra - chung khong phai danh tinh nen khong co gi de chan.
--
--   🔴 Ho so mang ma gia VAN KHONG NOI DUOC benh an (chot 10/09, ADR 0028) - viec
--   nay chi thoi khong cho chung dam vao nhau o tang chi muc.
--
-- Chay xong PHAI doc cong kiem o cuoi file.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- ── Cong chan: co can cuoc THAT nao dang trung khong ────────────────────────
-- Co thi DUNG LAI, khong drop chi muc cu. Drop xong moi phat hien trung thi
-- khong dung lai duoc nua - ma luc do bang dang KHONG co chi muc nao bao ve.
IF EXISTS (
    SELECT 1 FROM dbo.DM_BenhNhan
     WHERE CCCD <> '11111111111' AND CCCD <> '111111111111'
     GROUP BY CCCD HAVING COUNT(*) > 1)
BEGIN
    RAISERROR (N'DUNG LAI: dang co so can cuoc THAT bi trung. Don trung roi chay lai.', 16, 1);
END;
GO

-- ── Dung lai chi muc ────────────────────────────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan')
              AND name = N'UK_DM_BenhNhan_CCCD'
              AND has_filter = 0)
BEGIN
    PRINT '19: chi muc dang KHONG co loc -> dung lai.';
    DROP INDEX UK_DM_BenhNhan_CCCD ON dbo.DM_BenhNhan;
END
ELSE
BEGIN
    PRINT '19: chi muc da co loc san (hoac khong ton tai) -> bo qua buoc drop.';
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan')
                  AND name = N'UK_DM_BenhNhan_CCCD')
BEGIN
    -- 🔴 Vi ngu phap cua chi muc CO LOC: chi nhan so sanh don gian noi bang AND.
    -- KHONG dung duoc `NOT IN (...)` hay `OR` - SQL Server tu choi ngay.
    CREATE UNIQUE NONCLUSTERED INDEX UK_DM_BenhNhan_CCCD
        ON dbo.DM_BenhNhan (CCCD)
     WHERE CCCD <> '11111111111' AND CCCD <> '111111111111';

    PRINT '19: da tao lai UK_DM_BenhNhan_CCCD CO LOC.';
END;
GO

-- ============================================================================
-- CONG KIEM
--   (1) Chi muc phai co loc  -> has_filter = 1
--   (2) Ma gia phai chua duoc nhieu dong -> thu dem, khong INSERT gi
-- ============================================================================
SELECT i.name        AS ChiMuc,
       i.is_unique   AS Duynhat,
       i.has_filter  AS CoLoc,
       i.filter_definition AS BieuThucLoc
  FROM sys.indexes i
 WHERE i.object_id = OBJECT_ID(N'dbo.DM_BenhNhan')
   AND i.name = N'UK_DM_BenhNhan_CCCD';

SELECT CCCD, COUNT(*) AS SoHoSoDangGiu
  FROM dbo.DM_BenhNhan
 WHERE CCCD = '11111111111' OR CCCD = '111111111111'
 GROUP BY CCCD;
GO
