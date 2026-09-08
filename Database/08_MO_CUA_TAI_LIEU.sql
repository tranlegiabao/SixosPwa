-- ============================================================================
-- 08 — Mo *Cua tai lieu* cho cac ho so TU KHAI (V5)
--
-- Chot 9 dot 1 + ADR 0020: benh nhan chi xem duoc ket qua can lam sang / don
-- thuoc khi ho so da qua *Cua tai lieu*. Can cua rieng vi CCCD go luc dang nhap
-- KHONG duoc xac thuc — OTP chi xac thuc so dien thoai. Khong co cua thi go
-- CCCD nguoi khac la xem duoc tai lieu cua ho.
--
-- 🔴 Vi sao 21 dong hien co duoc mo HET:
--    Toan bo ho so dang co deu do CHINH CONG tao ra luc dang ky (MaBN dang
--    BN-yyyyMMdd-#### la ma cong tu bia, khong phai ma co so cap). Nguoi so huu
--    ho so chinh la chu tai khoan da tao no => cua mo la dung.
--    Cua chi thuc su co viec khi ho so duoc NOI sang mot benh an ben HIS — luc
--    do moi phai hoi "co so co ghi ban la dau moi lien lac cua nguoi nay
--    khong". Man Noi ho so thuoc dot sau.
--
-- Chay SAU 05 (cot DaMoTaiLieu duoc them o do).
-- ============================================================================

PRINT '--- Truoc khi chay ---';
SELECT DaMoTaiLieu, SoHoSo = COUNT(*) FROM dbo.DM_BenhNhanCoSo GROUP BY DaMoTaiLieu;
GO

UPDATE dbo.DM_BenhNhanCoSo SET DaMoTaiLieu = 1 WHERE DaMoTaiLieu = 0;
GO

-- ---------------------------------------------------------------------------
-- Ho so TAO MOI tu luong dang ky cung phai mo san, neu khong benh nhan vua dang
-- ky xong se thay man tai lieu rong ma khong hieu vi sao.
--
-- Them tham so TUY CHON, mac dinh 1: moi cho dang goi khong phai sua gi. Khi
-- man Noi ho so ra doi, no se truyen 0 roi tu mo theo luat chot 9.
--
-- CHI dat luc INSERT. UPDATE khong dung toi cot nay — mot cua da bi dong CO Y
-- thi khong duoc lang le mo lai chi vi ai do luu lai ho so.
--
-- KHONG doi khoa tra cuu (IDBenhNhan, IDCoSo) trong dot nay: luong dang ky van
-- goi SinhMaBenhNhan() sinh ma moi moi lan, doi bay gio thi moi lan luu se de
-- mot ho so MOI. Xem muc E cua output/02.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhanCoSo_Save
    @IDBenhNhan      bigint,
    @IDCoSo          bigint,
    @MaBN            varchar(20),
    @DaMoTaiLieu     bit            = 1,
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
        WHERE IDBenhNhan = @IDBenhNhan AND IDCoSo = @IDCoSo;

        IF @IDBenhNhanCoSo IS NULL
        BEGIN
            INSERT INTO dbo.DM_BenhNhanCoSo (IDBenhNhan, IDCoSo, MaBN, DaMoTaiLieu)
            VALUES (@IDBenhNhan, @IDCoSo, @MaBN, @DaMoTaiLieu);
            SET @IDBenhNhanCoSo = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE dbo.DM_BenhNhanCoSo SET MaBN = @MaBN WHERE ID = @IDBenhNhanCoSo;
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
            SET @ResultMessage = N'Mã bệnh nhân này đã được cơ sở cấp cho người khác.';
            RETURN;
        END;
        IF ERROR_NUMBER() = 547
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Bệnh nhân hoặc cơ sở không tồn tại.';
            RETURN;
        END;
        THROW;
    END CATCH;
END;
GO

PRINT '--- Sau khi chay ---';
SELECT DaMoTaiLieu, SoHoSo = COUNT(*) FROM dbo.DM_BenhNhanCoSo GROUP BY DaMoTaiLieu;
GO
