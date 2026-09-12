-- ============================================================================
-- 20 — HT_KhoFtpCoSo: kho FTP CUA PHONG KHAM ma cong CHI DOC ("Kho phieu co so")
--
-- Che do TRO DUONG (ADR 0030): tai lieu da ky so nam san tren FTP cua phong
-- kham; cong khong giu byte, chi giu DUONG roi keo ve khi benh nhan mo.
-- 🔴 Goi dung ten: "TRO DUONG", KHONG goi "day thang" — chu "Day" o CONTEXT.md
-- nghia la HIS chu dong gui byte len, ma che do nay di NGUOC chieu (cong tu keo).
--
-- Vi sao bang rieng (khuon HT_KhoaApiCoSo, script 01):
--   * ADR 0022 da bac loi nhet them cot vao DM_CSKCB mot lan roi.
--   * Moi duong GHI bang nay di qua stored (ADR 0008).
--
-- 🔴 KHAC HT_KhoaApiCoSo o HAI diem — dung chep khuon qua tay:
--   1. UNIQUE (IDCoSo): cau hinh kho la 1-1 voi co so (chot 41). HT_KhoaApiCoSo
--      co y 1-nhieu vi khoa API can XOAY (cap khoa moi -> HIS doi -> tat khoa cu);
--      kho FTP khong co nhu cau do.
--   2. MatKhau luu THO, KHONG bam. Khoa API chi can SO SANH nen bam duoc; FTP
--      can dang nhap nen phai doc lai duoc (chot 36, tien le ADR 0005).
--      Bu bang hang rao trong code: KhoCoSoService tu choi moi duong khong quy
--      duoc ve duoi {ThuMucGoc}/{MaCoSo}/CongVan/ (khuon DonAnhService/KhoAnh).
--
-- Chay mot lan, idempotent. Rollback o 99_ROLLBACK.sql.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HT_KhoFtpCoSo')
BEGIN
    CREATE TABLE dbo.HT_KhoFtpCoSo (
        ID          bigint IDENTITY(1,1) NOT NULL,
        IDCoSo      bigint         NOT NULL,
        Host        nvarchar(200)  NOT NULL,
        TaiKhoan    nvarchar(100)  NOT NULL,
        -- 🔴 THO, co y. Xem ghi chu dau file.
        MatKhau     nvarchar(200)  NOT NULL,
        -- Thu muc goc tren FTP cua phong kham. Mac dinh RONG vi do 135/135 duong
        -- URLKySo tren Dev_Master3 deu la duong tuong doi tinh tu goc FTP
        -- ("77121/CongVan/..."), khong co tien to nao (chot 39).
        ThuMucGoc   nvarchar(200)  NOT NULL CONSTRAINT DF_HT_KhoFtpCoSo_ThuMucGoc DEFAULT (N''),
        -- Mac dinh TAT. Chot 41: chua Thu ket noi dat thi khong bat duoc.
        Active      bit            NOT NULL CONSTRAINT DF_HT_KhoFtpCoSo_Active    DEFAULT (0),
        NgayThuDat  datetime       NULL,
        NgayTao     datetime       NOT NULL CONSTRAINT DF_HT_KhoFtpCoSo_NgayTao   DEFAULT (GETDATE()),
        NgaySua     datetime       NULL,
        CONSTRAINT PK_HT_KhoFtpCoSo PRIMARY KEY CLUSTERED (ID ASC),
        CONSTRAINT FK_HT_KhoFtpCoSo_DM_CSKCB FOREIGN KEY (IDCoSo) REFERENCES dbo.DM_CSKCB (ID)
    );

    -- 🔴 Rang buoc quan trong nhat cua bang nay: MOT co so chi mot kho.
    CREATE UNIQUE NONCLUSTERED INDEX UK_HT_KhoFtpCoSo_IDCoSo
        ON dbo.HT_KhoFtpCoSo (IDCoSo);
END;
GO

-- ---------------------------------------------------------------------------
-- Cap / sua cau hinh kho cua MOT co so. @ID = 0 la them moi.
--
-- Khuon: HT_KhoaApiCoSo_Save (script 01) — OUTPUT @ResultCode/@ResultMessage,
-- SET XACT_ABORT ON, BEGIN TRY/CATCH, bat 2601/2627 ra ma rieng.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.HT_KhoFtpCoSo_Save
    @ID            bigint,
    @IDCoSo        bigint,
    @Host          nvarchar(200),
    @TaiKhoan      nvarchar(100),
    @MatKhau       nvarchar(200),
    @ThuMucGoc     nvarchar(200)  = N'',
    @Active        bit            = 0,
    @IDKho         bigint         OUTPUT,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDKho = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @IDCoSo)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Cơ sở khám chữa bệnh không tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @Host IS NULL OR LTRIM(RTRIM(@Host)) = ''
           OR @TaiKhoan IS NULL OR LTRIM(RTRIM(@TaiKhoan)) = ''
           OR @MatKhau IS NULL OR LTRIM(RTRIM(@MatKhau)) = ''
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage = N'Phải nhập đủ Máy chủ, Tài khoản và Mật khẩu của kho.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DECLARE @IDHienCo bigint = (SELECT TOP 1 ID FROM dbo.HT_KhoFtpCoSo WHERE IDCoSo = @IDCoSo);

        -- Man Sua co so khong cam @ID (moi co so chi mot kho) => tu nhan dang.
        IF @ID = 0 AND @IDHienCo IS NOT NULL SET @ID = @IDHienCo;

        IF @ID <> 0 AND NOT EXISTS (SELECT 1 FROM dbo.HT_KhoFtpCoSo WHERE ID = @ID)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Cấu hình kho không tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- 🔴 Chot 41: chua Thu ket noi DAT thi khong duoc bat. Doi Host/TaiKhoan/
        -- MatKhau la lan thu cu HET HIEU LUC — phai thu lai.
        DECLARE @ThuDatCu    datetime = NULL;
        DECLARE @DoiKetNoi   bit      = 0;

        IF @ID <> 0
        BEGIN
            SELECT @ThuDatCu = NgayThuDat,
                   @DoiKetNoi = CASE WHEN Host = @Host
                                      AND TaiKhoan = @TaiKhoan
                                      AND MatKhau = @MatKhau
                                      AND ThuMucGoc = ISNULL(@ThuMucGoc, N'')
                                     THEN 0 ELSE 1 END
            FROM dbo.HT_KhoFtpCoSo WHERE ID = @ID;
        END;

        DECLARE @ThuDatMoi datetime = CASE WHEN @DoiKetNoi = 1 THEN NULL ELSE @ThuDatCu END;

        IF @Active = 1 AND @ThuDatMoi IS NULL
        BEGIN
            SET @ResultCode = 6;
            SET @ResultMessage = N'Phải Thử kết nối kho đạt trước khi bật.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @ID = 0
        BEGIN
            INSERT INTO dbo.HT_KhoFtpCoSo (IDCoSo, Host, TaiKhoan, MatKhau, ThuMucGoc, Active, NgayThuDat, NgayTao)
            VALUES (@IDCoSo, @Host, @TaiKhoan, @MatKhau, ISNULL(@ThuMucGoc, N''), 0, NULL, GETDATE());

            SET @IDKho = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE dbo.HT_KhoFtpCoSo
            SET Host       = @Host,
                TaiKhoan   = @TaiKhoan,
                MatKhau    = @MatKhau,
                ThuMucGoc  = ISNULL(@ThuMucGoc, N''),
                Active     = @Active,
                NgayThuDat = @ThuDatMoi,
                NgaySua    = GETDATE()
            WHERE ID = @ID;

            SET @IDKho = @ID;
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
            SET @ResultMessage = N'Cơ sở này đã có cấu hình kho rồi — mỗi cơ sở chỉ một kho.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- ---------------------------------------------------------------------------
-- Nut "Thu ket noi kho" bam dat thi goi cai nay. Tach rieng khoi _Save de
-- khong ai bat duoc Active bang cach tu ghi NgayThuDat kem trong mot luot luu.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.HT_KhoFtpCoSo_GhiNhanThuDat
    @IDCoSo        bigint,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.HT_KhoFtpCoSo WHERE IDCoSo = @IDCoSo)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Cơ sở chưa có cấu hình kho để ghi nhận.';
            RETURN;
        END;

        UPDATE dbo.HT_KhoFtpCoSo
        SET NgayThuDat = GETDATE()
        WHERE IDCoSo = @IDCoSo;

        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO
