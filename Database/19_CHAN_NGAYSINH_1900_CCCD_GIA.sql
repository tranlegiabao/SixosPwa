-- ============================================================================
-- 19_CHAN_NGAYSINH_1900_CCCD_GIA.sql
--
-- Muc tieu: Khi CCCD la ma gia ('11111111111' hoac '111111111111'),
-- neu ngay sinh de trong (NULL) hoac bang '01/01/1900' ('1900-01-01')
-- thi chan luon va thong bao loi:
-- "Ngày sinh không hợp lệ. Vui lòng nhập ngày sinh chính xác của bệnh nhân."
--
-- Ap dung cho ca:
-- 1. dbo.DM_BenhNhan_Save
-- 2. dbo.DM_BenhNhan_SuaHoSo
-- ============================================================================

USE [HIS_CSKH];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================================
-- 1. dbo.DM_BenhNhan_Save
-- ============================================================================
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

        -- 🔴 Chặn ngày sinh 01/01/1900 hoặc để trống khi CCCD là mã giả
        IF @laCccdKhongCo = 1 AND (@NgaySinh IS NULL OR CAST(@NgaySinh AS date) = '1900-01-01')
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage = N'Ngày sinh không hợp lệ. Vui lòng nhập ngày sinh chính xác của bệnh nhân.';
            RETURN;
        END;

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

            /* Chỉ ghi đè bằng giá trị thực sự có — đừng xóa trắng dữ liệu cũ */
            UPDATE dbo.DM_BenhNhan
               SET TenBN         = ISNULL(NULLIF(LTRIM(RTRIM(@TenBN)), N''), TenBN),
                   SDT           = ISNULL(@SDT,   SDT),
                   Email         = ISNULL(@Email, Email),
                   DiaChi        = ISNULL(@DiaChi, DiaChi),
                   NgaySinh      = ISNULL(@NgaySinh, NgaySinh),
                   HoTenKhongDau = ISNULL(@HoTenKhongDau, HoTenKhongDau),
                   GioiTinh      = ISNULL(NULLIF(LTRIM(RTRIM(@GioiTinh)), ''), GioiTinh),
                   -- Chỉ nhận chủ sở hữu khi đang bỏ trống.
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
            SET @ResultMessage =
                N'Số căn cước này đã thuộc về một người khác. ' +
                N'Nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được hỗ trợ.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- ============================================================================
-- 2. dbo.DM_BenhNhan_SuaHoSo
-- ============================================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_SuaHoSo
    @IDBenhNhan    bigint,
    @IDTaiKhoan    bigint,
    @CCCD          varchar(20),
    @TenBN         nvarchar(100),
    @SDT           varchar(20)   = NULL,
    @NgaySinh      datetime      = NULL,
    @HoTenKhongDau varchar(100)  = NULL,
    @GioiTinh      varchar(10)   = NULL,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Chủ sở hữu: chỉ tài khoản sở hữu hồ sơ mới được sửa.
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE ID = @IDBenhNhan AND IDTaiKhoan = @IDTaiKhoan)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Hồ sơ không tồn tại hoặc không thuộc tài khoản của bạn.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DECLARE @cccdSach varchar(20) = NULLIF(LTRIM(RTRIM(@CCCD)), '');
        DECLARE @laCccdKhongCo bit = CASE WHEN @cccdSach IN ('11111111111', '111111111111') THEN 1 ELSE 0 END;

        -- 🔴 Chặn ngày sinh 01/01/1900 hoặc để trống khi CCCD là mã giả
        IF @laCccdKhongCo = 1 AND (@NgaySinh IS NULL OR CAST(@NgaySinh AS date) = '1900-01-01')
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage = N'Ngày sinh không hợp lệ. Vui lòng nhập ngày sinh chính xác của bệnh nhân.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

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

        -- Hồ sơ tự khai: sửa được mọi ô.
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
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage =
                N'Số căn cước này đã thuộc về một người khác. ' +
                N'Nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được hỗ trợ.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO
