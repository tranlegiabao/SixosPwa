-- ============================================================================
-- 09 — Mot tai khoan quan NHIEU ho so (V6b, chot 2 dot 1, ADR 0019)
--
-- Doi chieu quan he: truoc day HT_TaiKhoan.IDBenhNhan (1-1), nay
-- DM_BenhNhan.IDTaiKhoan (1-N). Cot da duoc them o script 05; file nay do du
-- lieu sang va sua cac thu tuc.
--
-- Vi sao: do tren DB khach that — 4.140 so dien thoai gan >=2 CCCD, ca biet mot
-- so gan 876 nguoi. Ca nha dung chung mot so la chuyen thuong; voi mo hinh 1-1
-- thi moi nguoi trong nha phai co mot so rieng moi dung duoc cong, ma nguoi gia
-- — dung nhom can tra ket qua nhat — thuong khong co so rieng.
--
-- 🔴 KHONG doi khoa tra cuu cua DM_BenhNhanCoSo_Save sang (IDCoSo, MaBN).
--    Ke hoach cu ghi vay la QUA THO: ho so TU KHAI co MaBN NULL, ma trong SQL
--    "NULL = NULL" khong bao gio dung, nen moi lan luu se de mot dong moi. Duong
--    dung la HAI thao tac tach nhau:
--      * DM_BenhNhanCoSo_TaoTuKhai — khoa (IDBenhNhan, IDCoSo) WHERE MaBN IS NULL
--      * DM_BenhNhanCoSo_Save (cu)  — cho duong NOI HIS, co MaBN that
--
-- Chay SAU 05 va 08.
-- ============================================================================

PRINT '--- Truoc khi chay ---';
SELECT  TongNguoi        = COUNT(*),
        DaCoIDTaiKhoan   = SUM(CASE WHEN IDTaiKhoan IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.DM_BenhNhan;
GO

-- --- (1) Do quan he cu sang cot moi ------------------------------------------
-- HT_TaiKhoan.IDBenhNhan van GIU LAI, khong xoa: khu Admin va man Dashboard con
-- doc no (TaiKhoanController.cs:123, DashboardController.cs:62). Hai cot song
-- song mot thoi gian la CO Y — cot moi la nguon su that cua cong benh nhan.
UPDATE bn
SET    bn.IDTaiKhoan = tk.ID
FROM   dbo.DM_BenhNhan bn
JOIN   dbo.HT_TaiKhoan tk ON tk.IDBenhNhan = bn.ID
WHERE  bn.IDTaiKhoan IS NULL;
GO

-- --- (2) DM_BenhNhan_Save: nhan chu so huu + ba o cua luat gop ---------------
-- Them @IDTaiKhoan, @NgaySinh, @HoTenKhongDau — deu TUY CHON nen moi cho dang
-- goi khong phai sua gi.
--
-- 🔴 "Ai khai truoc giu CCCD" (ADR 0019 muc 2): CCCD da thuoc mot tai khoan
-- KHAC thi tra ResultCode 3. Thong bao phai CHI DUONG RA chu khong cut nhu ban
-- UB — nguoi doc phai biet lam gi tiep.
--
-- Chu so huu chi dat khi cot dang RONG. Da co chu thi khong ai cuop duoc bang
-- cach goi lai thu tuc.
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_Save
    @CCCD          varchar(20),
    @TenBN         nvarchar(100),
    @SDT           varchar(20)   = NULL,
    @Email         varchar(100)  = NULL,
    @DiaChi        nvarchar(255) = NULL,
    @IDTaiKhoan    bigint        = NULL,
    @NgaySinh      datetime      = NULL,
    @HoTenKhongDau nvarchar(100) = NULL,
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

        BEGIN TRANSACTION;

        DECLARE @chuSoHuuHienTai bigint;

        SELECT @IDBenhNhan = ID, @chuSoHuuHienTai = IDTaiKhoan
        FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
        WHERE CCCD = @cccdSach;

        IF @IDBenhNhan IS NULL
        BEGIN
            INSERT INTO dbo.DM_BenhNhan (CCCD, TenBN, SDT, Email, DiaChi, IDTaiKhoan, NgaySinh, HoTenKhongDau)
            VALUES (@cccdSach, @TenBN, @SDT, @Email, @DiaChi, @IDTaiKhoan, @NgaySinh, @HoTenKhongDau);
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
                    N'Số căn cước này đã được một tài khoản khác khai trước. ' +
                    N'Nếu đó là người thân của bạn, hãy nhờ họ vào mục "Hồ sơ của tôi" và xoá hồ sơ đó để nhả căn cước ra; ' +
                    N'nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được xử lý.';
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
        THROW;
    END CATCH;
END;
GO

-- --- (3) Ho so TU KHAI tai mot co so -----------------------------------------
-- MaBN de NULL: co so chua cap ma nao cho nguoi nay. Khoa (IDBenhNhan, IDCoSo)
-- CHI trong pham vi cac dong chua noi HIS — dong da noi co MaBN that va thuoc
-- duong khac, khong duoc dung vao.
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhanCoSo_TaoTuKhai
    @IDBenhNhan      bigint,
    @IDCoSo          bigint,
    @IDBenhNhanCoSo  bigint         OUTPUT,
    @ResultCode      int            OUTPUT,
    @ResultMessage   nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDBenhNhanCoSo = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @IDBenhNhanCoSo = ID
        FROM dbo.DM_BenhNhanCoSo WITH (UPDLOCK, HOLDLOCK)
        WHERE IDBenhNhan = @IDBenhNhan AND IDCoSo = @IDCoSo AND MaBN IS NULL;

        IF @IDBenhNhanCoSo IS NULL
        BEGIN
            -- Da co ho so DA NOI HIS tai co so nay thi khong can de them dong tu
            -- khai — nguoi dung se thay ho so that.
            SELECT TOP 1 @IDBenhNhanCoSo = ID
            FROM dbo.DM_BenhNhanCoSo
            WHERE IDBenhNhan = @IDBenhNhan AND IDCoSo = @IDCoSo
            ORDER BY ID;
        END;

        IF @IDBenhNhanCoSo IS NULL
        BEGIN
            -- Ho so tu khai la cua chinh chu tai khoan => cua tai lieu mo san
            -- (chot 9 dot 1). Cua chi that su co viec khi ho so duoc NOI sang
            -- benh an ben HIS.
            INSERT INTO dbo.DM_BenhNhanCoSo (IDBenhNhan, IDCoSo, MaBN, DaMoTaiLieu)
            VALUES (@IDBenhNhan, @IDCoSo, NULL, 1);
            SET @IDBenhNhanCoSo = SCOPE_IDENTITY();
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
            SET @ResultMessage = N'Bệnh nhân hoặc cơ sở không tồn tại.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- --- (4) Xoa mot ho so de NHA CCCD -------------------------------------------
-- ADR 0019 muc 2 doi cho nguoi dang giu mot nut xoa, neu khong thi "ai khai
-- truoc giu" thanh cai bay khong loi thoat.
--
-- Chan ba viec: khong phai ho so cua minh; ho so DA NOI HIS (co MaBN); va ho so
-- da co tai lieu / dot kham — xoa la mat du lieu y te that.
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_XoaHoSo
    @IDBenhNhan    bigint,
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
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                       WHERE ID = @IDBenhNhan AND IDTaiKhoan = @IDTaiKhoan)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Hồ sơ này không thuộc tài khoản của bạn.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                   WHERE IDBenhNhan = @IDBenhNhan AND MaBN IS NOT NULL)
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage = N'Hồ sơ này đã nối với bệnh án tại cơ sở nên không xoá được. Hãy bỏ nối trước.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF EXISTS (SELECT 1 FROM dbo.QL_TaiLieuBenhNhan t
                   JOIN dbo.DM_BenhNhanCoSo h ON h.ID = t.IDBenhNhanCoSo
                   WHERE h.IDBenhNhan = @IDBenhNhan)
           OR EXISTS (SELECT 1 FROM dbo.QL_DotKham d
                      JOIN dbo.DM_BenhNhanCoSo h ON h.ID = d.IDBenhNhanCoSo
                      WHERE h.IDBenhNhan = @IDBenhNhan)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Hồ sơ này đã có dữ liệu khám nên không xoá được.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DELETE FROM dbo.DM_BenhNhanCoSo WHERE IDBenhNhan = @IDBenhNhan;
        DELETE FROM dbo.DM_BenhNhan     WHERE ID = @IDBenhNhan;

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

-- --- (4b) Nhan chu so huu cho mot ho so ---------------------------------------
-- Vi sao tach rieng thay vi nhet vao DM_BenhNhan_Save: trong luong dang ky,
-- CON NGUOI duoc tao TRUOC tai khoan (SaveBenhNhanAsync roi moi SaveTaiKhoanAsync),
-- nen luc tao chua biet chu la ai. Dao thu tu ca luong dang nhap dang chay that
-- la rui ro khong dang, con goi DM_BenhNhan_Save hai lan thi mo ho.
--
-- Chi nhan khi cot dang BO TRONG. Da co chu thi tra ResultCode 3 kem duong ra —
-- "ai khai truoc giu CCCD" (ADR 0019 muc 2).
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_NhanChuSoHuu
    @IDBenhNhan    bigint,
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
        BEGIN TRANSACTION;

        DECLARE @chuHienTai bigint;

        SELECT @chuHienTai = IDTaiKhoan
        FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
        WHERE ID = @IDBenhNhan;

        IF @@ROWCOUNT = 0
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Hồ sơ không tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @chuHienTai IS NULL
        BEGIN
            UPDATE dbo.DM_BenhNhan SET IDTaiKhoan = @IDTaiKhoan WHERE ID = @IDBenhNhan;
        END
        ELSE IF @chuHienTai <> @IDTaiKhoan
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage =
                N'Số căn cước này đã được một tài khoản khác khai trước. ' +
                N'Nếu đó là người thân của bạn, hãy nhờ họ vào mục "Hồ sơ của tôi" và xoá hồ sơ đó để nhả căn cước ra; ' +
                N'nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được xử lý.';
            ROLLBACK TRANSACTION;
            RETURN;
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

-- --- (5) Don ma tu bia -------------------------------------------------------
-- Chot 12 dot 1: bo ma tu bia BN-yyyyMMdd-####. Chung KHONG phai ma co so cap,
-- chi la cho lap tam thoi cua cong. Dua ve NULL de dung nghia "chua noi HIS".
-- Chay SAU khi tang C# da bo SinhMaBenhNhan() va MaBN da la string?.
PRINT '--- Ma tu bia se don ve NULL ---';
SELECT SoDong = COUNT(*) FROM dbo.DM_BenhNhanCoSo WHERE MaBN LIKE 'BN-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]';
GO

UPDATE dbo.DM_BenhNhanCoSo
SET    MaBN = NULL
WHERE  MaBN LIKE 'BN-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]';
GO

PRINT '--- Sau khi chay ---';
SELECT  TongNguoi      = COUNT(*),
        DaCoIDTaiKhoan = SUM(CASE WHEN IDTaiKhoan IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.DM_BenhNhan;
SELECT  TongHoSo   = COUNT(*),
        ChuaNoiHIS = SUM(CASE WHEN MaBN IS NULL THEN 1 ELSE 0 END)
FROM dbo.DM_BenhNhanCoSo;
GO
