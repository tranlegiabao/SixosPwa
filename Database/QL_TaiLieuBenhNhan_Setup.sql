-- ============================================================================
-- Tạo bảng QL_TaiLieuBenhNhan, bổ sung cột ApiKey cho DM_CSKCB và SP lưu trữ
-- Tuân thủ kiến trúc ADR 0008 (Ghi qua Stored Procedure) & ADR 0012 (Lưu FTP)
-- ============================================================================

-- 1. Thêm cột ApiKey vào DM_CSKCB (nếu chưa có)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.DM_CSKCB') 
    AND name = 'ApiKey'
)
BEGIN
    ALTER TABLE dbo.DM_CSKCB ADD ApiKey NVARCHAR(100) NULL;
END;
GO

-- 2. Tạo bảng QL_TaiLieuBenhNhan
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QL_TaiLieuBenhNhan')
BEGIN
    CREATE TABLE dbo.QL_TaiLieuBenhNhan (
        ID BIGINT IDENTITY(1,1) NOT NULL,
        IDCoSo BIGINT NOT NULL,
        IDBenhNhanCoSo BIGINT NULL,
        MaBN NVARCHAR(50) NOT NULL,
        LoaiTaiLieu NVARCHAR(50) NOT NULL,
        TenTaiLieu NVARCHAR(255) NOT NULL,
        DuongDanFtp NVARCHAR(500) NOT NULL,
        DungLuongByte BIGINT NOT NULL,
        NgayKham DATETIME NULL,
        GhiChu NVARCHAR(MAX) NULL,
        NgayTao DATETIME NOT NULL CONSTRAINT DF_QL_TaiLieuBenhNhan_NgayTao DEFAULT GETDATE(),
        CONSTRAINT PK_QL_TaiLieuBenhNhan PRIMARY KEY CLUSTERED (ID ASC),
        CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_CSKCB FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB (ID),
        CONSTRAINT FK_QL_TaiLieuBenhNhan_DM_BenhNhanCoSo FOREIGN KEY (IDBenhNhanCoSo) REFERENCES dbo.DM_BenhNhanCoSo (ID)
    );

    CREATE NONCLUSTERED INDEX IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN 
        ON dbo.QL_TaiLieuBenhNhan (IDCoSo, MaBN);

    CREATE NONCLUSTERED INDEX IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo 
        ON dbo.QL_TaiLieuBenhNhan (IDBenhNhanCoSo) 
        WHERE IDBenhNhanCoSo IS NOT NULL;
END;
GO

-- 3. Stored procedure lưu tài liệu bệnh nhân (ADR 0008)
CREATE OR ALTER PROCEDURE dbo.QL_TaiLieuBenhNhan_Save
    @ID BIGINT,
    @IDCoSo BIGINT,
    @IDBenhNhanCoSo BIGINT = NULL,
    @MaBN NVARCHAR(50),
    @LoaiTaiLieu NVARCHAR(50),
    @TenTaiLieu NVARCHAR(255),
    @DuongDanFtp NVARCHAR(500),
    @DungLuongByte BIGINT,
    @NgayKham DATETIME = NULL,
    @GhiChu NVARCHAR(MAX) = NULL,
    @IDTaiLieu BIGINT OUTPUT,
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
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

        IF @ID = 0
        BEGIN
            INSERT INTO dbo.QL_TaiLieuBenhNhan (
                IDCoSo,
                IDBenhNhanCoSo,
                MaBN,
                LoaiTaiLieu,
                TenTaiLieu,
                DuongDanFtp,
                DungLuongByte,
                NgayKham,
                GhiChu,
                NgayTao
            )
            VALUES (
                @IDCoSo,
                @IDBenhNhanCoSo,
                @MaBN,
                @LoaiTaiLieu,
                @TenTaiLieu,
                @DuongDanFtp,
                @DungLuongByte,
                @NgayKham,
                @GhiChu,
                GETDATE()
            );

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
            SET IDCoSo = @IDCoSo,
                IDBenhNhanCoSo = @IDBenhNhanCoSo,
                MaBN = @MaBN,
                LoaiTaiLieu = @LoaiTaiLieu,
                TenTaiLieu = @TenTaiLieu,
                DuongDanFtp = @DuongDanFtp,
                DungLuongByte = @DungLuongByte,
                NgayKham = @NgayKham,
                GhiChu = @GhiChu
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
