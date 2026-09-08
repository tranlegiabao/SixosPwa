-- ============================================================================
-- 90 — Cap mot khoa THU NGHIEM de nghiem thu khu API nhan
--
-- Vi sao can: ba khoa cu cua 3 co so da duoc chuyen sang dang BAM o script 01.
-- Bam la mot chieu — khong doc nguoc ra khoa tho duoc. Muon go thu thi phai
-- cap mot khoa moi ma minh BIET noi dung.
--
-- Day KHONG phai buoc trien khai. Chay khi can nghiem thu, xong thi tat khoa
-- bang doan o cuoi file.
--
-- Co so chon san: MaCoSo = 75265 (ID = 8) — dang co khoa, Active = 1, va co 3
-- ho so trong DM_BenhNhanCoSo nen thu duoc CA hai nhanh nhan/tu choi.
-- ============================================================================

DECLARE @IDCoSo   bigint = 8;
DECLARE @KhoaTho  varchar(100) = 'THUNGHIEM-DOT2-20260908-a7f3c1';

DECLARE @IDKhoa bigint, @rc int, @msg nvarchar(4000);

EXEC dbo.HT_KhoaApiCoSo_Save
    @ID            = 0,
    @IDCoSo        = @IDCoSo,
    @TenKhoa       = N'Khoa thu nghiem Dot 2 (xoa sau khi nghiem thu)',
    @KhoaTho       = @KhoaTho,
    @Active        = 1,
    @NgayHetHan    = NULL,
    @IDKhoa        = @IDKhoa        OUTPUT,
    @ResultCode    = @rc            OUTPUT,
    @ResultMessage = @msg           OUTPUT;

SELECT KetQua = @rc, ThongBao = @msg, IDKhoaMoi = @IDKhoa,
       KhoaTho = @KhoaTho,
       MaCoSo  = (SELECT MaCoSo FROM dbo.DM_CSKCB WHERE ID = @IDCoSo);
GO

-- Ma benh nhan de go thu: lay mot ma CO THAT (nhanh NHAN) va bia mot ma khong
-- co (nhanh TU CHOI).
SELECT NhanhNhan_MaBN_co_that = MaBN
FROM dbo.DM_BenhNhanCoSo WHERE IDCoSo = 8 AND MaBN IS NOT NULL;
GO

-- ---------------------------------------------------------------------------
-- Doi soat sau khi go thu — day la cau tra loi cho "co so day bao nhieu, cong
-- nhan may, rot vi ly do gi".
-- ---------------------------------------------------------------------------
SELECT  cs.MaCoSo,
        l.Endpoint,
        l.KetQua,
        l.LyDo,
        SoLuot = COUNT(*),
        LanCuoi = MAX(l.NgayTao)
FROM dbo.HT_LogApiCoSo l
LEFT JOIN dbo.DM_CSKCB cs ON cs.ID = l.IDCoSo
GROUP BY cs.MaCoSo, l.Endpoint, l.KetQua, l.LyDo
ORDER BY LanCuoi DESC;
GO

-- ---------------------------------------------------------------------------
-- DON SAU KHI NGHIEM THU: tat khoa thu nghiem (khong xoa, de con dau vet).
-- ---------------------------------------------------------------------------
-- UPDATE dbo.HT_KhoaApiCoSo SET Active = 0
-- WHERE TenKhoa = N'Khoa thu nghiem Dot 2 (xoa sau khi nghiem thu)';
