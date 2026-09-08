-- ============================================================================
-- 07 — Bo DM_CSKCB.ApiKey.
--
-- ✅ DA CHAY 2026-09-08, va V7 da lam xong ngay sau do. File nay giu lai de doi
--    chieu; chay lai la no-op vi cot khong con.
--
-- BAI HOC: file nay tung mang dieu kien "chi chay sau khi xong V7" nhung lai
-- nam chung thu muc voi cac file chay tuan tu, nen "chay full" la quet luon.
-- Dieu kien viet trong THAN file khong chan duoc ai. Lan sau: buoc co dieu
-- kien phai nam THU MUC KHAC, hoac ten file phai tu chan (vd
-- "KHONG_CHAY_CHUNG_07_...").
--
-- 🔴 DIEU KIEN TRUOC KHI CHAY:
--    Khu tai lieu (TaiLieuApiController + TaiLieuService) da doi sang attribute
--    [KhoaCoSo] va khong con dong nao doc cskcb.ApiKey. Kiem bang:
--        grep -rn "ApiKey" SixosPwa/
--    Con ket qua nao ngoai file nay thi DUNG, chua chay duoc.
--
-- Chay som se lam vo duong nhan tai lieu dang chay: EF se bao "Invalid column
-- name 'ApiKey'" ngay o cau truy van co so dau tien.
--
-- Sau khi chay, thuoc tinh DMCSKCB.ApiKey ben C# cung phai bo di.
-- ============================================================================

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_CSKCB') AND name = 'ApiKey')
BEGIN
    -- Bam cua moi khoa da duoc chuyen sang HT_KhoaApiCoSo o script 01. Kiem lai
    -- cho chac truoc khi bo cot — bo nham la mat khoa cua co so.
    IF EXISTS (
        SELECT 1 FROM dbo.DM_CSKCB cs
        WHERE cs.ApiKey IS NOT NULL AND LTRIM(RTRIM(cs.ApiKey)) <> ''
          AND NOT EXISTS (
                SELECT 1 FROM dbo.HT_KhoaApiCoSo k
                WHERE k.KhoaBam = HASHBYTES('SHA2_256', CONVERT(varchar(100), cs.ApiKey))))
    BEGIN
        RAISERROR (N'Con co so co ApiKey chua duoc chuyen sang HT_KhoaApiCoSo. Chay lai 01 truoc.', 16, 1);
        RETURN;
    END;

    ALTER TABLE dbo.DM_CSKCB DROP COLUMN ApiKey;
END;
GO
