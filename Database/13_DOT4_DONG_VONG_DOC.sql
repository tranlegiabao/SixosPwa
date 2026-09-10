-- ============================================================================
-- 13 — Dot 4: DONG VONG DOC (noi ho so khi luu + Lich kham cua toi)
--
-- CHAY TREN: HIS_CSKH (co so du lieu cua CONG). KHONG chay ben HIS.
--
-- 🔴 CHAY LAI DUOC tung buoc (IF EXISTS / IF NOT EXISTS). Nho bai hoc Dot 2:
--    moi GO la mot BATCH RIENG — loi giua file KHONG dung cac batch sau, nen
--    CSDL co the vao trang thai nua voi. Sau khi chay phai DO LAI, dung tin la
--    da xong.
--
-- Gom bon viec:
--   (1) DM_BenhNhan  + GioiTinh        — o thu TU cua *Luat gop ho so* (ADR 0018
--       ban sua doi 2026-09-09: ba o -> bon o). Do that: 348 nhom trung ho ten +
--       ngay sinh + gioi tinh ma CCCD hop le KHAC NHAU => bo CCCD ra la gop nham
--       nguoi; nhung bo GIOI TINH ra cung khong du chat.
--   (2) DM_BenhNhanCoSo + NgayXemLichCuoi — *Moc xem lich* (ADR 0025). MOT COT,
--       khong phai mot bang "da doc tung muc": lich hen la mot TRANG THAI xem di
--       xem lai, khong phai su kien duoc day toi. Muc nao sinh sau moc thi deo
--       huy hieu MOI. He qua co y: mo mot lan la sach ca danh sach, con hen BI
--       DOI NGAY thi TU BAT LAI moi (vi NgayKe/NgayCapNhat nhich len).
--   (3) DM_BenhNhan_Save nhan them @GioiTinh (tuy chon — moi cho dang goi khong
--       phai sua) + DM_BenhNhan_SuaHoSo cho man *Sua ho so* MOI.
--   (4) DM_BenhNhanCoSo_GoNoi + DM_BenhNhanCoSo_DoiMocXemLich.
--
-- ROLLBACK doi xung: xem doan "--- Nguoc 13 ---" trong 99_ROLLBACK.sql.
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- (1) DM_BenhNhan.GioiTinh
--
-- Kieu varchar(10) chu khong bit/int: gia tri la MA GIOI TINH cua HIS
-- (DM_GioiTinh.MaGioiTinh = '1' Nam, '2' Nu, '3' Chua xac dinh). Giu nguyen
-- chuoi cua HIS de khoi phai dich hai chieu — dich la them mot cho de lech.
-- NULL = chua biet (ho so cu, ho so tu de ra luc dang ky bang OTP).
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'GioiTinh')
    ALTER TABLE dbo.DM_BenhNhan ADD GioiTinh varchar(10) NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- (1b) DM_DoiTacApi.KhoaGoiHIS — khoa B, cong dung de goi NGUOC vao HIS
--
-- 🔴 CHO TRONG CUA DOT 3, lo ra khi Dot 4 dung that. Bo `.sql` cu chi lo mot
-- chieu: `10_SEED_COSO_HIS.sql` nhan @KhoaTho roi BAM vao HT_KhoaApiCoSo — do la
-- khoa A (HIS goi LEN cong). Khoa B (cong goi VAO HIS, doi dau
-- ThongTinDoanhNghiep.SpwaKhoaNhanBam ben HIS) thi KHONG CO CHO NAO LUU: man
-- KiemTraHis nhan no qua query string vi no chi goi thu mot lan. Duong doc that
-- cua Dot 4 chay khong co nguoi go tay => phai luu.
--
-- Luu THO, khong bam — phai gui nguyen van trong header X-API-Key thi ben kia
-- moi bam ra de so. Cung ly do ThongTinDoanhNghiep.SpwaKhoaXacThuc ben HIS luu
-- tho, va cung le voi TaiKhoanDoiTac.MatKhau (ADR 0005).
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.DM_DoiTacApi') AND name = 'KhoaGoiHIS')
    ALTER TABLE dbo.DM_DoiTacApi ADD KhoaGoiHIS nvarchar(500) NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- (2) DM_BenhNhanCoSo.NgayXemLichCuoi — *Moc xem lich*
--
-- datetime + GETDATE() theo le nha (KHONG datetime2/SYSDATETIME: CSDL khach
-- chay ban SQL Server cu). NULL = chua mo lan nao => moi muc deu la MOI.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'NgayXemLichCuoi')
    ALTER TABLE dbo.DM_BenhNhanCoSo ADD NgayXemLichCuoi datetime NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- (3a) DM_BenhNhan_Save — them @GioiTinh
--
-- Chep nguyen ban 09 roi them DUNG mot tham so + mot o trong INSERT/UPDATE.
-- Tham so TUY CHON va dat SAU cac tham so cu nhung TRUOC cac tham so OUTPUT;
-- moi cho goi deu truyen theo TEN nen thu tu khong pha gi.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_Save
    @CCCD          varchar(20),
    @TenBN         nvarchar(100),
    @SDT           varchar(20)   = NULL,
    @Email         varchar(100)  = NULL,
    @DiaChi        nvarchar(255) = NULL,
    @IDTaiKhoan    bigint        = NULL,
    @NgaySinh      datetime      = NULL,
    @HoTenKhongDau nvarchar(100) = NULL,
    @GioiTinh      varchar(10)   = NULL,
    @IDBenhNhan    bigint         OUTPUT,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDBenhNhan = NULL;

    BEGIN TRY
        DECLARE @cccdSach varchar(20) = NULLIF(LTRIM(RTRIM(@CCCD)), '');
        IF @cccdSach IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Thiếu số căn cước công dân.';
            RETURN;
        END;

        BEGIN TRANSACTION;

        DECLARE @chuSoHuuHienTai bigint;

        SELECT @IDBenhNhan = ID, @chuSoHuuHienTai = IDTaiKhoan
        FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
        WHERE CCCD = @cccdSach;

        IF @IDBenhNhan IS NULL
        BEGIN
            INSERT INTO dbo.DM_BenhNhan
                (CCCD, TenBN, SDT, Email, DiaChi, IDTaiKhoan, NgaySinh, HoTenKhongDau, GioiTinh)
            VALUES
                (@cccdSach, @TenBN, @SDT, @Email, @DiaChi, @IDTaiKhoan, @NgaySinh, @HoTenKhongDau, @GioiTinh);
            SET @IDBenhNhan = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            IF @IDTaiKhoan IS NOT NULL
               AND @chuSoHuuHienTai IS NOT NULL
               AND @chuSoHuuHienTai <> @IDTaiKhoan
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage =
                    N'Số căn cước này đã được một tài khoản khác khai trước. ' +
                    N'Nếu đó là người thân của bạn, hãy nhờ họ vào mục "Hồ sơ của tôi" và xoá hồ sơ đó để nhả căn cước ra; ' +
                    N'nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được xử lý.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            /* Chi ghi de bang gia tri thuc su co — dung xoa trang du lieu cu */
            UPDATE dbo.DM_BenhNhan
               SET TenBN         = ISNULL(NULLIF(LTRIM(RTRIM(@TenBN)), N''), TenBN),
                   SDT           = ISNULL(@SDT,   SDT),
                   Email         = ISNULL(@Email, Email),
                   DiaChi        = ISNULL(@DiaChi, DiaChi),
                   NgaySinh      = ISNULL(@NgaySinh, NgaySinh),
                   HoTenKhongDau = ISNULL(@HoTenKhongDau, HoTenKhongDau),
                   GioiTinh      = ISNULL(NULLIF(LTRIM(RTRIM(@GioiTinh)), ''), GioiTinh),
                   -- Chi nhan chu so huu khi dang bo trong.
                   IDTaiKhoan    = ISNULL(IDTaiKhoan, @IDTaiKhoan)
             WHERE ID = @IDBenhNhan;
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
            SET @ResultMessage = N'Số căn cước này đã thuộc về một người khác.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- (3b) DM_BenhNhan_SuaHoSo — man *Sua ho so* MOI (Dot 4, ADR 0024 ve 1)
--
-- 🔴 VI SAO KHONG DUNG LAI DM_BenhNhan_Save: cai do nhan dien bang CCCD. Man
-- *Sua ho so* thi nguoi dung DOI DUOC ca CCCD, nen phai nhan dien bang ID va
-- kiem chu so huu. Goi Save voi CCCD moi se de ra mot CON NGUOI THU HAI thay vi
-- sua nguoi dang co.
--
-- Ba phep chan, theo thu tu tu re den dat:
--   1. Ho so phai thuoc tai khoan dang goi (ResultCode 5).
--   2. Ho so DA NOI HIS thi bon o danh tinh KHOA CUNG — chung doc tu HIS, va
--      chung dung la bon o cua *Luat gop ho so*. Chi con SDT sua duoc.
--   3. CCCD moi khong duoc dam vao nguoi khac (ResultCode 3 — "ai khai truoc giu").
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_SuaHoSo
    @IDBenhNhan    bigint,
    @IDTaiKhoan    bigint,
    @CCCD          varchar(20)   = NULL,
    @TenBN         nvarchar(100) = NULL,
    @SDT           varchar(20)   = NULL,
    @NgaySinh      datetime      = NULL,
    @HoTenKhongDau nvarchar(100) = NULL,
    @GioiTinh      varchar(10)   = NULL,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @chuSoHuu bigint;

        SELECT @chuSoHuu = IDTaiKhoan
        FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
        WHERE ID = @IDBenhNhan;

        IF @chuSoHuu IS NULL AND NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan WHERE ID = @IDBenhNhan)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Không tìm thấy hồ sơ.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- 🔴 Cong chan. Thieu phep kiem nay thi go ID ho so nguoi khac vao la SUA
        -- duoc danh tinh cua ho — nang hon ca lo hong "xem duoc benh an".
        IF @chuSoHuu IS NULL OR @chuSoHuu <> @IDTaiKhoan
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Hồ sơ này không thuộc tài khoản của bạn.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DECLARE @daNoi bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                                                WHERE IDBenhNhan = @IDBenhNhan AND MaBN IS NOT NULL)
                                  THEN 1 ELSE 0 END;

        IF @daNoi = 1
        BEGIN
            -- Ho so da noi: bon o danh tinh doc tu HIS => khoa cung, chi cho SDT.
            UPDATE dbo.DM_BenhNhan
               SET SDT = @SDT
             WHERE ID = @IDBenhNhan;

            COMMIT TRANSACTION;
            SET @ResultCode = 1;
            SET @ResultMessage = N'OK';
            RETURN;
        END;

        DECLARE @cccdSach varchar(20) = NULLIF(LTRIM(RTRIM(@CCCD)), '');

        IF @cccdSach IS NOT NULL
           AND EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE CCCD = @cccdSach AND ID <> @IDBenhNhan)
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage =
                N'Số căn cước này đã được một tài khoản khác khai trước. ' +
                N'Nếu đó là người thân của bạn, hãy nhờ họ vào mục "Hồ sơ của tôi" và xoá hồ sơ đó để nhả căn cước ra; ' +
                N'nếu bạn cho rằng có nhầm lẫn, liên hệ cơ sở khám chữa bệnh để được xử lý.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- Ho so tu khai: sua duoc moi o. SDT gan THANG (khong ISNULL) de nguoi
        -- dung xoa trang duoc o do; bon o danh tinh thi giu ban cu khi ben goi
        -- de trong, vi de trong o day nghia la "khong dong toi".
        UPDATE dbo.DM_BenhNhan
           SET CCCD          = ISNULL(@cccdSach, CCCD),
               TenBN         = ISNULL(NULLIF(LTRIM(RTRIM(@TenBN)), N''), TenBN),
               SDT           = @SDT,
               NgaySinh      = ISNULL(@NgaySinh, NgaySinh),
               HoTenKhongDau = ISNULL(@HoTenKhongDau, HoTenKhongDau),
               GioiTinh      = ISNULL(NULLIF(LTRIM(RTRIM(@GioiTinh)), ''), GioiTinh)
         WHERE ID = @IDBenhNhan;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Số căn cước này đã thuộc về một người khác.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- (4a) DM_BenhNhanCoSo_GoNoi — thao MOT ma khoi mot ho so (ADR 0024 ve 3)
--
-- 🔴 XOA CA TAI LIEU VA DOT KHAM cua dong do, khong chi xoa dong. Ba ly do:
--   * FK bam vao la NO_ACTION (da do: FK_QL_TaiLieuBenhNhan_DM_BenhNhanCoSo,
--     FK_QL_DotKham_DM_BenhNhanCoSo) nen khong xoa dong duoc chung nao con con.
--   * Neu ma bi noi NHAM thi tai lieu do dang la benh an NGUOI KHAC — de lai
--     chinh la giu nguyen cai hai ma nut *Go noi* sinh ra de chua.
--   * Mat khong vinh vien: khoa tu nhien (IDCoSo, LoaiTaiLieu, MaNguonHIS) con
--     nguyen ben HIS, va cua `kiem-tra-nhan` cua Dot 2 se thay ma nay tro lai
--     trang thai "chua ai nhan" => hang doi ben HIS day lai duoc.
-- Van SAO LUU sang schema bak truoc khi xoa, theo le nha (bak.*_V001).
--
-- Het ma tai co so do thi DUNG LAI mot dong TU KHAI — neu khong, ho so bien mat
-- khoi co so chu khong "tut ve *Ho so tu khai*" nhu ADR noi.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'bak')
    EXEC('CREATE SCHEMA bak');
GO

-- 🔴 UNION ALL trong nguon KHONG phai de thua: `SELECT ... INTO` GIU LAI thuoc
-- tinh IDENTITY cua cot ID, va roi `INSERT INTO bak.X SELECT * FROM dbo.T` se
-- chet `Msg 8101` (An explicit value for the identity column...). Bat duoc luc
-- chay that 2026-09-09 — va dung kieu loi khong dung cac batch sau, nen sau khi
-- chay phai DO LAI chu dung tin la xong.
-- UNION ALL lam ket qua thanh bieu thuc, IDENTITY roi ra, cot ID thanh bigint
-- thuong. Bang van RONG vi TOP 0.
--
-- ⚠️ Bang sao luu chup theo hinh dang bang goc TAI THOI DIEM TAO. Bang goc them
-- cot ve sau thi `INSERT ... SELECT *` se lech so cot => thu tuc *Go noi* bao loi.
-- Doi lược do bang nao trong ba bang duoi thi DROP bang bak tuong ung cho no
-- sinh lai (chung chi giu dau vet, khong phai nguon su that).

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
           WHERE s.name = 'bak' AND t.name = 'GoNoi_TaiLieu_V001')
   AND EXISTS (SELECT 1 FROM sys.identity_columns
               WHERE object_id = OBJECT_ID(N'bak.GoNoi_TaiLieu_V001'))
    DROP TABLE bak.GoNoi_TaiLieu_V001;   -- ban cu con IDENTITY, dung duoc
GO
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = 'bak' AND t.name = 'GoNoi_TaiLieu_V001')
    SELECT TOP 0 q.*, CAST(NULL AS datetime) AS NgayGoNoi
      INTO bak.GoNoi_TaiLieu_V001
      FROM (SELECT * FROM dbo.QL_TaiLieuBenhNhan
            UNION ALL
            SELECT * FROM dbo.QL_TaiLieuBenhNhan) q;
GO

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
           WHERE s.name = 'bak' AND t.name = 'GoNoi_DotKham_V001')
   AND EXISTS (SELECT 1 FROM sys.identity_columns
               WHERE object_id = OBJECT_ID(N'bak.GoNoi_DotKham_V001'))
    DROP TABLE bak.GoNoi_DotKham_V001;
GO
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = 'bak' AND t.name = 'GoNoi_DotKham_V001')
    SELECT TOP 0 q.*, CAST(NULL AS datetime) AS NgayGoNoi
      INTO bak.GoNoi_DotKham_V001
      FROM (SELECT * FROM dbo.QL_DotKham
            UNION ALL
            SELECT * FROM dbo.QL_DotKham) q;
GO

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
           WHERE s.name = 'bak' AND t.name = 'GoNoi_HoSoCoSo_V001')
   AND EXISTS (SELECT 1 FROM sys.identity_columns
               WHERE object_id = OBJECT_ID(N'bak.GoNoi_HoSoCoSo_V001'))
    DROP TABLE bak.GoNoi_HoSoCoSo_V001;
GO
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = 'bak' AND t.name = 'GoNoi_HoSoCoSo_V001')
    SELECT TOP 0 q.*, CAST(NULL AS datetime) AS NgayGoNoi
      INTO bak.GoNoi_HoSoCoSo_V001
      FROM (SELECT * FROM dbo.DM_BenhNhanCoSo
            UNION ALL
            SELECT * FROM dbo.DM_BenhNhanCoSo) q;
GO

CREATE OR ALTER PROCEDURE dbo.DM_BenhNhanCoSo_GoNoi
    @IDBenhNhanCoSo bigint,
    @IDTaiKhoan     bigint,
    @ResultCode     int            OUTPUT,
    @ResultMessage  nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @idBenhNhan bigint, @idCoSo bigint, @maBN varchar(20), @chuSoHuu bigint;

        SELECT @idBenhNhan = h.IDBenhNhan,
               @idCoSo     = h.IDCoSo,
               @maBN       = h.MaBN,
               @chuSoHuu   = b.IDTaiKhoan
          FROM dbo.DM_BenhNhanCoSo h WITH (UPDLOCK, HOLDLOCK)
          JOIN dbo.DM_BenhNhan     b ON b.ID = h.IDBenhNhan
         WHERE h.ID = @IDBenhNhanCoSo;

        IF @idBenhNhan IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Không tìm thấy hồ sơ tại cơ sở.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        -- 🔴 Cong chan — cung ly do voi HoSoThuocTaiKhoanAsync.
        IF @chuSoHuu IS NULL OR @chuSoHuu <> @IDTaiKhoan
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Hồ sơ này không thuộc tài khoản của bạn.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @maBN IS NULL
        BEGIN
            SET @ResultCode = 6;
            SET @ResultMessage = N'Hồ sơ này chưa nối mã nào nên không có gì để gỡ.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DECLARE @bayGio datetime = GETDATE();

        INSERT INTO bak.GoNoi_TaiLieu_V001
        SELECT *, @bayGio FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhanCoSo = @IDBenhNhanCoSo;

        INSERT INTO bak.GoNoi_DotKham_V001
        SELECT *, @bayGio FROM dbo.QL_DotKham WHERE IDBenhNhanCoSo = @IDBenhNhanCoSo;

        INSERT INTO bak.GoNoi_HoSoCoSo_V001
        SELECT *, @bayGio FROM dbo.DM_BenhNhanCoSo WHERE ID = @IDBenhNhanCoSo;

        DELETE FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhanCoSo = @IDBenhNhanCoSo;
        DELETE FROM dbo.QL_DotKham         WHERE IDBenhNhanCoSo = @IDBenhNhanCoSo;
        DELETE FROM dbo.DM_BenhNhanCoSo    WHERE ID = @IDBenhNhanCoSo;

        -- Het cho dung tai co so nay => dung lai mot dong TU KHAI de ho so "tut
        -- ve *Ho so tu khai*" chu khong bien mat.
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                        WHERE IDBenhNhan = @idBenhNhan AND IDCoSo = @idCoSo)
        BEGIN
            INSERT INTO dbo.DM_BenhNhanCoSo (IDBenhNhan, IDCoSo, MaBN, DaMoTaiLieu)
            VALUES (@idBenhNhan, @idCoSo, NULL, 1);
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

-- ─────────────────────────────────────────────────────────────────────────────
-- (4b) DM_BenhNhanCoSo_DoiMocXemLich — doi *Moc xem lich* (ADR 0025)
--
-- Doi cho TAT CA dong cua mot CON NGUOI tai MOT co so, khong phai tung dong: o
-- *Lich kham cua toi* gop het cac ma cua ho so lai roi hien mot danh sach, nen
-- "da xem" cung phai la mot trang thai duy nhat. Doi tung dong thi mo modal xong
-- huy hieu van con — dung loai loi im lang khong ai lan ra duoc.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhanCoSo_DoiMocXemLich
    @IDBenhNhan    bigint,
    @IDCoSo        bigint,
    @IDTaiKhoan    bigint,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE ID = @IDBenhNhan AND IDTaiKhoan = @IDTaiKhoan)
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Hồ sơ này không thuộc tài khoản của bạn.';
            RETURN;
        END;

        UPDATE dbo.DM_BenhNhanCoSo
           SET NgayXemLichCuoi = GETDATE()
         WHERE IDBenhNhan = @IDBenhNhan AND IDCoSo = @IDCoSo;

        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

-- ============================================================================
-- DO LAI SAU KHI CHAY — dung tin la xong, moi GO la mot batch doc lap.
-- ============================================================================
SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.columns
                          WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'GioiTinh')
            THEN N'[OK] DM_BenhNhan.GioiTinh' ELSE N'[THIEU] DM_BenhNhan.GioiTinh' END AS KetQua
UNION ALL
SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.columns
                          WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'NgayXemLichCuoi')
            THEN N'[OK] DM_BenhNhanCoSo.NgayXemLichCuoi' ELSE N'[THIEU] DM_BenhNhanCoSo.NgayXemLichCuoi' END
UNION ALL
SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.columns
                          WHERE object_id = OBJECT_ID(N'dbo.DM_DoiTacApi') AND name = 'KhoaGoiHIS')
            THEN N'[OK] DM_DoiTacApi.KhoaGoiHIS' ELSE N'[THIEU] DM_DoiTacApi.KhoaGoiHIS' END
UNION ALL
SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.parameters
                          WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan_Save') AND name = '@GioiTinh')
            THEN N'[OK] DM_BenhNhan_Save co @GioiTinh' ELSE N'[THIEU] DM_BenhNhan_Save @GioiTinh' END
UNION ALL
SELECT CASE WHEN OBJECT_ID(N'dbo.DM_BenhNhan_SuaHoSo', 'P') IS NOT NULL
            THEN N'[OK] DM_BenhNhan_SuaHoSo' ELSE N'[THIEU] DM_BenhNhan_SuaHoSo' END
UNION ALL
SELECT CASE WHEN OBJECT_ID(N'dbo.DM_BenhNhanCoSo_GoNoi', 'P') IS NOT NULL
            THEN N'[OK] DM_BenhNhanCoSo_GoNoi' ELSE N'[THIEU] DM_BenhNhanCoSo_GoNoi' END
UNION ALL
SELECT CASE WHEN OBJECT_ID(N'dbo.DM_BenhNhanCoSo_DoiMocXemLich', 'P') IS NOT NULL
            THEN N'[OK] DM_BenhNhanCoSo_DoiMocXemLich' ELSE N'[THIEU] DM_BenhNhanCoSo_DoiMocXemLich' END;
GO
