/* =============================================================================
   DOT 1B - buoc B10a: BAY DOI TUONG HOP DONG LINKED SERVER (ADR 0035)
   ---------------------------------------------------------------------------
   Tach rieng khoi B10b vi day la phan duy nhat ma sai mot dau la HIS chet,
   build van xanh, va khong grep nao ben repo cong nhin thay.

   🔴 CREATE OR ALTER, TUYET DOI KHONG DROP + CREATE.
      DROP PROCEDURE xoa sach moi GRANT tren object -> login spwa_his mat
      EXECUTE ngay lap tuc. Da dap that 16/09.

   🔴 KHONG bot tham so, KHONG doi ten tham so, KHONG doi ten cot tra ve,
      KHONG doi ten stored. Chi duoc THEM tham so co mac dinh.

   Chot cua phien grill 19-09 (xem HOP-DONG-5-CUA.md):
     G1  cot IDTaiKhoanTheoSdt SOI GUONG cot IDBenhNhan          (ADR 0034)
     G2  cua 4 ba nhanh: tai dung -> thang cap -> nhan ban
     G2b nhanh (1) soi guong cua 1 theo ADR 0018 (CCCD gia thi tra nhan than)
     G4  cua 3 im lang; cua 2 ghi 1 dong log roi rc=1, KHONG NEM
   ========================================================================== */
SET NOCOUNT ON;
GO
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NULL
    THROW 50100, 'Chua chay B02 - DM_BenhNhan chua co IDCoSo. DUNG LAI.', 1;
GO

/* ===========================================================================
   #1  CUA 1 - dbo.DM_BenhNhan_Save        (giu 12 tham so)
   ---------------------------------------------------------------------------
   PA-C1. Cua nay KHONG biet co so nao (khong co @IDCoSo va khong duoc them
   tham so bat buoc) => no chi dung DONG NEO: IDCoSo = NULL.

   🔴 CHI duoc dung lai DONG NEO. Tuyet doi khong tra ve mot dong da gan co so:
   HIS hoi lai DoHienTrang ngay sau do, ma DoHienTrang chi nhan dong-tai-co-so
   hoac dong-neo; tra dong cua co so KHAC thi DoHienTrang van tra NULL va HIS
   chet o MA_DA_CO_CHU (S00_UploadOnline:334).

   @IDTaiKhoan: NHAN ROI BO (cot da bi bo o B06). Giu tham so - hop dong.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_Save
    @CCCD          varchar(20),
    @TenBN         nvarchar(100),
    @SDT           varchar(20)   = NULL,
    @Email         varchar(100)  = NULL,
    @DiaChi        nvarchar(255) = NULL,
    @IDTaiKhoan    bigint        = NULL,   -- nhan roi bo (ADR 0035)
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

        DECLARE @laCccdKhongCo bit =
            CASE WHEN @cccdSach IN ('11111111111', '111111111111') THEN 1 ELSE 0 END;

        /* 🔴 Chan ngay sinh 01/01/1900 hoac de trong khi CCCD la ma gia */
        IF @laCccdKhongCo = 1 AND (@NgaySinh IS NULL OR CAST(@NgaySinh AS date) = '1900-01-01')
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage = N'Ngày sinh không hợp lệ. Vui lòng nhập ngày sinh chính xác của bệnh nhân.';
            RETURN;
        END;

        BEGIN TRANSACTION;

        /* --- tim DONG NEO cua chinh nguoi nay (ADR 0018) ------------------ */
        IF @laCccdKhongCo = 1
            SELECT TOP 1 @IDBenhNhan = ID
              FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
             WHERE IDCoSo IS NULL
               AND HoTenKhongDau = @HoTenKhongDau
               AND CAST(NgaySinh AS date) = CAST(@NgaySinh AS date)
               AND GioiTinh = @GioiTinh;
        ELSE
            SELECT TOP 1 @IDBenhNhan = ID
              FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
             WHERE IDCoSo IS NULL AND CCCD = @cccdSach;

        IF @IDBenhNhan IS NULL
        BEGIN
            /* Khong co dong neo -> DUNG MOI. Ke ca khi nguoi nay da co dong o
               mot co so khac: cua 4 se nhan ban, do dung la "chap nhan lap dong". */
            INSERT INTO dbo.DM_BenhNhan
                (IDCoSo, MaBN, CCCD, TenBN, SDT, Email, DiaChi, NgaySinh, HoTenKhongDau, GioiTinh)
            VALUES
                (NULL, NULL, @cccdSach, @TenBN, @SDT, @Email, @DiaChi, @NgaySinh, @HoTenKhongDau, @GioiTinh);
            SET @IDBenhNhan = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            /* Chi ghi de bang gia tri thuc su co - dung xoa trang du lieu cu.
               🔴 GIU cau SDT = ISNULL(@SDT, SDT): C8 (don 807 nhom) dua han vao
               viec nay de biet minh la viec CHAY DINH KY chu khong phai mot lan. */
            UPDATE dbo.DM_BenhNhan
               SET TenBN         = ISNULL(NULLIF(LTRIM(RTRIM(@TenBN)), N''), TenBN),
                   SDT           = ISNULL(@SDT,   SDT),
                   Email         = ISNULL(@Email, Email),
                   DiaChi        = ISNULL(@DiaChi, DiaChi),
                   NgaySinh      = ISNULL(@NgaySinh, NgaySinh),
                   HoTenKhongDau = ISNULL(@HoTenKhongDau, HoTenKhongDau),
                   GioiTinh      = ISNULL(NULLIF(LTRIM(RTRIM(@GioiTinh)), ''), GioiTinh)
             WHERE ID = @IDBenhNhan;
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

/* ===========================================================================
   #2  CUA 2 - dbo.HT_TaiKhoan_Save        (giu 9 tham so)
   ---------------------------------------------------------------------------
   G4. Role='Admin' -> chay Y NHU CU (khu Admin dang dung that:
   AdminStoredProcedureService.cs:40). Vai tro khac -> ghi MOT dong log roi
   tra rc=1.

   🔴 KHONG duoc nem o nhanh ELSE: sau B06 co CK_HT_TaiKhoan_Role CHECK
   (Role='Admin'), de INSERT chay tu nhien la loi 547 -> THROW -> vuot linked
   server -> HIS CATCH -> bao KHONG_TOI_DUOC_CONG, SAI nguyen nhan.

   Vi sao van ghi log: sau G1 cua nay la DUONG CHET (HIS chi goi khi cot 5 la
   NULL, ma cot 5 soi guong cot 2, ma cot 2 luon non-NULL sau cua 1). Tan suat
   ky vong = 0 dong/ngay. No KEU tuc la mot gia dinh cua G1 sai - tin bao chay.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.HT_TaiKhoan_Save
    @ID                  bigint,
    @SDT                 varchar(20),
    @Email               nvarchar(50) = NULL,
    @Role                varchar(20),
    @MatKhauNoiBoDaBam   varchar(255) = NULL,
    @IDBenhNhan          bigint       = NULL,   -- nhan roi bo (ADR 0035)
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
        IF @Role IS NULL OR @Role <> 'Admin'
        BEGIN
            BEGIN TRY
                INSERT INTO dbo.HT_LogApiCoSo (IDCoSo, Endpoint, MaBN, KetQua, LyDo, NgayTao)
                VALUES (NULL, 'HT_TaiKhoan_Save', LEFT(ISNULL(@SDT, ''), 20),
                        'BO_QUA', 'VAI_TRO_KHAI_TU', GETDATE());
            END TRY
            BEGIN CATCH
                /* ghi log hong thi NUOT - khong duoc lam hong loi goi */
            END CATCH;

            SET @ResultCode = 1;
            SET @ResultMessage = N'Vai trò này đã khai tử từ đợt 1B — bệnh nhân không còn tài khoản.';
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
            VALUES (@SDT, @Email, 'Admin', @MatKhauNoiBoDaBam);
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
                   Role  = 'Admin',
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
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ===========================================================================
   #3  CUA 3 - dbo.DM_BenhNhan_NhanChuSoHuu      (giu 4 tham so)
   ---------------------------------------------------------------------------
   G4. NO-OP IM LANG. Cot DM_BenhNhan.IDTaiKhoan da bi bo o B06, va khai niem
   "chu so huu ho so" chet theo (dao ADR 0019 muc 2 + ADR 0027).

   HIS goi cua nay MOI LUOT, khong co IF nao (S00_UploadOnline:385) => tuyet
   doi khong duoc ghi gi o day: ghi la tu bom rac vao dung cai bang vua dung
   may don o B10b.

   🔴 GIU stored nay lai, dung DROP: no nam trong hop dong (ADR 0035 #3).
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_NhanChuSoHuu
    @IDBenhNhan    bigint,
    @IDTaiKhoan    bigint,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    /* Khong con cot DM_BenhNhan.IDTaiKhoan de ghi. Tu 1B, "ho so thuoc ve ai"
       duoc tra loi bang (SDT, IDCoSo) cua chinh dong DM_BenhNhan - xem ADR 0034
       va muc "Loi vao" trong CONTEXT.md. */
    SET @ResultCode = 1;
    SET @ResultMessage = N'OK (không còn quyền sở hữu hồ sơ từ đợt 1B — xem ADR 0034).';
END;
GO

/* ===========================================================================
   #4  CUA 4 - dbo.DM_BenhNhanCoSo_Save
   ---------------------------------------------------------------------------
   🔴 GIU NGUYEN TEN du bang DM_BenhNhanCoSo da bien mat (ADR 0035 #4).
   🔴 GIU @IDBenhNhanCoSo OUTPUT.

   G2 - ba nhanh, khong bao gio tra tay khong:
     (1) da co dong cua NGUOI NAY tai @IDCoSo  -> TAI DUNG
     (2) chua co, src.IDCoSo IS NULL           -> THANG CAP chinh src
     (3) chua co, src.IDCoSo la co so KHAC     -> NHAN BAN dong moi
   Roi moi ap HANG RAO CHOT 47 len dong dich.

   G2b - nhanh (1) soi guong cua 1 theo ADR 0018: CCCD that thi tra CCCD;
   CCCD gia thi tra nhan than (ten khong dau + ngay sinh + gioi tinh). Thieu
   ve nay thi hai nguoi khac nhau cung mang CCCD gia se bi gom lam mot.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhanCoSo_Save
    @IDBenhNhan      bigint,
    @IDCoSo          bigint,
    @MaBN            varchar(20),
    @DaMoTaiLieu     bit            = 1,
    @IDBenhNhanCoSo  bigint         OUTPUT,
    @ResultCode      int            OUTPUT,
    @ResultMessage   nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDBenhNhanCoSo = NULL;

    DECLARE @MaDangCo varchar(20);
    DECLARE @srcCoSo  bigint, @srcCccd varchar(20), @srcTen nvarchar(200),
            @srcSdt   varchar(20),  @srcEmail varchar(100), @srcDiaChi nvarchar(255),
            @srcNgaySinh datetime,  @srcKhongDau nvarchar(200), @srcGioiTinh varchar(10);
    DECLARE @laCccdGia bit;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @srcCoSo = IDCoSo, @srcCccd = CCCD, @srcTen = TenBN, @srcSdt = SDT,
               @srcEmail = Email, @srcDiaChi = DiaChi, @srcNgaySinh = NgaySinh,
               @srcKhongDau = HoTenKhongDau, @srcGioiTinh = GioiTinh
          FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
         WHERE ID = @IDBenhNhan;

        IF @srcCccd IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Bệnh nhân không tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @IDCoSo)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Cơ sở không tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        SET @laCccdGia = CASE WHEN @srcCccd IN ('11111111111', '111111111111') THEN 1 ELSE 0 END;

        /* --- (1) TAI DUNG: da co dong cua nguoi nay tai @IDCoSo chua? ----- */
        IF @laCccdGia = 1
            SELECT TOP 1 @IDBenhNhanCoSo = ID
              FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
             WHERE IDCoSo = @IDCoSo
               AND HoTenKhongDau = @srcKhongDau
               AND CAST(NgaySinh AS date) = CAST(@srcNgaySinh AS date)
               AND GioiTinh = @srcGioiTinh;
        ELSE
            SELECT TOP 1 @IDBenhNhanCoSo = ID
              FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
             WHERE IDCoSo = @IDCoSo AND CCCD = @srcCccd;

        IF @IDBenhNhanCoSo IS NULL
        BEGIN
            IF @srcCoSo IS NULL
            BEGIN
                /* --- (2) THANG CAP chinh dong neo ------------------------- */
                UPDATE dbo.DM_BenhNhan
                   SET IDCoSo      = @IDCoSo,
                       MaBN        = @MaBN,
                       DaMoTaiLieu = @DaMoTaiLieu
                 WHERE ID = @IDBenhNhan;

                SET @IDBenhNhanCoSo = @IDBenhNhan;

                COMMIT TRANSACTION;
                SET @ResultCode = 1;
                SET @ResultMessage = N'OK';
                RETURN;
            END
            ELSE
            BEGIN
                /* --- (3) NHAN BAN: src thuoc co so KHAC ------------------- */
                INSERT INTO dbo.DM_BenhNhan
                    (IDCoSo, MaBN, CCCD, TenBN, SDT, Email, DiaChi, NgaySinh,
                     HoTenKhongDau, GioiTinh, DaMoTaiLieu)
                VALUES
                    (@IDCoSo, @MaBN, @srcCccd, @srcTen, @srcSdt, @srcEmail, @srcDiaChi,
                     @srcNgaySinh, @srcKhongDau, @srcGioiTinh, @DaMoTaiLieu);

                SET @IDBenhNhanCoSo = SCOPE_IDENTITY();

                COMMIT TRANSACTION;
                SET @ResultCode = 1;
                SET @ResultMessage = N'OK';
                RETURN;
            END;
        END;

        /* ------------------- HANG RAO CHOT 47 ----------------------------
           Ba ca, chi MOT ca bi chan:
             (a) @MaDangCo NULL  -> ho so TRANG MA, dien binh thuong.
                 🔴 Khong duoc chan ca nay: cua 4 cua S00_UploadOnline goi vao
                 day dung o ca "ho so co san nhung trang ma".
             (b) @MaDangCo = @MaBN -> luu lai cung ma, HOP LE, tra 1.
             (c) @MaDangCo khac rong VA khac @MaBN -> TU CHOI MEM.
                 Khong THROW: 4/5 noi goi khong bat exception. Va nem thi vuot
                 linked server -> HIS bao KHONG_TOI_DUOC_CONG, sai nguyen nhan.
           ------------------------------------------------------------------ */
        SELECT @MaDangCo = NULLIF(LTRIM(RTRIM(ISNULL(MaBN, ''))), '')
          FROM dbo.DM_BenhNhan WHERE ID = @IDBenhNhanCoSo;

        IF @MaDangCo IS NOT NULL AND @MaDangCo <> LTRIM(RTRIM(ISNULL(@MaBN, '')))
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ResultCode = 3;
            SET @ResultMessage = N'Hồ sơ này đang nối mã ' + @MaDangCo
                               + N' — muốn đổi sang ' + LTRIM(RTRIM(ISNULL(@MaBN, N'(trống)')))
                               + N' thì phải đi Cửa đổi mã.';
            RETURN;
        END;

        /* Nhanh nay CO Y khong dung toi DaMoTaiLieu: mot cua da bi dong CO Y
           thi khong duoc lang le mo lai chi vi ai do luu lai ho so. */
        UPDATE dbo.DM_BenhNhan SET MaBN = @MaBN WHERE ID = @IDBenhNhanCoSo;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Mã bệnh nhân này đã được cơ sở cấp cho người khác.';
            RETURN;
        END;
        IF ERROR_NUMBER() = 547
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Bệnh nhân hoặc cơ sở không tồn tại.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ===========================================================================
   #5  dbo.QL_DotKham_Save
   ---------------------------------------------------------------------------
   🔴 GIU TEN THAM SO @IDBenhNhanCoSo (hop dong) du COT trong bang da doi thanh
   IDBenhNhan. Day la cho BAT DOI XUNG CO Y - dung "don cho gon".
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.QL_DotKham_Save
    @IDCoSo         bigint,
    @IDBenhNhanCoSo bigint,
    @MaVaoVien      varchar(50),
    @MaBN           varchar(20)   = NULL,  -- nhan roi bo
    @NgayGioVao     datetime,
    @NgayGioRa      datetime      = NULL,  -- nhan roi bo
    @TenKhoa        nvarchar(255) = NULL,
    @TenBacSi       nvarchar(255) = NULL,
    @ChanDoan       nvarchar(MAX) = NULL,  -- nhan roi bo
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

        /* Giu phep CHAT: doi dong phai THUOC dung co so nay. Dong neo
           (IDCoSo NULL) truot phep nay - va do la DUNG (ADR 0021). */
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE ID = @IDBenhNhanCoSo AND IDCoSo = @IDCoSo)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Hồ sơ bệnh nhân không thuộc cơ sở này.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        /* 🔴 BAY T-SQL da lam MAT DU LIEU IM LANG: `SELECT @bien = cot` tren tap
           RONG thi bien GIU NGUYEN gia tri cu, KHONG thanh NULL. */
        DECLARE @IDCu bigint;

        SELECT @IDCu = ID
          FROM dbo.QL_DotKham WITH (UPDLOCK, HOLDLOCK)
         WHERE IDCoSo = @IDCoSo AND MaVaoVien = @MaVaoVien;

        IF @IDCu IS NULL
        BEGIN
            INSERT INTO dbo.QL_DotKham
                (IDCoSo, IDBenhNhan, MaVaoVien, NgayGioVao, TenKhoa, TenBacSi, NgayTao)
            VALUES
                (@IDCoSo, @IDBenhNhanCoSo, @MaVaoVien, @NgayGioVao, @TenKhoa, @TenBacSi, GETDATE());

            SET @IDDotKham = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE dbo.QL_DotKham
               SET IDBenhNhan = @IDBenhNhanCoSo,
                   NgayGioVao = @NgayGioVao,
                   TenKhoa    = @TenKhoa,
                   TenBacSi   = @TenBacSi,
                   NgayCapNhat = GETDATE()
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

/* ===========================================================================
   #6  dbo.QL_TaiLieuBenhNhan_Save
   ---------------------------------------------------------------------------
   🔴 GIU TEN THAM SO @IDBenhNhanCoSo (hop dong) du COT da doi thanh IDBenhNhan.
   🔴 Doi tuong DUY NHAT ma HIS doc duoc ResultCode - loi goi cua no co
      "SELECT @rc, @rm;" nen HIS hung duoc bang INSERT ... EXEC. Sua ma doi y
      nghia ma loi la HIS hieu sai (rc=5 -> NUA_DUONG o :177).

   Chot 3 (rc=5) GIU PHEP CHAT: doi dong phai thuoc DUNG co so nay. Dong neo
   (IDCoSo NULL) truot phep nay va do la DUNG - ADR 0021 tu choi tai lieu mo coi.
   Thuc te cua 4 luon chay truoc cua 5 nen nhanh nay khong cham toi.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.QL_TaiLieuBenhNhan_Save
    @ID             BIGINT,
    @IDCoSo         BIGINT,
    @IDBenhNhanCoSo BIGINT        = NULL,
    @MaBN           NVARCHAR(50)  = NULL,  -- nhan roi bo
    @LoaiTaiLieu    NVARCHAR(50),
    @TenTaiLieu     NVARCHAR(255),
    @DuongDanFtp    NVARCHAR(2000),
    @DungLuongByte  BIGINT        = NULL,  -- nhan roi bo
    @NgayKham       DATETIME      = NULL,
    @GhiChu         NVARCHAR(MAX) = NULL,  -- nhan roi bo
    @MaNguonHIS     VARCHAR(50)   = NULL,
    @BamNoiDung     CHAR(64)      = NULL,
    @NguonKho       NVARCHAR(20)  = N'CONG',
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
           OR NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
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
                SELECT @PhienBan = ISNULL(MAX(PhienBan), 0) + 1
                FROM dbo.QL_TaiLieuBenhNhan WITH (UPDLOCK, HOLDLOCK)
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu AND MaNguonHIS = @MaNguonHIS
                  AND LaBanMoiNhat = 1;   -- xem ADR 0034 cua SixosPwa/Database/29_ truoc khi go

                UPDATE dbo.QL_TaiLieuBenhNhan
                SET LaBanMoiNhat = 0
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu
                  AND MaNguonHIS = @MaNguonHIS AND LaBanMoiNhat = 1;
            END;

            INSERT INTO dbo.QL_TaiLieuBenhNhan (
                IDCoSo, IDBenhNhan, LoaiTaiLieu, TenTaiLieu, DuongDanFtp,
                NgayKham, MaNguonHIS, BamNoiDung, NguonKho,
                PhienBan, LaBanMoiNhat, NgayTao)
            VALUES (
                @IDCoSo, @IDBenhNhanCoSo, @LoaiTaiLieu, @TenTaiLieu, @DuongDanFtp,
                @NgayKham, @MaNguonHIS, @BamNoiDung,
                ISNULL(NULLIF(LTRIM(RTRIM(@NguonKho)), N''), N'CONG'),
                @PhienBan, 1, GETDATE());

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
            SET IDCoSo = @IDCoSo, IDBenhNhan = @IDBenhNhanCoSo,
                LoaiTaiLieu = @LoaiTaiLieu, TenTaiLieu = @TenTaiLieu,
                DuongDanFtp = @DuongDanFtp, NgayKham = @NgayKham
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

/* ===========================================================================
   #7  dbo.S00_SPWA_DoHienTrang        (hop dong COT TRA VE)
   ---------------------------------------------------------------------------
   🔴 BAY COT, DUNG TEN, DUNG THU TU. Them cot thi them O CUOI.
      IDCoSo, IDBenhNhan, IDBenhNhanCoSo, MaBNDangNoi,
      IDTaiKhoanTheoSdt, IDTaiLieuDaCo, DuongDanDaCo

   Doi so voi ban truoc 1B:
     - IDBenhNhan      : truoc tra CCCD TOAN HE; gio tra dong TAI CO SO NAY,
                         chua co thi tra DONG NEO. Tra dong cua co so khac la
                         HIS chet o MA_DA_CO_CHU.
     - IDBenhNhanCoSo  : cung so voi IDBenhNhan khi dong DA gan co so;
                         NULL khi con la dong neo (de HIS biet phai goi cua 4).
     - IDTaiKhoanTheoSdt: G1 - SOI GUONG IDBenhNhan (ADR 0034).
                         KHONG tra lai theo SDT: do that chi 114/10.987 ho so
                         Thien Nam co SDT khop voi HIS -> tra theo SDT la 99%
                         chet o NUA_DUONG (S00_UploadOnline:373).

   Stored nay CHI DOC. Moi tham so vo huong (TVP khong qua linked server duoc).
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.S00_SPWA_DoHienTrang
    @MaCoSo      nvarchar(50),
    @Cccd        varchar(20)   = NULL,
    @Sdt         varchar(20)   = NULL,
    @MaBN        varchar(20)   = NULL,
    @LoaiTaiLieu nvarchar(50)  = NULL,
    @MaNguonHIS  varchar(50)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @MaCoSo      = NULLIF(LTRIM(RTRIM(@MaCoSo)), N'');
    SET @Cccd        = NULLIF(LTRIM(RTRIM(@Cccd)), '');
    SET @Sdt         = NULLIF(LTRIM(RTRIM(@Sdt)), '');
    SET @MaBN        = NULLIF(LTRIM(RTRIM(@MaBN)), '');
    SET @LoaiTaiLieu = NULLIF(LTRIM(RTRIM(@LoaiTaiLieu)), N'');
    SET @MaNguonHIS  = NULLIF(LTRIM(RTRIM(@MaNguonHIS)), '');

    DECLARE @IDCoSo            bigint = NULL;
    DECLARE @IDBenhNhan        bigint = NULL;
    DECLARE @IDBenhNhanCoSo    bigint = NULL;
    DECLARE @MaBNDangNoi       varchar(20) = NULL;
    DECLARE @IDTaiKhoanTheoSdt bigint = NULL;
    DECLARE @IDTaiLieuDaCo     bigint = NULL;
    DECLARE @DuongDanDaCo      nvarchar(2000) = NULL;

    -- (1) Co so. Het buoc nay ma NULL thi moi thu con lai vo nghia.
    SELECT TOP 1 @IDCoSo = ID FROM dbo.DM_CSKCB WITH (NOLOCK) WHERE MaCoSo = @MaCoSo;

    IF @IDCoSo IS NOT NULL AND @Cccd IS NOT NULL
    BEGIN
        -- (2)+(3)+(4) Dong TAI CHINH co so nay: vua la ho so, vua la dong noi.
        SELECT TOP 1 @IDBenhNhan     = ID,
                     @IDBenhNhanCoSo = ID,
                     @MaBNDangNoi    = MaBN
          FROM dbo.DM_BenhNhan WITH (NOLOCK)
         WHERE IDCoSo = @IDCoSo AND CCCD = @Cccd;

        -- Chua gan co so -> tra DONG NEO cho cot 2, de cot 3 NULL.
        -- HIS thay cot 2 non-NULL nen bo qua cua 1; thay cot 3 NULL nen goi cua 4.
        IF @IDBenhNhan IS NULL
            SELECT TOP 1 @IDBenhNhan = ID
              FROM dbo.DM_BenhNhan WITH (NOLOCK)
             WHERE IDCoSo IS NULL AND CCCD = @Cccd;

        -- (5) G1: SOI GUONG. Xem ADR 0034.
        SET @IDTaiKhoanTheoSdt = @IDBenhNhan;
    END

    IF @IDCoSo IS NOT NULL AND @LoaiTaiLieu IS NOT NULL AND @MaNguonHIS IS NOT NULL
        -- (6)+(7) Ban MOI NHAT cua dung tai lieu nay, kem DUONG DAN dang tro.
        SELECT TOP 1 @IDTaiLieuDaCo = ID, @DuongDanDaCo = DuongDanFtp
          FROM dbo.QL_TaiLieuBenhNhan WITH (NOLOCK)
         WHERE IDCoSo = @IDCoSo
           AND LoaiTaiLieu = @LoaiTaiLieu
           AND MaNguonHIS = @MaNguonHIS
           AND LaBanMoiNhat = 1;

    -- LUON tra DUNG MOT DONG, ke ca khi khong tim thay gi.
    SELECT @IDCoSo            AS IDCoSo,
           @IDBenhNhan        AS IDBenhNhan,
           @IDBenhNhanCoSo    AS IDBenhNhanCoSo,
           @MaBNDangNoi       AS MaBNDangNoi,
           @IDTaiKhoanTheoSdt AS IDTaiKhoanTheoSdt,
           @IDTaiLieuDaCo     AS IDTaiLieuDaCo,
           @DuongDanDaCo      AS DuongDanDaCo;
END;
GO

/* --- tu kiem B10a ------------------------------------------------------- */
SELECT 'B10a hop dong' AS Buoc,
       (SELECT COUNT(*) FROM sys.objects
         WHERE type = 'P' AND name IN
           ('DM_BenhNhan_Save','HT_TaiKhoan_Save','DM_BenhNhan_NhanChuSoHuu',
            'DM_BenhNhanCoSo_Save','QL_DotKham_Save','QL_TaiLieuBenhNhan_Save',
            'S00_SPWA_DoHienTrang'))                       AS Du_7_doi_tuong,
       (SELECT COUNT(*) FROM sys.parameters
         WHERE object_id = OBJECT_ID('dbo.DM_BenhNhan_Save'))      AS Cua1_12_tham_so,
       (SELECT COUNT(*) FROM sys.parameters
         WHERE object_id = OBJECT_ID('dbo.HT_TaiKhoan_Save'))      AS Cua2_9_tham_so,
       (SELECT COUNT(*) FROM sys.parameters
         WHERE object_id = OBJECT_ID('dbo.DM_BenhNhanCoSo_Save')
           AND name = '@IDBenhNhanCoSo')                           AS Cua4_giu_ten_OUTPUT_1,
       (SELECT COUNT(*) FROM sys.parameters
         WHERE object_id = OBJECT_ID('dbo.QL_DotKham_Save')
           AND name = '@IDBenhNhanCoSo')                           AS Cua5_giu_ten_tham_so_1;
GO
