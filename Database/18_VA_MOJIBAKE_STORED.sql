-- ============================================================================
-- 18 - VA MOJIBAKE trong than 5 stored procedure
--
-- CHAY TREN: HIS_CSKH (co so du lieu cua CONG). KHONG chay ben HIS.
--
-- 🔴 FILE NAY GHI UTF-8 CO BOM. Dung go BOM di, va neu mo bang trinh soan thao
--    nao thi luu lai cung phai CO BOM. Do dung la cach 5 thu tuc duoi day hong
--    hom 09/09: file UTF-8 KHONG BOM => SSMS/sqlcmd doan la cp1252 => chuoi
--    tieng Viet di len CSDL thanh "Sa..." (mojibake), va benh nhan doc phai.
--    Chay bang sqlcmd thi nho co: sqlcmd -f 65001 -i 18_VA_MOJIBAKE_STORED.sql
--
-- CHAY LAI DUOC bao nhieu lan cung duoc: toan bo la CREATE OR ALTER, khong dung
-- toi mot dong du lieu nao, khong them/bot cot hay chi muc.
--
-- Nam thu tuc va o day va NGUON cua tung cai (day du ly le o ADR 0026):
--   (1) DM_BenhNhan_Save              - than lay tu CSDL dang chay
--   (2) DM_BenhNhan_SuaHoSo           - than lay tu CSDL + tra lai khoi 2601/2627
--   (3) DM_BenhNhanCoSo_GoNoi         - than lay tu 13_DOT4_DONG_VONG_DOC.sql
--   (4) DM_BenhNhanCoSo_DoiMocXemLich - than lay tu 13_DOT4_DONG_VONG_DOC.sql
--   (5) DM_CSKCB_GioLamViec_Save      - than lay tu P05__gio_lam_viec.sql
--
-- 🔴 (1) va (2) lay tu CSDL chu KHONG tu file, va do la CO Y: ban trong CSDL MOI
--    HON moi ban tren dia - no mang nhanh @laCccdKhongCo (can cuoc gia
--    11111111111 / 111111111111 => bo khop theo can cuoc, chuyen sang khop
--    ho ten khong dau + ngay sinh + gioi tinh) khong ton tai o bat ky file nguon
--    nao. Chep tu file "sach" tren dia la am tham XOA MAT tinh nang dang chay.
--    Tu file nay tro di, day la NGUON trong git cua ca nam thu tuc.
--
-- Chay xong PHAI doc cong kiem o cuoi file - no phai tra ve 0 dong.
-- ============================================================================

SET NOCOUNT ON;
GO

-- ----------------------------------------------------------------------------
-- DM_BenhNhan_Save
-- Than lay tu CSDL dang chay (ADR 0026). Giu nguyen nhanh @laCccdKhongCo.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_Save
    @CCCD          varchar(20),
    @TenBN         nvarchar(100),
    @SDT           varchar(20)   = NULL,
    @Email         varchar(100)  = NULL,
    @DiaChi        nvarchar(255) = NULL,
    @IDTaiKhoan    bigint        = NULL,
    @NgaySinh      datetime      = NULL,
    @HoTenKhongDau nvarchar(100) = NULL,
    @GioiTinh      varchar(10)   = NULL,
    @IDBenhNhan    bigint         OUTPUT,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDBenhNhan = NULL;

    BEGIN TRY
        DECLARE @cccdSach varchar(20) = NULLIF(LTRIM(RTRIM(@CCCD)), '');
        IF @cccdSach IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Thiếu số căn cước công dân.';
            RETURN;
        END;

        DECLARE @laCccdKhongCo bit = CASE WHEN @cccdSach IN ('11111111111', '111111111111') THEN 1 ELSE 0 END;

        BEGIN TRANSACTION;

        DECLARE @chuSoHuuHienTai bigint;

        IF @laCccdKhongCo = 1
        BEGIN
            -- Tìm theo nhân thân (Họ tên không dấu + Ngày sinh + Giới tính)
            SELECT TOP 1 @IDBenhNhan = ID, @chuSoHuuHienTai = IDTaiKhoan
            FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
            WHERE HoTenKhongDau = @HoTenKhongDau
              AND CAST(NgaySinh AS date) = CAST(@NgaySinh AS date)
              AND GioiTinh = @GioiTinh
              AND (@IDTaiKhoan IS NULL OR IDTaiKhoan = @IDTaiKhoan);

            IF @IDBenhNhan IS NULL
            BEGIN
                SELECT TOP 1 @IDBenhNhan = ID, @chuSoHuuHienTai = IDTaiKhoan
                FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
                WHERE HoTenKhongDau = @HoTenKhongDau
                  AND CAST(NgaySinh AS date) = CAST(@NgaySinh AS date)
                  AND GioiTinh = @GioiTinh;
            END;
        END
        ELSE
        BEGIN
            SELECT @IDBenhNhan = ID, @chuSoHuuHienTai = IDTaiKhoan
            FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
            WHERE CCCD = @cccdSach;
        END;

        IF @IDBenhNhan IS NULL
        BEGIN
            INSERT INTO dbo.DM_BenhNhan
                (CCCD, TenBN, SDT, Email, DiaChi, IDTaiKhoan, NgaySinh, HoTenKhongDau, GioiTinh)
            VALUES
                (@cccdSach, @TenBN, @SDT, @Email, @DiaChi, @IDTaiKhoan, @NgaySinh, @HoTenKhongDau, @GioiTinh);
            SET @IDBenhNhan = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            IF @IDTaiKhoan IS NOT NULL
               AND @chuSoHuuHienTai IS NOT NULL
               AND @chuSoHuuHienTai <> @IDTaiKhoan
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage =
                    CASE WHEN @laCccdKhongCo = 1
                         THEN N'Hồ sơ với thông tin này (Họ tên, ngày sinh, giới tính) đã được một tài khoản khác khai trước. Nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được xử lý.'
                         ELSE N'Số căn cước này đã được một tài khoản khác khai trước. Nếu đó là người thân của bạn, hãy nhờ họ vào mục "Hồ sơ của tôi" và xoá hồ sơ đó để nhả căn cước ra; nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được xử lý.'
                    END;
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            /* Chi ghi de bang gia tri thuc su co — dung xoa trang du lieu cu */
            UPDATE dbo.DM_BenhNhan
               SET TenBN         = ISNULL(NULLIF(LTRIM(RTRIM(@TenBN)), N''), TenBN),
                   SDT           = ISNULL(@SDT,   SDT),
                   Email         = ISNULL(@Email, Email),
                   DiaChi        = ISNULL(@DiaChi, DiaChi),
                   NgaySinh      = ISNULL(@NgaySinh, NgaySinh),
                   HoTenKhongDau = ISNULL(@HoTenKhongDau, HoTenKhongDau),
                   GioiTinh      = ISNULL(NULLIF(LTRIM(RTRIM(@GioiTinh)), ''), GioiTinh),
                   -- Chi nhan chu so huu khi dang bo trong.
                   IDTaiKhoan    = ISNULL(IDTaiKhoan, @IDTaiKhoan)
             WHERE ID = @IDBenhNhan;
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
            SET @ResultMessage = N'Số căn cước này đã thuộc về một người khác.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- ----------------------------------------------------------------------------
-- DM_BenhNhan_SuaHoSo
-- Than lay tu CSDL dang chay + TRA LAI khoi ERROR_NUMBER() IN (2601, 2627).
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_SuaHoSo
    @IDBenhNhan    bigint,
    @IDTaiKhoan    bigint,
    @CCCD          varchar(20)   = NULL,
    @TenBN         nvarchar(100) = NULL,
    @SDT           varchar(20)   = NULL,
    @NgaySinh      datetime      = NULL,
    @HoTenKhongDau nvarchar(100) = NULL,
    @GioiTinh      varchar(10)   = NULL,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @chuSoHuu bigint;

        SELECT @chuSoHuu = IDTaiKhoan
        FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
        WHERE ID = @IDBenhNhan;

        IF @chuSoHuu IS NULL AND NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan WHERE ID = @IDBenhNhan)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Không tìm thấy hồ sơ.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- 🔴 Cong chan. Thieu phep kiem nay thi go ID ho so nguoi khac vao la SUA
        -- duoc danh tinh cua ho — nang hon ca lo hong "xem duoc benh an".
        IF @chuSoHuu IS NULL OR @chuSoHuu <> @IDTaiKhoan
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Hồ sơ này không thuộc tài khoản của bạn.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DECLARE @daNoi bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                                                WHERE IDBenhNhan = @IDBenhNhan AND MaBN IS NOT NULL)
                                  THEN 1 ELSE 0 END;

        IF @daNoi = 1
        BEGIN
            -- Ho so da noi: bon o danh tinh doc tu HIS => khoa cung, chi cho SDT.
            UPDATE dbo.DM_BenhNhan
               SET SDT = @SDT
             WHERE ID = @IDBenhNhan;

            COMMIT TRANSACTION;
            SET @ResultCode = 1;
            SET @ResultMessage = N'OK';
            RETURN;
        END;

        DECLARE @cccdSach varchar(20) = NULLIF(LTRIM(RTRIM(@CCCD)), '');
        DECLARE @laCccdKhongCo bit = CASE WHEN @cccdSach IN ('11111111111', '111111111111') THEN 1 ELSE 0 END;

        IF @cccdSach IS NOT NULL AND @laCccdKhongCo = 0
           AND EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE CCCD = @cccdSach AND ID <> @IDBenhNhan)
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage =
                N'Số căn cước này đã được một tài khoản khác khai trước. ' +
                N'Nếu đó là người thân của bạn, hãy nhờ họ vào mục "Hồ sơ của tôi" và xoá hồ sơ đó để nhả căn cước ra; ' +
                N'nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được xử lý.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- Trường hợp không có CCCD (11111111111 hoặc 111111111111):
        -- Chuyển sang kiểm tra trùng nhân thân (Họ tên không dấu + Ngày sinh + Giới tính)
        IF @laCccdKhongCo = 1
           AND @HoTenKhongDau IS NOT NULL AND @NgaySinh IS NOT NULL AND @GioiTinh IS NOT NULL
           AND EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE HoTenKhongDau = @HoTenKhongDau
                          AND CAST(NgaySinh AS date) = CAST(@NgaySinh AS date)
                          AND GioiTinh = @GioiTinh
                          AND ID <> @IDBenhNhan
                          AND IDTaiKhoan IS NOT NULL
                          AND IDTaiKhoan <> @IDTaiKhoan)
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage =
                N'Hồ sơ với thông tin này (Họ tên, ngày sinh, giới tính) đã được một tài khoản khác khai trước. ' +
                N'Vui lòng kiểm tra lại hoặc liên hệ cơ sở khám chữa bệnh để được hỗ trợ.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- Ho so tu khai: sua duoc moi o. SDT gan THANG (khong ISNULL) de nguoi
        -- dung xoa trang duoc o do; bon o danh tinh thi giu ban cu khi ben goi
        -- de trong, vi de trong o day nghia la "khong dong toi".
        UPDATE dbo.DM_BenhNhan
           SET CCCD          = ISNULL(@cccdSach, CCCD),
               TenBN         = ISNULL(NULLIF(LTRIM(RTRIM(@TenBN)), N''), TenBN),
               SDT           = @SDT,
               NgaySinh      = ISNULL(@NgaySinh, NgaySinh),
               HoTenKhongDau = ISNULL(@HoTenKhongDau, HoTenKhongDau),
               GioiTinh      = ISNULL(NULLIF(LTRIM(RTRIM(@GioiTinh)), ''), GioiTinh)
         WHERE ID = @IDBenhNhan;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        -- Nam sửa: trả lại khối bắt trùng khoá mà bản triển khai tay đã đánh rơi.
        -- Thiếu nó thì đua tranh trùng CCCD rơi xuống ResultCode 99 trống trơn
        -- thay vì câu tử tế bên dưới. Mẫu lấy từ 13_DOT4_DONG_VONG_DOC.sql.
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Số căn cước này đã thuộc về một người khác.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- ----------------------------------------------------------------------------
-- DM_BenhNhanCoSo_GoNoi
-- Than lay tu 13_DOT4_DONG_VONG_DOC.sql (da doi soat: khop, chi lech dong CREATE).
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhanCoSo_GoNoi
    @IDBenhNhanCoSo bigint,
    @IDTaiKhoan     bigint,
    @ResultCode     int            OUTPUT,
    @ResultMessage  nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @idBenhNhan bigint, @idCoSo bigint, @maBN varchar(20), @chuSoHuu bigint;

        SELECT @idBenhNhan = h.IDBenhNhan,
               @idCoSo     = h.IDCoSo,
               @maBN       = h.MaBN,
               @chuSoHuu   = b.IDTaiKhoan
          FROM dbo.DM_BenhNhanCoSo h WITH (UPDLOCK, HOLDLOCK)
          JOIN dbo.DM_BenhNhan     b ON b.ID = h.IDBenhNhan
         WHERE h.ID = @IDBenhNhanCoSo;

        IF @idBenhNhan IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Không tìm thấy hồ sơ tại cơ sở.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- 🔴 Cong chan — cung ly do voi HoSoThuocTaiKhoanAsync.
        IF @chuSoHuu IS NULL OR @chuSoHuu <> @IDTaiKhoan
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Hồ sơ này không thuộc tài khoản của bạn.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @maBN IS NULL
        BEGIN
            SET @ResultCode = 6;
            SET @ResultMessage = N'Hồ sơ này chưa nối mã nào nên không có gì để gỡ.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DECLARE @bayGio datetime = GETDATE();

        INSERT INTO bak.GoNoi_TaiLieu_V001
        SELECT *, @bayGio FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhanCoSo = @IDBenhNhanCoSo;

        INSERT INTO bak.GoNoi_DotKham_V001
        SELECT *, @bayGio FROM dbo.QL_DotKham WHERE IDBenhNhanCoSo = @IDBenhNhanCoSo;

        INSERT INTO bak.GoNoi_HoSoCoSo_V001
        SELECT *, @bayGio FROM dbo.DM_BenhNhanCoSo WHERE ID = @IDBenhNhanCoSo;

        DELETE FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhanCoSo = @IDBenhNhanCoSo;
        DELETE FROM dbo.QL_DotKham         WHERE IDBenhNhanCoSo = @IDBenhNhanCoSo;
        DELETE FROM dbo.DM_BenhNhanCoSo    WHERE ID = @IDBenhNhanCoSo;

        -- Het cho dung tai co so nay => dung lai mot dong TU KHAI de ho so "tut
        -- ve *Ho so tu khai*" chu khong bien mat.
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                        WHERE IDBenhNhan = @idBenhNhan AND IDCoSo = @idCoSo)
        BEGIN
            INSERT INTO dbo.DM_BenhNhanCoSo (IDBenhNhan, IDCoSo, MaBN, DaMoTaiLieu)
            VALUES (@idBenhNhan, @idCoSo, NULL, 1);
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

-- ----------------------------------------------------------------------------
-- DM_BenhNhanCoSo_DoiMocXemLich
-- Than lay tu 13_DOT4_DONG_VONG_DOC.sql (da doi soat: khop).
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhanCoSo_DoiMocXemLich
    @IDBenhNhan    bigint,
    @IDCoSo        bigint,
    @IDTaiKhoan    bigint,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE ID = @IDBenhNhan AND IDTaiKhoan = @IDTaiKhoan)
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Hồ sơ này không thuộc tài khoản của bạn.';
            RETURN;
        END;

        UPDATE dbo.DM_BenhNhanCoSo
           SET NgayXemLichCuoi = GETDATE()
         WHERE IDBenhNhan = @IDBenhNhan AND IDCoSo = @IDCoSo;

        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- ----------------------------------------------------------------------------
-- DM_CSKCB_GioLamViec_Save
-- Than lay tu P05__gio_lam_viec.sql (da doi soat: khop het).
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.DM_CSKCB_GioLamViec_Save
    @IDCoSo        bigint,
    @Thu           tinyint,
    @GioMoCua      time(0),
    @GioDongCua    time(0),
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        IF @Thu > 6
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Thứ phải nằm trong khoảng 0 (Chủ nhật) đến 6 (Thứ bảy).';
            RETURN;
        END

        IF @GioMoCua IS NULL OR @GioDongCua IS NULL
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Giờ mở cửa và giờ đóng cửa không được để trống.';
            RETURN;
        END

        IF @GioDongCua <= @GioMoCua
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage = N'Giờ đóng cửa phải sau giờ mở cửa.';
            RETURN;
        END

        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.DM_CSKCB_GioLamViec
                    WHERE IDCoSo = @IDCoSo AND Thu = @Thu)
            UPDATE dbo.DM_CSKCB_GioLamViec
               SET GioMoCua = @GioMoCua, GioDongCua = @GioDongCua
             WHERE IDCoSo = @IDCoSo AND Thu = @Thu;
        ELSE
            INSERT INTO dbo.DM_CSKCB_GioLamViec (IDCoSo, Thu, GioMoCua, GioDongCua)
            VALUES (@IDCoSo, @Thu, @GioMoCua, @GioDongCua);

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() = 547
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Cơ sở không tồn tại.';
        END
        ELSE
        BEGIN
            SET @ResultCode = 9;
            SET @ResultMessage = ERROR_MESSAGE();
            THROW;
        END
    END CATCH
END;
GO

-- ============================================================================
-- CONG KIEM - phai tra ve 0 DONG. Con dong nao la con thu tuc mang chuoi hong.
--
-- 🔴 BAT BUOC COLLATE Latin1_General_BIN2. Collation mac dinh cua CSDL nay BO
--    DAU khi so sanh, nen LIKE N'%<A-tilde>%' khop luon ca chu 'A' thuong -
--    dung no thi phep do bao dong gia hang loat (dem ra 14 thu tuc thay vi 5).
-- ============================================================================
SELECT o.name AS ThuTucConMojibake
  FROM sys.sql_modules m
  JOIN sys.objects   o ON o.object_id = m.object_id
 WHERE m.definition COLLATE Latin1_General_BIN2
       LIKE N'%' + NCHAR(0x00E1) + NCHAR(0x00BB) + N'%'
    OR m.definition COLLATE Latin1_General_BIN2
       LIKE N'%' + NCHAR(0x00E1) + NCHAR(0x00BA) + N'%'
    OR m.definition COLLATE Latin1_General_BIN2
       LIKE N'%' + NCHAR(0x00C3) + NCHAR(0x00A0) + N'%'
    OR m.definition COLLATE Latin1_General_BIN2
       LIKE N'%' + NCHAR(0x00C4) + NCHAR(0x0192) + N'%'
 ORDER BY o.name;
GO
