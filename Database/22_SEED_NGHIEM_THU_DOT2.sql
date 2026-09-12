-- ============================================================================
-- 22 — SEED NGHIEM THU DOT 2 (che do TRO DUONG, ADR 0030)
--
-- Vi sao phai seed tay: duong GHI (HIS goi S00_UploadOnline dat NguonKho='COSO')
-- la viec cua DOT 3, chua co. Dot 2 chi lam duong DOC, nen dung 3 dong tay de
-- nghiem thu tron ven ma khong phai cho Dot 3 (chot 42).
--
-- CHAY TREN: HIS_CSKH (cong). Chay SAU 20_ va 21_.
-- 🔴 File nay CO GHI DU LIEU — nguoi dung tu chay, agent khong chay ho.
-- Don sach bang 22_ROLLBACK_NGHIEM_THU_DOT2.sql.
--
-- Bo doi nghiem thu (do 12/09, da xac minh co ca ho so ben cong lan file that
-- tren FTP):
--   MaBN 145691 / SDT dang nhap 0933270921
--   File that: 77121/CongVan/145691_TranThiNhung/VV2609100003/xquang.pdf
--
-- Ba dong cho ba ket qua KHAC NHAU — do la ca diem cua chot 38:
--   (1) tro DUNG        -> mo duoc PDF that            -> 200
--   (2) tro tep KHONG CO -> 404 + "Khong tim thay tep" -> KHONG co nut Thu lai
--   (3) tro host CHET    -> 502 + man PA-1             -> co nut Thu lai, <= 10s
-- ============================================================================

SET NOCOUNT ON;
GO

DECLARE @MaCoSo     nvarchar(50) = N'77121';
DECLARE @MaBN       nvarchar(50) = N'145691';
DECLARE @IDCoSo     bigint;
DECLARE @IDBnCoSo   bigint;

SELECT @IDCoSo = ID FROM dbo.DM_CSKCB WHERE MaCoSo = @MaCoSo;

IF @IDCoSo IS NULL
BEGIN
    RAISERROR(N'Khong tim thay co so 77121 trong DM_CSKCB. Dung lai.', 16, 1);
    RETURN;
END;

SELECT @IDBnCoSo = ID
FROM dbo.DM_BenhNhanCoSo
WHERE IDCoSo = @IDCoSo AND MaBN = @MaBN;

IF @IDBnCoSo IS NULL
BEGIN
    RAISERROR(N'Khong tim thay ho so 145691 tai co so 77121. Dung lai.', 16, 1);
    RETURN;
END;

-- 🔴 Cua tai lieu phai MO, khong thi TaiLieuApiController tra 404 o tang kiem
-- quyen va ta se tuong duong doc hong (chot 9 dot 1, ADR 0020).
IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo WHERE ID = @IDBnCoSo AND DaMoTaiLieu = 1)
    PRINT N'⚠ Ho so 145691 dang DONG cua tai lieu — mo cua roi hay nghiem thu (xem 08_MO_CUA_TAI_LIEU.sql).';

-- ---------------------------------------------------------------------------
-- Cau hinh kho cho co so 77121. Dung chinh tai khoan HIS dang ghi (chot 36).
-- 🔴 SUA 3 gia tri duoi cho dung moi truong truoc khi chay.
-- ---------------------------------------------------------------------------
DECLARE @Host     nvarchar(200) = N'118.69.34.247';
DECLARE @TaiKhoan nvarchar(100) = N'test';
-- 🔴 KHONG dien mat khau that vao day roi commit — file nay nam trong git.
-- Dien luc chay, xong thi tra lai placeholder.
DECLARE @MatKhau  nvarchar(200) = N'<DIEN_MAT_KHAU_FTP>';

IF @MatKhau = N'<DIEN_MAT_KHAU_FTP>'
BEGIN
    RAISERROR(N'Chua dien mat khau FTP that o bien @MatKhau. Dung lai.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.HT_KhoFtpCoSo WHERE IDCoSo = @IDCoSo)
BEGIN
    -- NgayThuDat dat san = nghiem thu khoi phai bam nut truoc. Phep thu #4/#5
    -- cua man cau hinh van kiem duoc nut Thu ket noi rieng.
    INSERT INTO dbo.HT_KhoFtpCoSo (IDCoSo, Host, TaiKhoan, MatKhau, ThuMucGoc, Active, NgayThuDat, NgayTao)
    VALUES (@IDCoSo, @Host, @TaiKhoan, @MatKhau, N'', 1, GETDATE(), GETDATE());
END
ELSE
BEGIN
    UPDATE dbo.HT_KhoFtpCoSo
    SET Host = @Host, TaiKhoan = @TaiKhoan, MatKhau = @MatKhau,
        ThuMucGoc = N'', Active = 1, NgayThuDat = GETDATE(), NgaySua = GETDATE()
    WHERE IDCoSo = @IDCoSo;
END;

-- ---------------------------------------------------------------------------
-- Ba dong tai lieu. TenTaiLieu mang tien to [NT2] de rollback nhan dang duoc,
-- khong phai do theo ID.
-- ---------------------------------------------------------------------------
DELETE FROM dbo.QL_TaiLieuBenhNhan
WHERE IDCoSo = @IDCoSo AND MaBN = @MaBN AND TenTaiLieu LIKE N'[[]NT2]%';

INSERT INTO dbo.QL_TaiLieuBenhNhan
    (IDCoSo, IDBenhNhanCoSo, MaBN, LoaiTaiLieu, TenTaiLieu, DuongDanFtp, NguonKho,
     DungLuongByte, NgayKham, MaNguonHIS, PhienBan, LaBanMoiNhat, NgayTao)
VALUES
    -- (1) tro DUNG -> phai mo duoc PDF that, HTTP 200, header Cache-Control: no-store
    (@IDCoSo, @IDBnCoSo, @MaBN, N'CDHA', N'[NT2] 1 - Tro dung, phai mo duoc',
     N'77121/CongVan/145691_TranThiNhung/VV2609100003/xquang.pdf', N'COSO',
     0, GETDATE(), N'NT2-OK', 1, 1, GETDATE()),

    -- (2) tep KHONG TON TAI -> FTP tra 550 -> 404, man khong co nut Thu lai
    (@IDCoSo, @IDBnCoSo, @MaBN, N'CDHA', N'[NT2] 2 - Tep khong ton tai, phai 404',
     N'77121/CongVan/145691_TranThiNhung/VV2609100003/khong-he-co-tep-nay.pdf', N'COSO',
     0, GETDATE(), N'NT2-404', 1, 1, GETDATE()),

    -- (3) HOST CHET -> khong noi duoc -> 502 + man PA-1, PHAI ra trong <= 10 giay.
    -- Dung dia chi TEST-NET-1 (RFC 5737) — khong bao gio co that, va la dia chi
    -- bi DROP im lang nen no test dung cai Timeout 10s chu khong phai "refused".
    -- Dong nay co y tro sang mot co so KHAC de host chet: xem ghi chu duoi.
    (@IDCoSo, @IDBnCoSo, @MaBN, N'CDHA', N'[NT2] 3 - Host chet, phai 502 trong 10s',
     N'77121/CongVan/145691_TranThiNhung/VV2609100003/xquang.pdf', N'COSO',
     0, GETDATE(), N'NT2-502', 1, 1, GETDATE());

PRINT N'✔ Da seed 3 dong [NT2] + cau hinh kho cho co so 77121.';
GO

-- ---------------------------------------------------------------------------
-- 🔴 CACH CHAY PHEP THU (3) — doc ky, dung bo qua.
--
-- Host nam o BANG KHO (theo co so), KHONG nam o tung dong tai lieu. Nen de thu
-- ca "host chet" phai doi host cua kho trong CHOC LAT, mo dong [NT2] 3, roi tra
-- host lai. Hai cau duoi lam viec do:
--
--   -- buoc 1: lam kho chet
--   UPDATE dbo.HT_KhoFtpCoSo SET Host = N'192.0.2.1'
--   WHERE IDCoSo = (SELECT ID FROM dbo.DM_CSKCB WHERE MaCoSo = N'77121');
--
--   -- buoc 2: tren cong, bam mo dong "[NT2] 3" => phai ra 502 + man PA-1 trong
--   --         <= 10 giay (khong phai 100 giay). Bam "Thu lai" phai goi lai that.
--
--   -- buoc 3: tra host that ve
--   UPDATE dbo.HT_KhoFtpCoSo SET Host = N'118.69.34.247'
--   WHERE IDCoSo = (SELECT ID FROM dbo.DM_CSKCB WHERE MaCoSo = N'77121');
--
-- 192.0.2.1 = TEST-NET-1 (RFC 5737), khong dinh tuyen toi dau ca => ket noi treo
-- cho den khi cham Timeout. Do dung la thu ta can do.
-- ---------------------------------------------------------------------------

-- Doi chieu sau khi seed:
-- SELECT ID, TenTaiLieu, NguonKho, DuongDanFtp FROM dbo.QL_TaiLieuBenhNhan
-- WHERE TenTaiLieu LIKE N'[[]NT2]%' ORDER BY ID;
