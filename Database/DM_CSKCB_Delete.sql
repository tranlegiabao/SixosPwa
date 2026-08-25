/*
    Xóa một cơ sở y tế cùng dữ liệu phụ thuộc trực tiếp.
    Việc gọi thủ tục chỉ được controller cho phép sau khi quản trị viên xác nhận
    thao tác xóa không thể hoàn tác trên giao diện.
*/
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

        DELETE FROM dbo.QL_LichSuKham
        WHERE IDBenhNhanCoSo IN (SELECT ID FROM dbo.DM_BenhNhanCoSo WHERE IDCoSo = @ID);
        DELETE FROM dbo.DM_BenhNhanCoSo WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_GioLamViec WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_CapQuangCao WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_NoiDung WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_QuangCao WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_DoiTacApi WHERE IDCoSo = @ID;
        DELETE FROM dbo.HT_TaiKhoanDoiTac WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB WHERE ID = @ID;

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
