-- ============================================================================
-- 06 — Khai tu QL_LichSuKham
--
-- Bang 4 cot dem (NgayKhamDau / NgayKhamGanNhat / SoLanKham / TrangThai).
-- QL_DotKham o file 03 giu chi tiet 1 dong = 1 lan den; ba so dem do SUY THANG
-- tu do bang MIN/MAX/COUNT. Giu ca hai la giu cung mot con so o hai noi —
-- lech nhau la chuyen som muon va luc do khong biet tin cai nao.
--
-- DO THAT 2026-09-08: QL_LichSuKham co 0 DONG. Ben code, 2/3 cho goi dang
-- truyen thang danh sach rong (HomeController:521,539;
-- CoSoYTeController:545) => khai tu khong mat gi.
--
-- Van chep sang bak.* truoc khi xoa, dung quy uoc bak.* co san cua CSDL nay.
-- ============================================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QL_LichSuKham')
BEGIN
    PRINT '--- So dong QL_LichSuKham truoc khi xoa ---';
    SELECT SoDong = COUNT(*) FROM dbo.QL_LichSuKham;

    IF NOT EXISTS (SELECT 1 FROM sys.tables t
                   JOIN sys.schemas s ON s.schema_id = t.schema_id
                   WHERE s.name = 'bak' AND t.name = 'LichSuKham_V001')
    BEGIN
        SELECT * INTO bak.LichSuKham_V001 FROM dbo.QL_LichSuKham;
    END;

    DROP TABLE dbo.QL_LichSuKham;
END;
GO

-- Ben code phai bo cung dot: DbSet<LichSuKham>, mapping trong
-- ApplicationDbContext, Models/LichSuKham.cs va view ThongTinBenhNhan.cshtml.
