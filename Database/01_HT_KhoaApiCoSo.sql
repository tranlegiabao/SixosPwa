-- ============================================================================
-- 01 — HT_KhoaApiCoSo: khoa API cap cho TUNG CO SO, tach khoi DM_CSKCB
--
-- Vi sao bang rieng (ADR 0022):
--   * Mot co so mang NHIEU khoa cung luc => xoay khoa khong co khoang chet:
--     cap khoa moi -> HIS doi cau hinh -> tat khoa cu.
--   * Khoa luu BAM (SHA2_256), khong luu tho: doc DB khong doc duoc khoa.
--   * Co Active RIENG CUA KHOA => cat duong API khong dung DM_CSKCB.Active.
--     ADR 0013 chot Active chi quyet dinh 2 thu: hien o cong cong khai, va
--     nhan dang nhap/dang ky moi. KHONG phai cong API.
--
-- Chay mot lan, idempotent. Rollback o 99_ROLLBACK.sql.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HT_KhoaApiCoSo')
BEGIN
    CREATE TABLE dbo.HT_KhoaApiCoSo (
        ID            bigint IDENTITY(1,1) NOT NULL,
        IDCoSo        bigint         NOT NULL,
        TenKhoa       nvarchar(100)  NOT NULL,
        KhoaBam       varbinary(32)  NOT NULL,
        Active        bit            NOT NULL CONSTRAINT DF_HT_KhoaApiCoSo_Active   DEFAULT (1),
        NgayCap       datetime       NOT NULL CONSTRAINT DF_HT_KhoaApiCoSo_NgayCap  DEFAULT (GETDATE()),
        NgayHetHan    datetime       NULL,
        NgayDungCuoi  datetime       NULL,
        CONSTRAINT PK_HT_KhoaApiCoSo PRIMARY KEY CLUSTERED (ID ASC),
        CONSTRAINT FK_HT_KhoaApiCoSo_DM_CSKCB FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB (ID)
    );

    -- Khoa tu nhien: bam phai duy nhat toan he thi tra cuu moi xac dinh duoc co so.
    CREATE UNIQUE NONCLUSTERED INDEX UK_HT_KhoaApiCoSo_KhoaBam
        ON dbo.HT_KhoaApiCoSo (KhoaBam);
END;
GO

-- ---------------------------------------------------------------------------
-- Chuyen 3 khoa dang nam tho o DM_CSKCB.ApiKey sang bang moi.
-- CONVERT(varchar) truoc khi bam: khoa API la ASCII, bam theo byte ASCII/UTF-8
-- de phia C# bam bang Encoding.UTF8 ra DUNG mot ket qua. Bam thang nvarchar se
-- ra byte UTF-16 va hai ben khong bao gio khop.
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_CSKCB') AND name = 'ApiKey')
BEGIN
    INSERT INTO dbo.HT_KhoaApiCoSo (IDCoSo, TenKhoa, KhoaBam, Active, NgayCap)
    SELECT  cs.ID,
            N'Khoa chuyen tu DM_CSKCB.ApiKey',
            HASHBYTES('SHA2_256', CONVERT(varchar(100), cs.ApiKey)),
            1,
            GETDATE()
    FROM dbo.DM_CSKCB cs
    WHERE cs.ApiKey IS NOT NULL
      AND LTRIM(RTRIM(cs.ApiKey)) <> ''
      AND NOT EXISTS (
            SELECT 1 FROM dbo.HT_KhoaApiCoSo k
            WHERE k.KhoaBam = HASHBYTES('SHA2_256', CONVERT(varchar(100), cs.ApiKey)));
END;
GO

-- 🔴 CO Y KHONG DROP DM_CSKCB.ApiKey o day.
-- Khu tai lieu cua dong nghiep dang con doc cot do (TaiLieuApiController muc 3).
-- Drop bay gio la lam vo code dang chay tren nhanh chung. Sau khi ho va xong
-- V7 (doi sang [KhoaCoSo]) thi chay 07_BO_ApiKey_SAU_KHI_VA.sql.
-- Trong khoang giao thoa, hai duong xac thuc song song va CUNG mot khoa dung
-- duoc cho ca hai — vi khoa tho van con o cot cu, con bam cua no da nam o bang
-- moi.
GO

-- ---------------------------------------------------------------------------
-- Cap / sua / bat tat mot khoa. Nhan khoa THO, tu bam — khoa tho khong bao gio
-- di qua bien nao ngoai tham so nay.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.HT_KhoaApiCoSo_Save
    @ID          bigint,
    @IDCoSo      bigint,
    @TenKhoa     nvarchar(100),
    @KhoaTho     varchar(100)   = NULL,
    @Active      bit            = 1,
    @NgayHetHan  datetime       = NULL,
    @IDKhoa      bigint         OUTPUT,
    @ResultCode  int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDKhoa = 0;

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
            IF @KhoaTho IS NULL OR LTRIM(RTRIM(@KhoaTho)) = ''
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Cấp khóa mới thì bắt buộc phải có khóa thô.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            INSERT INTO dbo.HT_KhoaApiCoSo (IDCoSo, TenKhoa, KhoaBam, Active, NgayCap, NgayHetHan)
            VALUES (@IDCoSo, @TenKhoa, HASHBYTES('SHA2_256', @KhoaTho), @Active, GETDATE(), @NgayHetHan);

            SET @IDKhoa = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.HT_KhoaApiCoSo WHERE ID = @ID)
            BEGIN
                SET @ResultCode = 4;
                SET @ResultMessage = N'Khóa không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            -- Doi noi dung khoa la viec CAP KHOA MOI, khong phai sua khoa cu.
            UPDATE dbo.HT_KhoaApiCoSo
            SET TenKhoa    = @TenKhoa,
                Active     = @Active,
                NgayHetHan = @NgayHetHan
            WHERE ID = @ID;

            SET @IDKhoa = @ID;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Khóa này đã được cấp rồi, hãy sinh khóa khác.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO
