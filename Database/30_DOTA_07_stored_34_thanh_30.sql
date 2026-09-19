/* =============================================================================
   DOT A - buoc A9: stored 34 -> 30.
   Chay SAU A02 (DM_CSKCB moi) + A04 (xoa 3 bang) + A05 (bo 16 cot) + A06.

   BO  (4): HT_TaiKhoanDoiTac_Save, HT_TaiKhoanDoiTac_XoaMaXacNhan,
            HT_ThietBi_Save, HT_KhoaApiCoSo_Save
   GOP (2): DM_CSKCB_QuangCao_Save, HT_KhoFtpCoSo_Save  -> vao DM_CSKCB_Save
   DOI TEN (1): HT_KhoFtpCoSo_GhiNhanThuDat -> DM_CSKCB_GhiNhanThuDatFtp
   SUA (6): DM_CSKCB_Save, DM_CSKCB_Delete, DM_CSKCB_TopQuangCao,
            HT_LogApiCoSo_Ghi, HT_PushDangKy_Save, HT_TaiKhoan_Save/_Loc,
            QL_DotKham_Save, QL_TaiLieuBenhNhan_Save

   Duong lui: bak2.StoredDinhNghia_A0 giu nguyen van 37 dinh nghia cu (A01).
   ========================================================================== */
SET NOCOUNT ON;
GO

/* ===========================================================================
   1. BO 4 STORED CUA CAC BANG DA CHET
   =========================================================================== */
IF OBJECT_ID('dbo.HT_TaiKhoanDoiTac_Save')          IS NOT NULL DROP PROCEDURE dbo.HT_TaiKhoanDoiTac_Save;
IF OBJECT_ID('dbo.HT_TaiKhoanDoiTac_XoaMaXacNhan')  IS NOT NULL DROP PROCEDURE dbo.HT_TaiKhoanDoiTac_XoaMaXacNhan;
IF OBJECT_ID('dbo.HT_ThietBi_Save')                 IS NOT NULL DROP PROCEDURE dbo.HT_ThietBi_Save;
IF OBJECT_ID('dbo.HT_KhoaApiCoSo_Save')             IS NOT NULL DROP PROCEDURE dbo.HT_KhoaApiCoSo_Save;
/* 2 stored duoc GOP vao DM_CSKCB_Save */
IF OBJECT_ID('dbo.DM_CSKCB_QuangCao_Save')          IS NOT NULL DROP PROCEDURE dbo.DM_CSKCB_QuangCao_Save;
IF OBJECT_ID('dbo.HT_KhoFtpCoSo_Save')              IS NOT NULL DROP PROCEDURE dbo.HT_KhoFtpCoSo_Save;
/* doi ten => bo ban cu, tao ban moi o muc 3 */
IF OBJECT_ID('dbo.HT_KhoFtpCoSo_GhiNhanThuDat')     IS NOT NULL DROP PROCEDURE dbo.HT_KhoFtpCoSo_GhiNhanThuDat;
GO

/* ===========================================================================
   2. DM_CSKCB_Save — mot cho luu CA 4 nhom (goc / quang cao / ket noi / FTP)
   ---------------------------------------------------------------------------
   🔴 LUAT GIU BI MAT: @KhoaBam / @Ftp_MatKhau / @KetNoi_KhoaGoiHIS truyen NULL
   thi GIU NGUYEN gia tri cu, KHONG ghi de NULL. Man Admin khong hien cac o do
   (Ftp_MatKhau luu THO - co y, vi FTP can dang nhap lai duoc) nen no khong gui
   lai gia tri; neu ghi de NULL thi moi lan Admin sua dia chi la mat ket noi.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_CSKCB_Save
    @ID                    bigint,
    @MaCoSo                varchar(10),
    @TenCoSo               nvarchar(200),
    @Slug                  varchar(100),
    @IDNhomCS              bigint         = NULL,
    @DiaChi                nvarchar(255)  = NULL,
    @Tinh                  int            = NULL,
    @PhuongXa              int            = NULL,
    @SDT                   varchar(20)    = NULL,
    @Email                 varchar(100)   = NULL,
    @AnhBia                nvarchar(500)  = NULL,
    @Logo                  nvarchar(max)  = NULL,
    @HienThiCongKhai       bit            = 0,
    @QcSoTienDaTra         decimal(15,0)  = NULL,   -- so nguyen VND la CO Y (I-01)
    @QcNoiDung             nvarchar(max)  = NULL,
    @QcAnh                 nvarchar(max)  = NULL,
    @IDCongTy              bigint         = NULL,
    @KetNoi_UrlChuyenHuong nvarchar(255)  = NULL,
    @KetNoi_BaseUrlHIS     nvarchar(255)  = NULL,
    @KetNoi_KhoaGoiHIS     nvarchar(500)  = NULL,   -- NULL = giu nguyen
    @KetNoi_Active         bit            = 1,
    @KhoaBam               varbinary(32)  = NULL,   -- NULL = giu nguyen
    @Khoa_NgayHetHan       datetime       = NULL,
    @Ftp_Host              nvarchar(200)  = NULL,
    @Ftp_TaiKhoan          nvarchar(100)  = NULL,
    @Ftp_MatKhau           nvarchar(200)  = NULL,   -- NULL = giu nguyen
    @Ftp_ThuMucGoc         nvarchar(200)  = NULL,
    @Ftp_Active            bit            = 0,
    @ResultCode            int            OUTPUT,
    @ResultMessage         nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @ID = 0
        BEGIN
            /* 🔴 Chot 41 (giu tu HT_KhoFtpCoSo_Save): kho MOI thi chua the da
               "Thu ket noi dat" => ep Ftp_Active = 0, Ftp_NgayThuDat = NULL. */
            INSERT INTO dbo.DM_CSKCB
                (MaCoSo, TenCoSo, Slug, IDNhomCS, DiaChi, Tinh, PhuongXa, SDT, Email,
                 AnhBia, Logo, HienThiCongKhai, QcSoTienDaTra, QcNoiDung, QcAnh, IDCongTy,
                 KetNoi_UrlChuyenHuong, KetNoi_BaseUrlHIS, KetNoi_KhoaGoiHIS, KetNoi_Active,
                 KhoaBam, Khoa_NgayCap, Khoa_NgayHetHan,
                 Ftp_Host, Ftp_TaiKhoan, Ftp_MatKhau, Ftp_ThuMucGoc, Ftp_Active, Ftp_NgayThuDat, NgayTao)
            VALUES
                (@MaCoSo, @TenCoSo, @Slug, @IDNhomCS, @DiaChi, @Tinh, @PhuongXa, @SDT, @Email,
                 @AnhBia, @Logo, @HienThiCongKhai, @QcSoTienDaTra, @QcNoiDung, @QcAnh, @IDCongTy,
                 @KetNoi_UrlChuyenHuong, @KetNoi_BaseUrlHIS, @KetNoi_KhoaGoiHIS, @KetNoi_Active,
                 @KhoaBam,
                 CASE WHEN @KhoaBam IS NULL THEN NULL ELSE GETDATE() END,
                 @Khoa_NgayHetHan,
                 @Ftp_Host, @Ftp_TaiKhoan, @Ftp_MatKhau, ISNULL(@Ftp_ThuMucGoc, N''), 0, NULL,
                 GETDATE());
            SET @ID = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @ID)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Cơ sở y tế không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            /* ================= 🔴 Chot 41 — giu nguyen luat cu cua kho FTP =====
               Truoc dot A luat nay nam o HT_KhoFtpCoSo_Save. Gop bang thi luat
               phai theo, khong duoc bay hoi: chua "Thu ket noi DAT" thi KHONG
               duoc bat Ftp_Active; va doi Host/TaiKhoan/MatKhau/ThuMucGoc la lan
               thu cu HET HIEU LUC, phai thu lai.
               Khac ban cu mot cho: @Ftp_MatKhau = NULL nghia la GIU NGUYEN (man
               Admin khong hien lai mat khau tho), nen NULL KHONG tinh la doi. */
            DECLARE @ThuDatCu  datetime = NULL;
            DECLARE @DoiKetNoi bit      = 0;

            SELECT @ThuDatCu   = Ftp_NgayThuDat,
                   @DoiKetNoi  = CASE
                       WHEN ISNULL(Ftp_Host,      N'') =  ISNULL(@Ftp_Host,     N'')
                        AND ISNULL(Ftp_ThuMucGoc, N'') =  ISNULL(@Ftp_ThuMucGoc, N'')
                        /* 2 o bi mat: NULL = khong gui lai = KHONG PHAI doi */
                        AND (@Ftp_TaiKhoan IS NULL OR ISNULL(Ftp_TaiKhoan, N'') = @Ftp_TaiKhoan)
                        AND (@Ftp_MatKhau  IS NULL OR ISNULL(Ftp_MatKhau,  N'') = @Ftp_MatKhau)
                       THEN 0 ELSE 1 END
            FROM dbo.DM_CSKCB WHERE ID = @ID;

            DECLARE @ThuDatMoi datetime = CASE WHEN @DoiKetNoi = 1 THEN NULL ELSE @ThuDatCu END;

            IF @Ftp_Active = 1 AND @ThuDatMoi IS NULL
            BEGIN
                SET @ResultCode = 6;
                SET @ResultMessage = N'Phải Thử kết nối kho đạt trước khi bật.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;
            /* ================================================================ */

            UPDATE dbo.DM_CSKCB
               SET MaCoSo = @MaCoSo, TenCoSo = @TenCoSo, Slug = @Slug, IDNhomCS = @IDNhomCS,
                   DiaChi = @DiaChi, Tinh = @Tinh, PhuongXa = @PhuongXa,
                   SDT = @SDT, Email = @Email, AnhBia = @AnhBia, Logo = @Logo,
                   HienThiCongKhai = @HienThiCongKhai, QcSoTienDaTra = @QcSoTienDaTra,
                   QcNoiDung = @QcNoiDung, QcAnh = @QcAnh, IDCongTy = @IDCongTy,
                   KetNoi_UrlChuyenHuong = @KetNoi_UrlChuyenHuong,
                   KetNoi_BaseUrlHIS     = @KetNoi_BaseUrlHIS,
                   /* 3 cot bi mat: NULL = GIU NGUYEN, khong phai xoa */
                   KetNoi_KhoaGoiHIS     = ISNULL(@KetNoi_KhoaGoiHIS, KetNoi_KhoaGoiHIS),
                   KetNoi_Active         = @KetNoi_Active,
                   KhoaBam               = ISNULL(@KhoaBam, KhoaBam),
                   Khoa_NgayCap          = CASE WHEN @KhoaBam IS NULL THEN Khoa_NgayCap ELSE GETDATE() END,
                   /* 🔴 Man Admin KHONG co o nhap han khoa => luon gui NULL.
                      Ghi thang la moi lan luu co so lai xoa trang han khoa API. */
                   Khoa_NgayHetHan       = ISNULL(@Khoa_NgayHetHan, Khoa_NgayHetHan),
                   Ftp_Host              = @Ftp_Host,
                   /* o bi mat, man Sua de trong => NULL = giu nguyen */
                   Ftp_TaiKhoan          = ISNULL(@Ftp_TaiKhoan, Ftp_TaiKhoan),
                   Ftp_MatKhau           = ISNULL(@Ftp_MatKhau, Ftp_MatKhau),
                   Ftp_ThuMucGoc         = ISNULL(@Ftp_ThuMucGoc, N''),
                   Ftp_Active            = @Ftp_Active,
                   Ftp_NgayThuDat        = @ThuDatMoi,
                   NgayCapNhat           = GETDATE()
             WHERE ID = @ID;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        /* Rang buoc duy nhat nam o DB chu khong o day (W-01). Dich loi cua SQL
           Server sang thong diep nguoi dung doc duoc. */
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = CASE
                WHEN ERROR_MESSAGE() LIKE '%UK_DM_CSKCB_MaCoSo%'  THEN N'Mã cơ sở đã tồn tại.'
                WHEN ERROR_MESSAGE() LIKE '%UK_DM_CSKCB_Slug%'    THEN N'Đường dẫn (slug) đã được dùng cho cơ sở khác.'
                WHEN ERROR_MESSAGE() LIKE '%UK_DM_CSKCB_KhoaBam%' THEN N'Khoá API này đã được cấp cho cơ sở khác.'
                ELSE N'Dữ liệu bị trùng.' END;
            RETURN;
        END;
        THROW;
    END CATCH;
END;
GO

/* ===========================================================================
   3. DM_CSKCB_GhiNhanThuDatFtp — thay HT_KhoFtpCoSo_GhiNhanThuDat
   Van tach rieng khoi _Save: khong ai duoc bat Ftp_Active bang cach tu ghi
   NgayThuDat kem trong mot luot luu.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_CSKCB_GhiNhanThuDatFtp
    @IDCoSo        bigint,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @IDCoSo AND Ftp_Host IS NOT NULL)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Cơ sở chưa có cấu hình kho để ghi nhận.';
            RETURN;
        END;

        UPDATE dbo.DM_CSKCB SET Ftp_NgayThuDat = GETDATE() WHERE ID = @IDCoSo;

        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ===========================================================================
   4. DM_CSKCB_Delete
   🔴 Ban cu DANG HONG SAN: no DELETE dbo.QL_LichSuKham — bang do da bi xoa tu
   truoc, nen moi lan xoa co so la nem loi. Sua luon trong dot nay.
   Ban cu cung KHONG xoa QL_DotKham / QL_TaiLieuBenhNhan (2 bang co FK tro vao
   DM_BenhNhanCoSo va DM_CSKCB) => phai xoa theo dung thu tu phu thuoc.
   GIU DM_CSKCB_CapQuangCao: quan he 1:N, bang nay VAN SONG.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_CSKCB_Delete
    @ID BIGINT,
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        IF @ID IS NULL OR @ID <= 0 OR NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @ID)
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage = N'Không tìm thấy cơ sở y tế.';
            RETURN;
        END;

        BEGIN TRANSACTION;

        /* con truoc, cha sau */
        DELETE FROM dbo.QL_TaiLieuBenhNhan   WHERE IDCoSo = @ID;
        DELETE FROM dbo.QL_DotKham           WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_BenhNhanCoSo      WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_GioLamViec  WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_CapQuangCao WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_NoiDung     WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB             WHERE ID     = @ID;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ===========================================================================
   5. DM_CSKCB_TopQuangCao — quang cao gio nam ngay tren DM_CSKCB
   GIU JOIN DM_CSKCB_CapQuangCao (cong Cap=1 AND Active=1) — bang 1:N van song.
   🔴 c.QuangCao la SO TIEN DA TRA, la khoa xep hang => QcSoTienDaTra DESC.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_CSKCB_TopQuangCao
    @SoLuong int = 5
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@SoLuong)
           c.TenCoSo,
           ISNULL(c.QcNoiDung, N'') AS NoiDung,
           c.QcAnh                  AS Img
    FROM dbo.DM_CSKCB c
    JOIN dbo.DM_CSKCB_CapQuangCao q ON q.IDCoSo = c.ID AND q.Cap = 1 AND q.Active = 1
    WHERE c.HienThiCongKhai = 1
    ORDER BY c.QcSoTienDaTra DESC;
END;
GO

/* ===========================================================================
   6. HT_LogApiCoSo_Ghi
   🔴 CHO DE BO SOT NHAT CA DOT: cot IDKhoa va bang HT_KhoaApiCoSo deu khong con.
   Go @IDKhoa, go cot khoi INSERT, va GO HAN cau UPDATE ... SET NgayDungCuoi.
   Thieu buoc nay thi NHAT KY API CHET TRONG IM LANG sau migration.
   Van KHONG dat FK sang DM_CSKCB: nhat ky phai ghi duoc CA khi khoa sai va
   chua xac dinh duoc co so nao.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.HT_LogApiCoSo_Ghi
    @IDCoSo     bigint       = NULL,
    @Endpoint   varchar(100),
    @MaBN       varchar(20)  = NULL,
    @MaNguonHIS varchar(50)  = NULL,
    @KetQua     varchar(20),
    @LyDo       varchar(50)  = NULL,
    @SoLuong    int          = NULL,
    @IpGoi      varchar(45)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.HT_LogApiCoSo
        (IDCoSo, Endpoint, MaBN, MaNguonHIS, KetQua, LyDo, SoLuong, IpGoi, NgayTao)
    VALUES
        (@IDCoSo, @Endpoint, @MaBN, @MaNguonHIS, @KetQua, @LyDo, @SoLuong, @IpGoi, GETDATE());
END;
GO

/* ===========================================================================
   7. HT_PushDangKy_Save — bo @MaThietBi / @IDThietBi (bang HT_ThietBi da bo)
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.HT_PushDangKy_Save
    @IDTaiKhoan    bigint,
    @Endpoint      nvarchar(1000),
    @P256dh        nvarchar(500),
    @Auth          nvarchar(200),
    @IDDangKy      bigint         OUTPUT,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDDangKy = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @IDDangKy = ID
        FROM dbo.HT_PushDangKy WITH (UPDLOCK, HOLDLOCK)
        WHERE IDTaiKhoan = @IDTaiKhoan AND Endpoint = @Endpoint;

        IF @IDDangKy IS NULL
        BEGIN
            INSERT INTO dbo.HT_PushDangKy (IDTaiKhoan, Endpoint, P256dh, Auth, ThoiGian)
            VALUES (@IDTaiKhoan, @Endpoint, @P256dh, @Auth, GETDATE());
            SET @IDDangKy = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE dbo.HT_PushDangKy
               SET P256dh = @P256dh, Auth = @Auth, ThoiGian = GETDATE()
             WHERE ID = @IDDangKy;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() = 547
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Tài khoản không tồn tại.';
            RETURN;
        END;
        THROW;
    END CATCH;
END;
GO

/* ===========================================================================
   8. HT_TaiKhoan_Save — thi hanh not ADR 0019: bo @IDBenhNhan va cot IDBenhNhan
   Chieu dung la DM_BenhNhan.IDTaiKhoan (1-N).
   @MatKhauNoiBoDaBam: truyen NULL = giu nguyen (tang C# bam roi moi truyen vao,
   thu tuc nay KHONG BAO GIO tu bam — ADR 0009).
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.HT_TaiKhoan_Save
    @ID                  bigint,
    @SDT                 varchar(20),
    @Email               nvarchar(50) = NULL,
    @Role                varchar(20),
    @MatKhauNoiBoDaBam   varchar(255) = NULL,
    @IDTaiKhoan          bigint         OUTPUT,
    @ResultCode          int            OUTPUT,
    @ResultMessage       nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDTaiKhoan = NULL;

    BEGIN TRY
        IF @Role NOT IN ('Admin','DoiTac','BenhNhan')
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Vai trò không hợp lệ.';
            RETURN;
        END;

        BEGIN TRANSACTION;

        IF @ID IS NULL OR @ID = 0
            SELECT @IDTaiKhoan = ID FROM dbo.HT_TaiKhoan WITH (UPDLOCK, HOLDLOCK) WHERE SDT = @SDT;
        ELSE
            SET @IDTaiKhoan = @ID;

        IF @IDTaiKhoan IS NULL
        BEGIN
            INSERT INTO dbo.HT_TaiKhoan (SDT, Email, Role, MatKhauNoiBo)
            VALUES (@SDT, @Email, @Role, @MatKhauNoiBoDaBam);
            SET @IDTaiKhoan = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.HT_TaiKhoan WHERE ID = @IDTaiKhoan)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Tài khoản không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE dbo.HT_TaiKhoan
               SET SDT   = @SDT,
                   Email = ISNULL(@Email, Email),
                   Role  = @Role,
                   /* NULL = khong doi mat khau, khong phai xoa mat khau */
                   MatKhauNoiBo = ISNULL(@MatKhauNoiBoDaBam, MatKhauNoiBo)
             WHERE ID = @IDTaiKhoan;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Số điện thoại này đã có tài khoản.';
            RETURN;
        END;
        THROW;
    END CATCH;
END;
GO

/* ===========================================================================
   9. HT_TaiKhoan_Loc
   Hai thu bi bo cung luc:
     - tk.IDBenhNhan (6 cho) => chi con noi qua DM_BenhNhan.IDTaiKhoan hoac SDT
     - HT_TaiKhoanDoiTac (2 nhanh loc @LoaiCS) => bang da chet cung bo man
       doi tac UB. Loc theo co so gio di duy nhat qua DM_BenhNhanCoSo.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.HT_TaiKhoan_Loc
    @Trang    int           = 1,
    @SoDong   int           = 50,
    @SDT      varchar(20)   = NULL,
    @CCCD     varchar(20)   = NULL,
    @MaBN     varchar(50)   = NULL,
    @Role     varchar(20)   = NULL,
    @LoaiCS   varchar(50)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @Trang = ISNULL(@Trang, 1);
    IF @Trang < 1 SET @Trang = 1;
    SET @SoDong = ISNULL(@SoDong, 50);
    IF @SoDong NOT IN (20, 50, 100, 500) SET @SoDong = 50;

    SET @SDT    = NULLIF(LTRIM(RTRIM(@SDT)), '');
    SET @CCCD   = NULLIF(LTRIM(RTRIM(@CCCD)), '');
    SET @MaBN   = NULLIF(LTRIM(RTRIM(@MaBN)), '');
    SET @Role   = NULLIF(LTRIM(RTRIM(@Role)), '');
    SET @LoaiCS = NULLIF(LTRIM(RTRIM(@LoaiCS)), '');

    DECLARE @IdCoSoFilter bigint = NULL;
    DECLARE @MaNhomFilter varchar(50) = NULL;

    IF @LoaiCS IS NOT NULL
    BEGIN
        IF LOWER(@LoaiCS) LIKE 'cs:%'
            SET @IdCoSoFilter = TRY_CAST(SUBSTRING(@LoaiCS, 4, 20) AS bigint);
        ELSE
            SET @MaNhomFilter = LOWER(@LoaiCS);
    END

    -- 1. Lọc các IdTaiKhoan thỏa mãn điều kiện
    CREATE TABLE #TaiKhoanLoc (ID bigint PRIMARY KEY);

    INSERT INTO #TaiKhoanLoc (ID)
    SELECT tk.ID
    FROM dbo.HT_TaiKhoan tk WITH (NOLOCK)
    WHERE
        -- Lọc Vai trò
        (@Role IS NULL
         OR (@Role = 'Admin'    AND tk.Role = 'Admin')
         OR (@Role = 'BenhNhan' AND ISNULL(tk.Role, '') <> 'Admin'))

        -- Lọc SĐT
        AND (@SDT IS NULL
             OR tk.SDT LIKE '%' + @SDT + '%'
             OR EXISTS (
                 SELECT 1 FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                 WHERE b.IDTaiKhoan = tk.ID AND b.SDT LIKE '%' + @SDT + '%'
             ))

        -- Lọc CCCD
        AND (@CCCD IS NULL
             OR EXISTS (
                 SELECT 1 FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                 WHERE (b.IDTaiKhoan = tk.ID OR (tk.SDT IS NOT NULL AND b.SDT = tk.SDT))
                   AND b.CCCD LIKE '%' + @CCCD + '%'
             ))

        -- Lọc Mã bệnh nhân tại cơ sở
        AND (@MaBN IS NULL
             OR EXISTS (
                 SELECT 1
                 FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                 JOIN dbo.DM_BenhNhanCoSo cs WITH (NOLOCK) ON cs.IDBenhNhan = b.ID
                 WHERE (b.IDTaiKhoan = tk.ID OR (tk.SDT IS NOT NULL AND b.SDT = tk.SDT))
                   AND cs.MaBN LIKE '%' + @MaBN + '%'
             ))

        -- Lọc Loại cơ sở hoặc cơ sở cụ thể
        AND (
            (@IdCoSoFilter IS NULL AND @MaNhomFilter IS NULL)
            OR (@IdCoSoFilter IS NOT NULL AND EXISTS (
                    SELECT 1
                    FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                    JOIN dbo.DM_BenhNhanCoSo cs WITH (NOLOCK) ON cs.IDBenhNhan = b.ID
                    WHERE (b.IDTaiKhoan = tk.ID OR (tk.SDT IS NOT NULL AND b.SDT = tk.SDT))
                      AND cs.IDCoSo = @IdCoSoFilter
                ))
            OR (@MaNhomFilter IS NOT NULL AND EXISTS (
                    SELECT 1
                    FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                    JOIN dbo.DM_BenhNhanCoSo cs WITH (NOLOCK) ON cs.IDBenhNhan = b.ID
                    JOIN dbo.DM_CSKCB c        WITH (NOLOCK) ON c.ID  = cs.IDCoSo
                    JOIN dbo.DM_NhomCS n       WITH (NOLOCK) ON n.ID  = c.IDNhomCS
                    WHERE (b.IDTaiKhoan = tk.ID OR (tk.SDT IS NOT NULL AND b.SDT = tk.SDT))
                      AND LOWER(n.MaNhom) = @MaNhomFilter
                ))
        );

    DECLARE @TongSoDong int = (SELECT COUNT(*) FROM #TaiKhoanLoc);

    -- 2. Phân trang đúng @SoDong dòng của trang đang xem
    CREATE TABLE #Trang (ID bigint PRIMARY KEY, ThuTu int);

    INSERT INTO #Trang (ID, ThuTu)
    SELECT ID, ROW_NUMBER() OVER (ORDER BY ID DESC)
    FROM #TaiKhoanLoc
    ORDER BY ID DESC
    OFFSET (@Trang - 1) * @SoDong ROWS FETCH NEXT @SoDong ROWS ONLY;

    -- Result Set 1: Danh sách tài khoản đã phân trang
    SELECT tk.ID, tk.SDT, tk.Email, tk.Role, tk.MatKhauNoiBo, tk.NgayTao,
           @TongSoDong AS TongSoDong
    FROM #Trang tr
    JOIN dbo.HT_TaiKhoan tk WITH (NOLOCK) ON tr.ID = tk.ID
    ORDER BY tr.ThuTu;

    -- Result Set 2: Hồ sơ bệnh nhân kèm theo của các tài khoản trong trang
    SELECT tr.ThuTu,
           b.ID    AS IdBenhNhan,
           tr.ID   AS IdTaiKhoan,
           b.TenBN, b.CCCD, b.SDT, b.Email, b.DiaChi, b.NgaySinh,
           CAST(b.GioiTinh AS varchar(10)) AS GioiTinhStr,
           cs.MaBN,
           kcb.TenCoSo,
           cs.ID     AS IdHoSoCoSo,
           cs.IDCoSo,
           (SELECT COUNT(*) FROM dbo.DM_BenhNhanCoSo c2 WITH (NOLOCK)
             WHERE c2.IDBenhNhan = b.ID AND c2.MaBN IS NOT NULL AND c2.MaBN <> '') AS SoCoSo
    FROM #Trang tr
    JOIN dbo.HT_TaiKhoan tk WITH (NOLOCK) ON tr.ID = tk.ID
    JOIN dbo.DM_BenhNhan  b  WITH (NOLOCK)
         ON b.IDTaiKhoan = tr.ID
         OR (b.IDTaiKhoan IS NULL AND tk.SDT IS NOT NULL AND b.SDT = tk.SDT)
    LEFT JOIN dbo.DM_BenhNhanCoSo cs  WITH (NOLOCK) ON cs.IDBenhNhan = b.ID
    LEFT JOIN dbo.DM_CSKCB        kcb WITH (NOLOCK) ON cs.IDCoSo = kcb.ID
    ORDER BY tr.ThuTu, b.ID,
             (CASE WHEN cs.MaBN IS NOT NULL AND cs.MaBN <> '' THEN 0 ELSE 1 END), cs.ID;

    DROP TABLE #Trang;
    DROP TABLE #TaiKhoanLoc;
END;
GO

/* ===========================================================================
   10. QL_DotKham_Save — GIU @MaBN / @NgayGioRa / @ChanDoan, chi khong INSERT nua
   🔴 Tien le V14: HIS dang gui 3 truong nay len. Bo THAM SO la vo ben HIS.
   Nhan roi BO.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.QL_DotKham_Save
    @IDCoSo         bigint,
    @IDBenhNhanCoSo bigint,
    @MaVaoVien      varchar(50),
    @MaBN           varchar(20)   = NULL,  -- nhan roi bo (suy ra qua IDBenhNhanCoSo)
    @NgayGioVao     datetime,
    @NgayGioRa      datetime      = NULL,  -- nhan roi bo (0 cho hien thi)
    @TenKhoa        nvarchar(255) = NULL,
    @TenBacSi       nvarchar(255) = NULL,
    @ChanDoan       nvarchar(MAX) = NULL,  -- nhan roi bo (0 cho hien thi)
    @IDDotKham      bigint         OUTPUT,
    @ResultCode     int            OUTPUT,
    @ResultMessage  nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDDotKham = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo WHERE ID = @IDBenhNhanCoSo AND IDCoSo = @IDCoSo)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Hồ sơ bệnh nhân không thuộc cơ sở này.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        /* 🔴 BAY T-SQL da lam MAT DU LIEU IM LANG (bat duoc 08/09 luc chay that):
           `SELECT @bien = cot` tren tap RONG thi bien GIU NGUYEN gia tri cu,
           KHONG thanh NULL. Dung bien CUC BO khai bao ngay tai day: no chac chan
           bat dau bang NULL. */
        DECLARE @IDCu bigint;

        SELECT @IDCu = ID
        FROM dbo.QL_DotKham WITH (UPDLOCK, HOLDLOCK)
        WHERE IDCoSo = @IDCoSo AND MaVaoVien = @MaVaoVien;

        IF @IDCu IS NULL
        BEGIN
            INSERT INTO dbo.QL_DotKham
                (IDCoSo, IDBenhNhanCoSo, MaVaoVien, NgayGioVao, TenKhoa, TenBacSi, NgayTao)
            VALUES
                (@IDCoSo, @IDBenhNhanCoSo, @MaVaoVien, @NgayGioVao, @TenKhoa, @TenBacSi, GETDATE());

            SET @IDDotKham = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            -- Day lai la chuyen binh thuong => cap nhat, khong bao loi trung.
            UPDATE dbo.QL_DotKham
            SET IDBenhNhanCoSo = @IDBenhNhanCoSo,
                NgayGioVao     = @NgayGioVao,
                TenKhoa        = @TenKhoa,
                TenBacSi       = @TenBacSi,
                NgayCapNhat    = GETDATE()
            WHERE ID = @IDCu;

            SET @IDDotKham = @IDCu;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ===========================================================================
   11. QL_TaiLieuBenhNhan_Save — GIU @MaBN / @DungLuongByte / @GhiChu,
   chi khong INSERT nua (tien le V14). Moi luat con lai GIU NGUYEN, ke ca
   `AND LaBanMoiNhat = 1` o cau tinh PhienBan (ADR 0034 — doc truoc khi go).
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.QL_TaiLieuBenhNhan_Save
    @ID             BIGINT,
    @IDCoSo         BIGINT,
    @IDBenhNhanCoSo BIGINT = NULL,
    @MaBN           NVARCHAR(50)  = NULL,  -- nhan roi bo
    @LoaiTaiLieu    NVARCHAR(50),
    @TenTaiLieu     NVARCHAR(255),
    -- Tran 2000 (file 27). 🔴 Con so nay nam o SAU cho -- doi mot cho ma quen
    -- cac cho kia la quay lai dung benh CAT IM LANG ma chot chan sinh ra de chong.
    @DuongDanFtp    NVARCHAR(2000),
    @DungLuongByte  BIGINT        = NULL,  -- nhan roi bo
    @NgayKham       DATETIME      = NULL,
    @GhiChu         NVARCHAR(MAX) = NULL,  -- nhan roi bo
    @MaNguonHIS     VARCHAR(50)   = NULL,
    @BamNoiDung     CHAR(64)      = NULL,
    /* Kho chua tep. Mac dinh N'CONG' => moi cho goi cu giu nguyen hanh vi.
       Chi che do Tro duong ben HIS truyen N'COSO'. */
    @NguonKho       NVARCHAR(20)  = N'CONG',
    @IDTaiLieu      BIGINT OUTPUT,
    @ResultCode     INT OUTPUT,
    @ResultMessage  NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDTaiLieu = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @IDCoSo)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Cơ sở khám chữa bệnh không tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- Chot 3: khong co ho so noi thi TU CHOI, khong luu.
        IF @IDBenhNhanCoSo IS NULL
           OR NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                          WHERE ID = @IDBenhNhanCoSo AND IDCoSo = @IDCoSo)
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Mã bệnh nhân này chưa có hồ sơ nào nhận tại cổng.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @ID = 0
        BEGIN
            DECLARE @PhienBan int = 1;

            IF @MaNguonHIS IS NOT NULL
            BEGIN
                SELECT @PhienBan = ISNULL(MAX(PhienBan), 0) + 1
                FROM dbo.QL_TaiLieuBenhNhan WITH (UPDLOCK, HOLDLOCK)
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu AND MaNguonHIS = @MaNguonHIS
                  AND LaBanMoiNhat = 1;   -- 29: xem ADR 0034 truoc khi go dong nay

                UPDATE dbo.QL_TaiLieuBenhNhan
                SET LaBanMoiNhat = 0
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu
                  AND MaNguonHIS = @MaNguonHIS AND LaBanMoiNhat = 1;
            END;

            INSERT INTO dbo.QL_TaiLieuBenhNhan (
                IDCoSo, IDBenhNhanCoSo, LoaiTaiLieu, TenTaiLieu, DuongDanFtp,
                NgayKham, MaNguonHIS, BamNoiDung, NguonKho,
                PhienBan, LaBanMoiNhat, NgayTao)
            VALUES (
                @IDCoSo, @IDBenhNhanCoSo, @LoaiTaiLieu, @TenTaiLieu, @DuongDanFtp,
                @NgayKham, @MaNguonHIS, @BamNoiDung,
                ISNULL(NULLIF(LTRIM(RTRIM(@NguonKho)), N''), N'CONG'),
                @PhienBan, 1, GETDATE());

            SET @IDTaiLieu = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.QL_TaiLieuBenhNhan WHERE ID = @ID)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Tài liệu không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE dbo.QL_TaiLieuBenhNhan
            SET IDCoSo = @IDCoSo, IDBenhNhanCoSo = @IDBenhNhanCoSo,
                LoaiTaiLieu = @LoaiTaiLieu, TenTaiLieu = @TenTaiLieu,
                DuongDanFtp = @DuongDanFtp, NgayKham = @NgayKham
            WHERE ID = @ID;

            SET @IDTaiLieu = @ID;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH
END;
GO

/* ===========================================================================
   TU KIEM CUOI FILE
   =========================================================================== */
SELECT 'A07' AS Buoc,
       (SELECT COUNT(*) FROM sys.objects WHERE type = 'P' AND is_ms_shipped = 0) AS SoStored;

SELECT 'A07 stored con tham chieu bang da chet' AS Buoc,
       SCHEMA_NAME(o.schema_id) + '.' + o.name AS Stored
FROM sys.objects o
WHERE o.type = 'P' AND o.is_ms_shipped = 0
  AND (OBJECT_DEFINITION(o.object_id) LIKE '%HT_KhoaApiCoSo%'
    OR OBJECT_DEFINITION(o.object_id) LIKE '%HT_KhoFtpCoSo%'
    OR OBJECT_DEFINITION(o.object_id) LIKE '%HT_ThietBi%'
    OR OBJECT_DEFINITION(o.object_id) LIKE '%HT_TaiKhoanDoiTac%'
    OR OBJECT_DEFINITION(o.object_id) LIKE '%DM_DoiTacApi%'
    OR OBJECT_DEFINITION(o.object_id) LIKE '%DM_CSKCB_QuangCao%'
    OR OBJECT_DEFINITION(o.object_id) LIKE '%DM_GioiTinh%'
    OR OBJECT_DEFINITION(o.object_id) LIKE '%QL_LichSuKham%');
GO
