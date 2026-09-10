/* =============================================================================
   16_CREATE_HT_CONFIG.sql -- Tao bang HT_Config trong database HIS_CSKH
   Tham chieu nguyen mau 100% tu database Dev_Master3 (he thong HIS).
   Chay lai duoc (Idempotent).

   CAU TRUC BANG:
     - ID         bigint IDENTITY(1,1) NOT NULL (PK Clustered)
     - MaChucNang nvarchar(50) NULL
     - SoLuong    int NULL
     - HieuLuc    bit NULL
     - Ghichu     nvarchar(500) NULL
     - Ngay       date NULL
     - GiaTri     int NULL
     - Nhom       nvarchar(20) NULL
   ============================================================================= */

SET NOCOUNT ON;
GO

PRINT N'=== BAT DAU TAO BANG dbo.HT_Config TRONG HIS_CSKH ===';

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HT_Config' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.HT_Config (
        ID bigint IDENTITY(1,1) NOT NULL,
        MaChucNang nvarchar(50) NULL,
        SoLuong int NULL,
        HieuLuc bit NULL,
        Ghichu nvarchar(500) NULL,
        Ngay date NULL,
        GiaTri int NULL,
        Nhom nvarchar(20) NULL,
        CONSTRAINT PK_HT_Config PRIMARY KEY CLUSTERED (ID ASC)
    );
    PRINT N'-> Da tao bang dbo.HT_Config thanh cong (nguyen mau nhu Dev_Master3).';
END
ELSE
BEGIN
    PRINT N'-> Bang dbo.HT_Config da ton tai trong database HIS_CSKH.';
END
GO

PRINT N'=== HOAN TAT TAO BANG dbo.HT_Config ===';
GO
