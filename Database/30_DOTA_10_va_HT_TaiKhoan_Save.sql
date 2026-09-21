/* =============================================================================
   DOT A - buoc A10 (VA SAU NGHIEM THU): tra lai tham so @IDBenhNhan cho
   HT_TaiKhoan_Save theo kieu "NHAN ROI BO".

   🔴 VI SAO — chuyen PLAN KHONG BIET, nghiem thu dau-cuoi moi loi ra:
   HIS KHONG chi goi cong qua HTTP. Stored `Dev_Master3.dbo.S00_UploadOnline`
   goi THANG sang CSDL cong qua LINKED SERVER `SPWA_CONG`, di qua 6 cua:
        cua 1  DM_BenhNhan_Save
        cua 2  HT_TaiKhoan_Save          <-- A07 bo @IDBenhNhan o day
        cua 3  DM_BenhNhan_NhanChuSoHuu
        cua 4  DM_BenhNhanCoSo_Save
        cua 5  QL_DotKham_Save
        cua 6  QL_TaiLieuBenhNhan_Save
   Bo tham so => HIS nem "Procedure or function HT_TaiKhoan_Save has too many
   arguments specified", va man Gui cho benh nhan CHET (do that: 2/2 dong
   SPWA_GuiTaiLieu bao KHONG_TOI_DUOC_CONG luc 18:59 ngay 18-09).

   => Xu ly y het TIEN LE V14 da dung cho QL_TaiLieuBenhNhan_Save va
   QL_DotKham_Save: CSDL bo cot, nhung THU TUC VAN NHAN tham so roi BO DI.
   Ben goi (HIS) khong phai sua mot dong nao — va quan trong hon, KHONG phai
   di sua stored o TUNG khach hang.

   Cot HT_TaiKhoan.IDBenhNhan van BI XOA (ADR 0019 van duoc thi hanh tron ven):
   chieu dung la DM_BenhNhan.IDTaiKhoan, va cua 3 (DM_BenhNhan_NhanChuSoHuu)
   moi la cho ghi quyen so huu that su.
   ========================================================================== */
SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.HT_TaiKhoan_Save
    @ID                  bigint,
    @SDT                 varchar(20),
    @Email               nvarchar(50) = NULL,
    @Role                varchar(20),
    @MatKhauNoiBoDaBam   varchar(255) = NULL,
    /* 🔴 NHAN ROI BO — GIU NGUYEN, DUNG XOA.
       Cot HT_TaiKhoan.IDBenhNhan da bi bo (ADR 0019) nhung HIS van truyen tham
       so nay qua linked server SPWA_CONG (S00_UploadOnline, cua 2). Bo di la
       man "Gui cho benh nhan" ben HIS chet ngay. Quyen so hua thuc su duoc ghi
       o cua 3 — DM_BenhNhan_NhanChuSoHuu — theo chieu DM_BenhNhan.IDTaiKhoan. */
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

/* --- tu kiem: 6 cua ma HIS goi qua linked server phai CON DU va DUNG chu ky -- */
SELECT 'A10 — 6 cua HIS goi qua linked server SPWA_CONG' AS Buoc,
       c.Ten AS Stored,
       CASE WHEN OBJECT_ID('dbo.' + c.Ten) IS NULL THEN N'THIEU!' ELSE N'co' END AS TonTai,
       (SELECT COUNT(*) FROM sys.parameters p WHERE p.object_id = OBJECT_ID('dbo.' + c.Ten)) AS SoThamSo
FROM (VALUES ('DM_BenhNhan_Save'), ('HT_TaiKhoan_Save'), ('DM_BenhNhan_NhanChuSoHuu'),
             ('DM_BenhNhanCoSo_Save'), ('QL_DotKham_Save'), ('QL_TaiLieuBenhNhan_Save')) AS c(Ten);

SELECT 'A10 — HT_TaiKhoan_Save nhan lai @IDBenhNhan' AS Buoc, name AS ThamSo,
       TYPE_NAME(user_type_id) AS Kieu, is_output AS LaOutput
FROM sys.parameters WHERE object_id = OBJECT_ID('dbo.HT_TaiKhoan_Save') ORDER BY parameter_id;

/* Cot thi VAN PHAI vang mat — ADR 0019 khong bi dao nguoc */
SELECT 'A10 — cot HT_TaiKhoan.IDBenhNhan van phai BIEN MAT' AS Buoc,
       CASE WHEN COL_LENGTH('dbo.HT_TaiKhoan','IDBenhNhan') IS NULL
            THEN N'DAT (cot da bo)' ELSE N'HONG (cot con day)' END AS KetQua;
GO
