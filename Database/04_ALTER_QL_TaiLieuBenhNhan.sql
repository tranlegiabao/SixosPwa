-- ============================================================================
-- 04 — Siet QL_TaiLieuBenhNhan theo chot dot 2
--
-- Ba viec:
--   (1) IDBenhNhanCoSo -> NOT NULL. Chot 3: tai lieu cua MaBN CHUA co ho so noi
--       thi bi TU CHOI, khong nam lai o cong. Cong khong giu mot byte benh an
--       nao cua nguoi chua la nguoi dung.
--   (2) Khoa tu nhien (IDCoSo, LoaiTaiLieu, MaNguonHIS) + giu PHIEN BAN.
--       Day lai cung noi dung => khong de dong thu hai. Ket qua bi sua/ky lai
--       => them phien ban moi, chi ban moi nhat duoc hien.
--   (3) MaBN doi nvarchar(50) -> varchar(20) cho KHOP DM_BenhNhanCoSo.MaBN.
--       Dang lech kieu => moi phep so sanh deu implicit convert.
--
-- 🔴 CHAY LAI DUOC. Lan chay dau (08/09) gay o buoc siet cot vi thieu doan go
--    index — Msg 5074 + Msg 4922. Moi GO la mot batch rieng nen cac batch SAU
--    van chay tiep, bang do o trang thai nua voi: da don mo coi + da them 3 cot
--    + da co UK_..._Nguon + stored da co @MaNguonHIS, nhung IDBenhNhanCoSo van
--    NULL duoc va MaBN van la nvarchar(50). Ban nay da sua va toan bo buoc deu
--    co IF EXISTS, nen cu chay lai ca file — buoc nao xong roi se tu bo qua.
--
-- DO THAT truoc khi viet script (2026-09-08, HIS_CSKH@118):
--    12 dong tong, 3 dong IDBenhNhanCoSo IS NULL. Ba dong mo coi do duoc CHEP
--    sang bak.QL_TaiLieuBenhNhan_MoCoi_V001 roi moi xoa — dung quy uoc bak.*
--    da co san trong CSDL nay.
-- ============================================================================

-- --- Kiem truoc khi chay: bao nhieu dong se bi don ---------------------------
PRINT '--- Truoc khi chay ---';
SELECT  TongDong        = COUNT(*),
        MoCoiSeDon      = SUM(CASE WHEN IDBenhNhanCoSo IS NULL THEN 1 ELSE 0 END),
        MaBNDaiHon20    = SUM(CASE WHEN LEN(MaBN) > 20 THEN 1 ELSE 0 END)
FROM dbo.QL_TaiLieuBenhNhan;
GO

-- --- (1) Don dong mo coi -----------------------------------------------------
IF EXISTS (SELECT 1 FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhanCoSo IS NULL)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.tables t
                   JOIN sys.schemas s ON s.schema_id = t.schema_id
                   WHERE s.name = 'bak' AND t.name = 'QL_TaiLieuBenhNhan_MoCoi_V001')
    BEGIN
        SELECT * INTO bak.QL_TaiLieuBenhNhan_MoCoi_V001
        FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhanCoSo IS NULL;
    END;

    DELETE FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhanCoSo IS NULL;
END;
GO

-- Tep tren FTP cua nhung dong vua don KHONG bi xoa tu dong: duong dan con nam
-- trong bang bak de xoa tay neu can. Xoa tep la thao tac mot chieu.

-- --- (2) Siet cot ------------------------------------------------------------
-- 🔴 KHONG ALTER COLUMN duoc chung nao con index bam vao cot do. Script cua
-- dong nghiep tao HAI index nam dung tren hai cot ta phai sua:
--     IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo  (loc WHERE IDBenhNhanCoSo IS NOT NULL)
--     IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN     (chua cot MaBN)
-- Bo qua buoc go la lan chay bao Msg 5074 + Msg 4922, va vi moi GO la mot batch
-- rieng nen cac batch SAU do van chay tiep => bang vao trang thai nua voi.
-- Phai go index xuong, doi cot, roi dung lai.

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo'
             AND object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan'))
    DROP INDEX IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo ON dbo.QL_TaiLieuBenhNhan;
GO
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN'
             AND object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan'))
    DROP INDEX IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN ON dbo.QL_TaiLieuBenhNhan;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan')
             AND name = 'IDBenhNhanCoSo' AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.QL_TaiLieuBenhNhan ALTER COLUMN IDBenhNhanCoSo bigint NOT NULL;
END;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan')
             AND name = 'MaBN' AND system_type_id = TYPE_ID('nvarchar'))
   AND NOT EXISTS (SELECT 1 FROM dbo.QL_TaiLieuBenhNhan WHERE LEN(MaBN) > 20)
BEGIN
    ALTER TABLE dbo.QL_TaiLieuBenhNhan ALTER COLUMN MaBN varchar(20) NOT NULL;
END;
GO

-- Dung lai hai index vua go. Cai theo IDBenhNhanCoSo dung lai KHONG CON BO LOC:
-- cot da NOT NULL nen dieu kien "IS NOT NULL" khong loc gi nua, ma index co loc
-- thi bo toi uu khong dung duoc cho moi truy van.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN'
                 AND object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan'))
    CREATE NONCLUSTERED INDEX IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN
        ON dbo.QL_TaiLieuBenhNhan (IDCoSo, MaBN);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo'
                 AND object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan'))
    CREATE NONCLUSTERED INDEX IX_QL_TaiLieuBenhNhan_IdBenhNhanCoSo
        ON dbo.QL_TaiLieuBenhNhan (IDBenhNhanCoSo);
GO

-- --- (3) Cot phien ban -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND name = 'MaNguonHIS')
    ALTER TABLE dbo.QL_TaiLieuBenhNhan ADD MaNguonHIS varchar(50) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND name = 'PhienBan')
    ALTER TABLE dbo.QL_TaiLieuBenhNhan ADD PhienBan int NOT NULL CONSTRAINT DF_QL_TaiLieuBenhNhan_PhienBan DEFAULT (1);
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND name = 'LaBanMoiNhat')
    ALTER TABLE dbo.QL_TaiLieuBenhNhan ADD LaBanMoiNhat bit NOT NULL CONSTRAINT DF_QL_TaiLieuBenhNhan_LaBanMoiNhat DEFAULT (1);
GO

-- Unique CO LOC: chi rang buoc ban moi nhat, va chi khi HIS co gui MaNguonHIS.
-- 9 dong cu khong co MaNguonHIS nen nam ngoai rang buoc, khong can don.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_QL_TaiLieuBenhNhan_Nguon')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UK_QL_TaiLieuBenhNhan_Nguon
        ON dbo.QL_TaiLieuBenhNhan (IDCoSo, LoaiTaiLieu, MaNguonHIS)
        WHERE MaNguonHIS IS NOT NULL AND LaBanMoiNhat = 1;
END;
GO

-- ---------------------------------------------------------------------------
-- Thu tuc luu — VIET DE, GIU NGUYEN chu ky cu roi THEM tham so tuy chon.
-- Code C# hien tai cua khu tai lieu goi khong co @MaNguonHIS van chay duoc.
--
-- Luat TU CHOI duoc chan ngay o day chu khong chi o C#: @IDBenhNhanCoSo NULL
-- hay khong thuoc co so => ResultCode 5. Nhu vay chot 3 van dung ke ca khi
-- tang C# chua kip va.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.QL_TaiLieuBenhNhan_Save
    @ID             BIGINT,
    @IDCoSo         BIGINT,
    @IDBenhNhanCoSo BIGINT = NULL,
    @MaBN           NVARCHAR(50),
    @LoaiTaiLieu    NVARCHAR(50),
    @TenTaiLieu     NVARCHAR(255),
    @DuongDanFtp    NVARCHAR(500),
    @DungLuongByte  BIGINT,
    @NgayKham       DATETIME = NULL,
    @GhiChu         NVARCHAR(MAX) = NULL,
    @MaNguonHIS     VARCHAR(50) = NULL,
    @IDTaiLieu      BIGINT OUTPUT,
    @ResultCode     INT OUTPUT,
    @ResultMessage  NVARCHAR(4000) OUTPUT
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

        -- Chot 3: khong co ho so noi thi TU CHOI, khong luu.
        IF @IDBenhNhanCoSo IS NULL
           OR NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                          WHERE ID = @IDBenhNhanCoSo AND IDCoSo = @IDCoSo)
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Mã bệnh nhân này chưa có hồ sơ nào nhận tại cổng.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @ID = 0
        BEGIN
            DECLARE @PhienBan int = 1;

            IF @MaNguonHIS IS NOT NULL
            BEGIN
                -- Cung nguon => day them mot PHIEN BAN, ha co ban moi nhat cua
                -- cac ban truoc. Chan trung tuyet doi (day lai y het noi dung)
                -- la viec cua tang C#, xem NhanTaiLieu.
                SELECT @PhienBan = ISNULL(MAX(PhienBan), 0) + 1
                FROM dbo.QL_TaiLieuBenhNhan WITH (UPDLOCK, HOLDLOCK)
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu AND MaNguonHIS = @MaNguonHIS;

                UPDATE dbo.QL_TaiLieuBenhNhan
                SET LaBanMoiNhat = 0
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu
                  AND MaNguonHIS = @MaNguonHIS AND LaBanMoiNhat = 1;
            END;

            INSERT INTO dbo.QL_TaiLieuBenhNhan (
                IDCoSo, IDBenhNhanCoSo, MaBN, LoaiTaiLieu, TenTaiLieu, DuongDanFtp,
                DungLuongByte, NgayKham, GhiChu, MaNguonHIS, PhienBan, LaBanMoiNhat, NgayTao)
            VALUES (
                @IDCoSo, @IDBenhNhanCoSo, @MaBN, @LoaiTaiLieu, @TenTaiLieu, @DuongDanFtp,
                @DungLuongByte, @NgayKham, @GhiChu, @MaNguonHIS, @PhienBan, 1, GETDATE());

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
            SET IDCoSo = @IDCoSo, IDBenhNhanCoSo = @IDBenhNhanCoSo, MaBN = @MaBN,
                LoaiTaiLieu = @LoaiTaiLieu, TenTaiLieu = @TenTaiLieu,
                DuongDanFtp = @DuongDanFtp, DungLuongByte = @DungLuongByte,
                NgayKham = @NgayKham, GhiChu = @GhiChu
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

PRINT '--- Sau khi chay ---';
SELECT TongDong = COUNT(*) FROM dbo.QL_TaiLieuBenhNhan;
GO
