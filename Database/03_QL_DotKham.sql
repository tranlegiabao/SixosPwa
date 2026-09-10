-- ============================================================================
-- 03 — QL_DotKham: mot dong = MOT LAN DEN (chot 13 dot 1)
--
-- Thay cho QL_LichSuKham (bang 4 cot dem, dang rong 0 dong — xem 06).
-- Bon so dem cu (lan dau / lan gan nhat / so lan) SUY THANG tu bang nay bang
-- MIN/MAX/COUNT, khong giu so dem o hai noi.
--
-- Khoa tu nhien (IDCoSo, MaVaoVien): HIS day lai ca lo cung khong de dong trung.
-- Day TEN khoa / TEN bac si chu khong day ID — ID cua HIS vo nghia ben cong.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QL_DotKham')
BEGIN
    CREATE TABLE dbo.QL_DotKham (
        ID              bigint IDENTITY(1,1) NOT NULL,
        IDCoSo          bigint        NOT NULL,
        IDBenhNhanCoSo  bigint        NOT NULL,
        MaVaoVien       varchar(50)   NOT NULL,
        MaBN            varchar(20)   NOT NULL,
        NgayGioVao      datetime      NOT NULL,
        NgayGioRa       datetime      NULL,
        TenKhoa         nvarchar(255) NULL,
        TenBacSi        nvarchar(255) NULL,
        ChanDoan        nvarchar(MAX) NULL,
        NgayTao         datetime      NOT NULL CONSTRAINT DF_QL_DotKham_NgayTao DEFAULT (GETDATE()),
        NgayCapNhat     datetime      NULL,
        CONSTRAINT PK_QL_DotKham PRIMARY KEY CLUSTERED (ID ASC),
        CONSTRAINT FK_QL_DotKham_DM_CSKCB FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB (ID),
        CONSTRAINT FK_QL_DotKham_DM_BenhNhanCoSo FOREIGN KEY (IDBenhNhanCoSo) REFERENCES dbo.DM_BenhNhanCoSo (ID)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UK_QL_DotKham_MaVaoVien
        ON dbo.QL_DotKham (IDCoSo, MaVaoVien);
END;
GO

-- ---------------------------------------------------------------------------
-- Luu MOT dot kham. Goi lap cho ca lo tu phia C# (lo it dong, moi benh nhan
-- vai chuc dot), moi dong tu quyet dinh them hay cap nhat theo khoa tu nhien.
--
-- 20,5% dot kham ben Thien Nam TRONG chan doan — do that. Vi vay ChanDoan
-- KHONG bat buoc, va man hien thi phai chiu duoc o trong.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.QL_DotKham_Save
    @IDCoSo         bigint,
    @IDBenhNhanCoSo bigint,
    @MaVaoVien      varchar(50),
    @MaBN           varchar(20),
    @NgayGioVao     datetime,
    @NgayGioRa      datetime      = NULL,
    @TenKhoa        nvarchar(255) = NULL,
    @TenBacSi       nvarchar(255) = NULL,
    @ChanDoan       nvarchar(MAX) = NULL,
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
           `SELECT @bien = cot` tren tap RONG thi bien GIU NGUYEN gia tri cu, KHONG
           thanh NULL. Ban dau doan nay do thang vao @IDDotKham -- ma @IDDotKham da
           duoc SET = 0 o dau thu tuc => `IF @IDDotKham IS NULL` KHONG BAO GIO dung
           => luon roi xuong nhanh UPDATE ... WHERE ID = 0 => 0 dong, khong loi,
           roi van SET @ResultCode = 1. API tra "Da nhan 1 dot kham" ma bang trong.
           Dung bien CUC BO khai bao ngay tai day: no chac chan bat dau bang NULL. */
        DECLARE @IDCu bigint;

        SELECT @IDCu = ID
        FROM dbo.QL_DotKham WITH (UPDLOCK, HOLDLOCK)
        WHERE IDCoSo = @IDCoSo AND MaVaoVien = @MaVaoVien;

        IF @IDCu IS NULL
        BEGIN
            INSERT INTO dbo.QL_DotKham
                (IDCoSo, IDBenhNhanCoSo, MaVaoVien, MaBN, NgayGioVao, NgayGioRa, TenKhoa, TenBacSi, ChanDoan, NgayTao)
            VALUES
                (@IDCoSo, @IDBenhNhanCoSo, @MaVaoVien, @MaBN, @NgayGioVao, @NgayGioRa, @TenKhoa, @TenBacSi, @ChanDoan, GETDATE());

            SET @IDDotKham = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            -- Day lai la chuyen binh thuong (4,5% dot kham chua co ngay ra vien
            -- luc day lan dau) => cap nhat, khong bao loi trung.
            UPDATE dbo.QL_DotKham
            SET IDBenhNhanCoSo = @IDBenhNhanCoSo,
                MaBN           = @MaBN,
                NgayGioVao     = @NgayGioVao,
                NgayGioRa      = @NgayGioRa,
                TenKhoa        = @TenKhoa,
                TenBacSi       = @TenBacSi,
                ChanDoan       = @ChanDoan,
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
