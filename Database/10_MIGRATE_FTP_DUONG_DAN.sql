-- ============================================================================
-- SCRIPT MIGRATION: TÁI CẤU TRÚC ĐƯỜNG DẪN TÀI LIỆU VÀ HÌNH ẢNH TRÊN FTP
-- ============================================================================
-- 1. Chuyển đường dẫn QL_TaiLieuBenhNhan.DuongDanFtp sang cấu trúc:
--    sixospwa/{MaCoSo}/tailieu/{MaBN}/{TenFile}
-- 2. Chuyển đường dẫn ảnh cơ sở DM_CSKCB (Logo, Img) sang:
--    /anh/{MaCoSo}/logo/{TenFile}, /anh/{MaCoSo}/hinh_anh/{TenFile}
-- 3. Chuyển ảnh quảng cáo DM_CSKCB_QuangCao.Img sang:
--    /anh/{MaCoSo}/quang_cao/{TenFile}
-- 4. Cập nhật src ảnh trong bài viết ND_CSKCB.NoiDung sang:
--    /anh/{MaCoSo}/noi_dung/{TenFile}
-- ============================================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

PRINT N'--- BẮT ĐẦU MIGRATION ĐƯỜNG DẪN FTP & ẢNH ---';

-- 1. Cập nhật đường dẫn tài liệu bệnh nhân
-- Format cũ: sixospwa/tailieu/{MaCoSo}/{yyyy}/{MM}/{TenFile}
-- Format mới: sixospwa/{MaCoSo}/tailieu/{MaBN}/{TenFile}
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QL_TaiLieuBenhNhan')
BEGIN
    DECLARE @SoLuongTaiLieu INT;

    SELECT @SoLuongTaiLieu = COUNT(*)
    FROM dbo.QL_TaiLieuBenhNhan tl
    INNER JOIN dbo.DM_CSKCB cs ON tl.IDCoSo = cs.ID
    WHERE tl.DuongDanFtp LIKE 'sixospwa/tailieu/%';

    PRINT N'1. Tìm thấy ' + CAST(@SoLuongTaiLieu AS NVARCHAR(20)) + N' tài liệu PDF cần chuyển đổi đường dẫn.';

    IF @SoLuongTaiLieu > 0
    BEGIN
        UPDATE tl
        SET tl.DuongDanFtp = 'sixospwa/' + cs.MaCoSo + '/tailieu/' 
                           + REPLACE(REPLACE(tl.MaBN, ' ', '_'), '/', '_') + '/' 
                           + RIGHT(tl.DuongDanFtp, CHARINDEX('/', REVERSE(tl.DuongDanFtp)) - 1)
        FROM dbo.QL_TaiLieuBenhNhan tl
        INNER JOIN dbo.DM_CSKCB cs ON tl.IDCoSo = cs.ID
        WHERE tl.DuongDanFtp LIKE 'sixospwa/tailieu/%';

        PRINT N'   -> Đã cập nhật xong đường dẫn cho QL_TaiLieuBenhNhan.';
    END;
END;

-- 2. Cập nhật Logo và Img của cơ sở trong DM_CSKCB
-- Format cũ: /anh/logo_cs/{file}, /anh/img_cs/{file}
-- Format mới: /anh/{MaCoSo}/logo/{file}, /anh/{MaCoSo}/hinh_anh/{file}
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DM_CSKCB')
BEGIN
    -- Logo
    UPDATE dbo.DM_CSKCB
    SET Logo = '/anh/' + RTRIM(LTRIM(MaCoSo)) + '/logo/' + SUBSTRING(Logo, 14, 500)
    WHERE Logo LIKE '/anh/logo_cs/%';

    PRINT N'2.1. Đã cập nhật Logo cơ sở (DM_CSKCB): ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + N' dòng.';

    -- Ảnh đại diện cơ sở (Img)
    UPDATE dbo.DM_CSKCB
    SET Img = '/anh/' + RTRIM(LTRIM(MaCoSo)) + '/hinh_anh/' + SUBSTRING(Img, 13, 500)
    WHERE Img LIKE '/anh/img_cs/%';

    PRINT N'2.2. Đã cập nhật Img cơ sở (DM_CSKCB): ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + N' dòng.';
END;

-- 3. Cập nhật ảnh quảng cáo trong DM_CSKCB_QuangCao
-- Format cũ: /anh/img_qc_kcb/{file}
-- Format mới: /anh/{MaCoSo}/quang_cao/{file}
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DM_CSKCB_QuangCao')
BEGIN
    UPDATE qc
    SET qc.Img = '/anh/' + RTRIM(LTRIM(cs.MaCoSo)) + '/quang_cao/' + SUBSTRING(qc.Img, 17, 500)
    FROM dbo.DM_CSKCB_QuangCao qc
    INNER JOIN dbo.DM_CSKCB cs ON qc.IdCoSo = cs.ID
    WHERE qc.Img LIKE '/anh/img_qc_kcb/%';

    PRINT N'3. Đã cập nhật ảnh quảng cáo (DM_CSKCB_QuangCao): ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + N' dòng.';
END;

-- 4. Cập nhật ảnh bài viết HTML trong ND_CSKCB
-- Format cũ: /anh/img_nd/{file}
-- Format mới: /anh/{MaCoSo}/noi_dung/{file}
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ND_CSKCB')
BEGIN
    UPDATE nd
    SET nd.NoiDung = REPLACE(nd.NoiDung, '/anh/img_nd/', '/anh/' + RTRIM(LTRIM(cs.MaCoSo)) + '/noi_dung/')
    FROM dbo.ND_CSKCB nd
    INNER JOIN dbo.DM_CSKCB cs ON nd.IdCoSo = cs.ID
    WHERE nd.NoiDung LIKE '%/anh/img_nd/%';

    PRINT N'4. Đã cập nhật link ảnh bài viết (ND_CSKCB): ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + N' dòng.';
END;

COMMIT TRANSACTION;
PRINT N'--- HOÀN THÀNH MIGRATION THÀNH CÔNG ---';
GO
