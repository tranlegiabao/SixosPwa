/* =============================================================================
   12_NOI_HO_SO_THU.sql -- Noi mot ho so benh nhan de THU duong day
   DB: HIS_CSKH. Chay lai duoc.

   🔴🔴 CHI DUNG TREN MOI TRUONG THU. KHONG chay tren DB khach that.
   File nay LAM THAY viec cua benh nhan: binh thuong ho tu bam "Noi ho so" tren
   cong, cong hoi nguoc vao HIS roi tao dong DM_BenhNhanCoSo. O day ta tao tay
   dong do de co cai ma thu duong day ngay, khong phai dung ca luong dang ky.

   VI SAO CAN: chua ai noi ho so thi MOI cuoc day tai lieu / dot kham deu bi
   cong tra 409 CHUA_CO_NGUOI_NHAN (dung luat, ADR 0021) — man ben HIS se hien
   tat ca o tab "Cho nguoi nhan" va khong thu duoc gi.

   ⚠️ Phai chay voi QUOTED_IDENTIFIER ON (sqlcmd: them -I). DM_BenhNhanCoSo co
   unique index CO LOC nen thieu no la Msg 1934.
   ============================================================================= */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
GO

-- ─── SUA O DAY ───────────────────────────────────────────────────────────────
DECLARE @MaCoSo   varchar(50)  = '77121';          -- co so da bat KieuApi='HIS'
DECLARE @MaBN     varchar(20)  = '100992';         -- Ma BN BEN HIS
DECLARE @CCCD     varchar(20)  = '077094005943';   -- CCCD cua chinh benh nhan do
DECLARE @TenBN    nvarchar(500)= N'LÊ VIỆT HƯNG';
DECLARE @NgaySinh date         = '1994-01-12';
DECLARE @SDT      varchar(20)  = '0900000001';

/* SDT cua TAI KHOAN se so huu ho so nay. De trong (NULL) thi ho so dung mot
   minh, KHONG ai dang nhap vao xem duoc — day dung tai lieu len van thanh cong
   nhung khong benh nhan nao mo ra duoc, nen luc thu tren giao dien phai dat. */
DECLARE @SdtTaiKhoan varchar(20) = '0363982926';
-- ─────────────────────────────────────────────────────────────────────────────

DECLARE @IdCoSo bigint = (SELECT Id FROM dbo.DM_CSKCB WHERE MaCoSo = @MaCoSo);

IF @IdCoSo IS NULL
BEGIN
    RAISERROR (N'Chưa có cơ sở mã %s — chạy 10_SEED_COSO_HIS.sql trước.', 16, 1, @MaCoSo);
    RETURN;
END

/* --- 0. Tai khoan se so huu ho so ----------------------------------------- */
DECLARE @IdTaiKhoan bigint = NULL;

IF @SdtTaiKhoan IS NOT NULL
BEGIN
    SELECT @IdTaiKhoan = ID FROM dbo.HT_TaiKhoan WHERE SDT = @SdtTaiKhoan AND Role = 'BenhNhan';

    IF @IdTaiKhoan IS NULL
    BEGIN
        RAISERROR (N'Không có tài khoản bệnh nhân nào mang SĐT %s. Đăng ký trên cổng trước, hoặc để @SdtTaiKhoan = NULL.', 16, 1, @SdtTaiKhoan);
        RETURN;
    END
END

/* --- 1. Con nguoi (DM_BenhNhan) ------------------------------------------- */
DECLARE @IdBN bigint = (SELECT ID FROM dbo.DM_BenhNhan WHERE CCCD = @CCCD);

IF @IdBN IS NULL
BEGIN
    INSERT INTO dbo.DM_BenhNhan (CCCD, TenBN, SDT, NgaySinh, HoTenKhongDau, IDTaiKhoan, NgayTao)
    VALUES (@CCCD, @TenBN, @SDT, @NgaySinh, NULL, @IdTaiKhoan, GETDATE());

    SET @IdBN = SCOPE_IDENTITY();
    PRINT N'Da tao con nguoi moi trong DM_BenhNhan.';
END
ELSE
BEGIN
    /* Gan chu so huu neu ho so dang mo coi. KHONG cuop tu tai khoan khac:
       luat "ai khai truoc giu" (chot 3 Dot 1) — doi chu la dua benh an cua
       nguoi nay cho tai khoan nguoi kia. */
    IF @IdTaiKhoan IS NOT NULL
    BEGIN
        DECLARE @ChuHienTai bigint = (SELECT IDTaiKhoan FROM dbo.DM_BenhNhan WHERE ID = @IdBN);

        IF @ChuHienTai IS NULL
        BEGIN
            UPDATE dbo.DM_BenhNhan SET IDTaiKhoan = @IdTaiKhoan WHERE ID = @IdBN;
            PRINT N'Ho so dang mo coi — da gan cho tai khoan.';
        END
        ELSE IF @ChuHienTai <> @IdTaiKhoan
            PRINT N'!!! Ho so nay DA thuoc mot tai khoan KHAC — giu nguyen (ai khai truoc giu).';
        ELSE
            PRINT N'Ho so da thuoc dung tai khoan nay.';
    END
    ELSE
        PRINT N'Con nguoi da co san — dung lai.';
END

/* --- 2. Ho so tai co so (DM_BenhNhanCoSo) ---------------------------------
   DaMoTaiLieu = 1: "Cua tai lieu" da mo (chot 9 Dot 1, ADR 0020). Khong mo thi
   benh nhan van khong xem duoc tep du tai lieu day len thanh cong. */
IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo WHERE IDCoSo = @IdCoSo AND MaBN = @MaBN)
BEGIN
    INSERT INTO dbo.DM_BenhNhanCoSo
        (IDBenhNhan, IDCoSo, MaBN, NgayTao, TenBN_ChupTuHIS, NgaySinh_ChupTuHIS, DaMoTaiLieu)
    VALUES
        (@IdBN, @IdCoSo, @MaBN, GETDATE(), @TenBN, @NgaySinh, 1);

    PRINT N'Da noi ho so.';
END
ELSE
BEGIN
    UPDATE dbo.DM_BenhNhanCoSo SET DaMoTaiLieu = 1
     WHERE IDCoSo = @IdCoSo AND MaBN = @MaBN AND DaMoTaiLieu = 0;

    PRINT N'Ho so da noi tu truoc — chi bao dam Cua tai lieu dang mo.';
END

/* --- 3. Doi chieu ---------------------------------------------------------- */
SELECT c.MaCoSo + ' | MaBN=' + h.MaBN
     + ' | ' + ISNULL(h.TenBN_ChupTuHIS, N'(khong ten)')
     + ' | DaMoTaiLieu=' + CAST(h.DaMoTaiLieu AS varchar(2))
     + ' | TaiKhoan=' + ISNULL(tk.SDT, N'(chua gan - khong ai dang nhap xem duoc)') AS HoSoDaNoi
  FROM dbo.DM_BenhNhanCoSo h
  JOIN dbo.DM_CSKCB    c ON c.Id = h.IDCoSo
  JOIN dbo.DM_BenhNhan b ON b.ID = h.IDBenhNhan
  LEFT JOIN dbo.HT_TaiKhoan tk ON tk.ID = b.IDTaiKhoan
 WHERE h.IDCoSo = @IdCoSo AND h.MaBN = @MaBN;

/* Tat ca ho so cua tai khoan do — de biet man se hien bao nhieu */
IF @IdTaiKhoan IS NOT NULL
    SELECT 'Tai khoan ' + @SdtTaiKhoan + ' dang co '
         + CAST(COUNT(*) AS varchar(4)) + ' ho so tai co so nay' AS SoHoSo
      FROM dbo.DM_BenhNhanCoSo h
      JOIN dbo.DM_BenhNhan b ON b.ID = h.IDBenhNhan
     WHERE h.IDCoSo = @IdCoSo AND b.IDTaiKhoan = @IdTaiKhoan;

PRINT N'';
PRINT N'Gio man "Gui cho benh nhan" ben HIS se xep luot cua Ma BN nay vao tab';
PRINT N'"Gui duoc ngay" thay vi "Cho nguoi nhan", va bam Gui se ra 200.';
PRINT N'Dang nhap cong bang SDT tai khoan o tren, OTP 123456.';
GO

/* =============================================================================
   NGUOC LAI (go ho so thu):

   DECLARE @IdCoSo bigint = (SELECT Id FROM dbo.DM_CSKCB WHERE MaCoSo = '77121');
   DELETE FROM dbo.QL_TaiLieuBenhNhan WHERE IDCoSo = @IdCoSo AND MaBN = '100992';
   DELETE FROM dbo.QL_DotKham         WHERE IDCoSo = @IdCoSo AND MaBN = '100992';
   DELETE FROM dbo.DM_BenhNhanCoSo    WHERE IDCoSo = @IdCoSo AND MaBN = '100992';
   -- DM_BenhNhan giu lai: co the dang duoc ho so khac dung.
   ============================================================================= */
