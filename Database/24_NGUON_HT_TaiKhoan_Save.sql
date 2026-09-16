-- ============================================================================
-- 24 — NGUON cua dbo.HT_TaiKhoan_Save (keo ve git, ADR 0026)
--
-- 🔴 VI SAO CO FILE NAY: stored HT_TaiKhoan_Save DANG CHAY tren HIS_CSKH nhung
--    KHONG CO FILE NAO TRONG GIT TAO RA NO (do 16/09). Trai ADR 0026 — "moi
--    object CSDL phai co nguon trong repo". Ma chot 45 lai di dua vao CHU KY
--    cua no: S00_UploadOnline ben HIS EXEC thang stored nay qua linked server.
--    Chu ky doi ma khong ai thay nguon la ben HIS vo ma khong hieu tai sao.
--
-- Noi dung duoi day la NGUYEN VAN OBJECT_DEFINITION lay tu HIS_CSKH@118 ngay
-- 16/09/2026, chi doi dung mot chu: CREATE -> CREATE OR ALTER (de chay lai duoc).
-- KHONG sua logic. Muon doi logic thi mo mot file so moi, dung sua o day.
--
-- 🔴 KHONG DUNG dbo.Admin_TaiKhoan_Save — stored CHET: no INSERT INTO TaiKhoan
--    (AdminStoredProcedures.sql:28) trong khi bang that la HT_TaiKhoan
--    (ApplicationDbContext.cs:99), va 0 cho trong C# goi no.
--    Cua song la dbo.HT_TaiKhoan_Save (AdminStoredProcedureService.cs:41).
-- ============================================================================
GO

/* ============================================================================
   P03 — THU TUC TAI KHOAN + DOI TAC
   ----------------------------------------------------------------------------
   !! HAI COT MAT KHAU, HAI LUAT KHAC NHAU — dung lam lan (ADR 0009) !!
     HT_TaiKhoan.MatKhauNoiBo      -> dang nhap Admin/DoiTac, CO BAM.
                                      Thu tuc o day KHONG BAO GIO tu bam:
                                      tang C# bam roi truyen chuoi da bam vao.
     HT_TaiKhoanDoiTac.MatKhau     -> POST nguyen van sang he doi tac.
                                      CO Y KHONG BAM — ADR 0005 bat buoc.
   ============================================================================ */

/* ----------------------------------------------------------------------------
   HT_TaiKhoan_Save — thay Admin_TaiKhoan_Save, dung luon cho
   LuongCongBenhNhan.cs:387 (_db.TaiKhoans.AddAsync)
   Khac ban cu: BO @CCCD (CCCD nay thuoc DM_BenhNhan), THEM @IDBenhNhan.
   @MatKhauNoiBoDaBam: truyen NULL = giu nguyen gia tri hien co.
   ---------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.HT_TaiKhoan_Save
    @ID                  bigint,
    @SDT                 varchar(20),
    @Email               nvarchar(50) = NULL,
    @Role                varchar(20),
    @MatKhauNoiBoDaBam   varchar(255) = NULL,
    @IDBenhNhan          bigint       = NULL,
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
            INSERT INTO dbo.HT_TaiKhoan (SDT, Email, Role, MatKhauNoiBo, IDBenhNhan)
            VALUES (@SDT, @Email, @Role, @MatKhauNoiBoDaBam, @IDBenhNhan);
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
                   MatKhauNoiBo = ISNULL(@MatKhauNoiBoDaBam, MatKhauNoiBo),
                   IDBenhNhan   = ISNULL(@IDBenhNhan, IDBenhNhan)
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
