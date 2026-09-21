/* =============================================================================
   DOT 1B - buoc B10b: cac stored CHI CONG GOI (duoc phep doi chu ky)
   ---------------------------------------------------------------------------
   Chay SAU B10a. Khac B10a o cho: khong cai nao nam trong hop dong linked
   server, nen doi ten / doi tham so thoai mai - mien la sua C# theo.

   HAI DOI THAY XUYEN SUOT FILE NAY:

   (1) Doi tuong so huu ho so.
       Truoc: @IDTaiKhoan + DM_BenhNhan.IDTaiKhoan.
       Sau  : @SDT + @IDCoSo cua PHIEN. "Ho so cua toi" = dong DM_BenhNhan co
              dung SDT do tai dung co so do (luat C7b, xem CONTEXT.md muc
              "Loi vao"). Cot IDTaiKhoan da bi bo o B06.

   (2) Doi ten 4 stored (plan §4.3, da kiem: chi C# cua cong goi):
       DM_BenhNhanCoSo_DoiMa          -> DM_BenhNhan_DoiMa
       DM_BenhNhanCoSo_DoiMocXemLich  -> DM_BenhNhan_DoiMocXemLich
       DM_BenhNhanCoSo_GoNoi          -> DM_BenhNhan_GoNoi
       DM_BenhNhanCoSo_TaoTuKhai      -> DM_BenhNhan_TaoTuKhai
       🔴 DM_BenhNhanCoSo_Save KHONG doi ten - no o trong hop dong (B10a #4).
   ========================================================================== */
SET NOCOUNT ON;
GO
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NULL
    THROW 50110, 'Chua chay B02. DUNG LAI.', 1;
IF OBJECT_ID('dbo.S00_SPWA_DoHienTrang', 'P') IS NULL
    THROW 50111, 'Chua chay B10a. DUNG LAI.', 1;
GO

/* --- kho luu cho Go noi: bang cu duoc nan tu DM_BenhNhanCoSo (7 cot) nen
       khong con nhan duoc SELECT * cua DM_BenhNhan (14 cot). Dung bang moi. -- */
IF SCHEMA_ID('bak3') IS NULL EXEC('CREATE SCHEMA bak3');
GO
/* 🔴 SELECT * INTO CHEP CA THUOC TINH IDENTITY sang bang dich => moi cau
   INSERT ... SELECT * vao no deu no "An explicit value for the identity column
   ... can only be specified when a column list is used". Da dap that 19-09.
   Meo UNION ALL triet IDENTITY ma khong phai liet ke 14 cot (liet ke tay la
   them mot cho phai sua moi lan bang doi). */
IF OBJECT_ID('bak3.GoNoi_HoSo_V002') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('bak3.GoNoi_HoSo_V002') AND is_identity = 1)
    DROP TABLE bak3.GoNoi_HoSo_V002;
GO
IF OBJECT_ID('bak3.GoNoi_HoSo_V002') IS NULL
    SELECT TOP 0 x.*, CAST(NULL AS datetime) AS NgayGo
      INTO bak3.GoNoi_HoSo_V002
      FROM (SELECT * FROM dbo.DM_BenhNhan
            UNION ALL
            SELECT * FROM dbo.DM_BenhNhan) x;
GO

/* ===========================================================================
   1. dbo.HT_LogApiCoSo_Ghi  (+ khoi DON DINH KY - C17 va G3)
   ---------------------------------------------------------------------------
   🔴 Don theo ID, KHONG theo NgayTao: bang chi co PK clustered tren ID, khong
   chi muc nao tren NgayTao, va luat du an CAM them index => DELETE ... WHERE
   NgayTao < ... la quet toan bang MOI LUOT GHI. ID tang don dieu theo thoi
   gian nen di dung clustered index.

   🔴 Khoi don boc TRY...CATCH RIENG va NUOT loi: don hong thi thoi, tuyet doi
   khong duoc lam hong luot GHI log.

   Tiet che: moc HT_Config.'LOG_DON_LAN_CUOI'.Ngay, moi ngay lich mot lan
   (C17b). Tat han bang cach dat HieuLuc = 0.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.HT_LogApiCoSo_Ghi
    @IDCoSo     bigint       = NULL,
    @Endpoint   varchar(100),
    @MaBN       varchar(20)  = NULL,
    @MaNguonHIS varchar(50)  = NULL,
    @KetQua     varchar(20),
    @LyDo       varchar(50)  = NULL,
    @SoLuong    int          = NULL,
    @IpGoi      varchar(45)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /* --- viec chinh: GHI. Phai xong truoc, khong dinh dang gi toi viec don. */
    INSERT INTO dbo.HT_LogApiCoSo
        (IDCoSo, Endpoint, MaBN, MaNguonHIS, KetQua, LyDo, SoLuong, IpGoi, NgayTao)
    VALUES
        (@IDCoSo, @Endpoint, @MaBN, @MaNguonHIS, @KetQua, @LyDo, @SoLuong, @IpGoi, GETDATE());

    /* --- viec phu: DON. Loi o day khong duoc noi ra ngoai. --------------- */
    BEGIN TRY
        DECLARE @bat bit, @moc date;

        SELECT @bat = HieuLuc, @moc = Ngay
          FROM dbo.HT_Config WHERE MaChucNang = 'LOG_DON_LAN_CUOI';

        IF @bat = 1 AND (@moc IS NULL OR @moc < CAST(GETDATE() AS date))
        BEGIN
            /* (a) log qua 3 thang - di theo ID */
            DECLARE @nguong bigint;
            SELECT @nguong = MAX(ID) FROM dbo.HT_LogApiCoSo
             WHERE NgayTao < DATEADD(month, -3, GETDATE());

            IF @nguong IS NOT NULL
                DELETE FROM dbo.HT_LogApiCoSo WHERE ID < @nguong;

            /* (b) G3 - dong neo mo coi qua 30 ngay.
               Luong chet giua cua 1 va cua 4 cach nhau vai giay; nguong 30 ngay
               la hon 200.000 lan khoang cach do nen khong the xoa nham dong
               dang dung, va con cho co hoi TAI DUNG neu BN quay lai trong thang. */
            DELETE FROM dbo.DM_BenhNhan
             WHERE IDCoSo IS NULL
               AND NgayTao < DATEADD(day, -30, GETDATE());

            /* (c) dong moc lai */
            UPDATE dbo.HT_Config SET Ngay = CAST(GETDATE() AS date)
             WHERE MaChucNang = 'LOG_DON_LAN_CUOI';
        END
    END TRY
    BEGIN CATCH
        /* NUOT - co y. Xem khoi chu thich dau stored. */
    END CATCH;
END;
GO

/* ===========================================================================
   2. dbo.DM_BenhNhan_Loc
   ---------------------------------------------------------------------------
   Bo @IDDoiTac -> @TenCongTy (C16). Bo join DM_BenhNhanCoSo: mot bang la du.
   🔴 Ban cu con tham chieu c.IDDoiTac - mot cot KHONG TON TAI (bang that co
   IDCongTy). Stored chay duoc la nho phan giai ten tre; no se no luc chay that.
   Sua luon o day.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_Loc
    @TuKhoa    nvarchar(100) = NULL,
    @IDCoSo    bigint        = NULL,
    @TenCongTy nvarchar(200) = NULL,
    @Trang     int           = 1,
    @CoTrang   int           = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF @Trang   IS NULL OR @Trang   < 1 SET @Trang = 1;
    IF @CoTrang IS NULL OR @CoTrang < 1 SET @CoTrang = 20;

    DECLARE @tk nvarchar(102) = N'%' + LTRIM(RTRIM(ISNULL(@TuKhoa, N''))) + N'%';

    ;WITH loc AS (
        SELECT p.ID AS IDBenhNhan, p.CCCD, p.TenBN, p.SDT, p.Email, p.DiaChi,
               p.ID AS IDBenhNhanCoSo,      -- giu ten cot cho C#/view cu
               p.MaBN, c.ID AS IDCoSo, c.MaCoSo, c.TenCoSo, c.TenCongTy
        FROM dbo.DM_BenhNhan p
        JOIN dbo.DM_CSKCB    c ON c.ID = p.IDCoSo   -- INNER => tu loc dong neo
        WHERE (@IDCoSo IS NULL OR p.IDCoSo = @IDCoSo)
          AND (NULLIF(LTRIM(RTRIM(@TenCongTy)), N'') IS NULL OR c.TenCongTy = @TenCongTy)
          AND (NULLIF(LTRIM(RTRIM(@TuKhoa)), N'') IS NULL
               OR p.TenBN LIKE @tk OR p.CCCD LIKE @tk OR p.SDT LIKE @tk OR p.MaBN LIKE @tk)
    )
    SELECT *, (SELECT COUNT(*) FROM loc) AS TongSoDong
    FROM loc
    ORDER BY TenBN
    OFFSET (@Trang - 1) * @CoTrang ROWS FETCH NEXT @CoTrang ROWS ONLY;
END;
GO

/* ===========================================================================
   3. dbo.DM_BenhNhan_SuaHoSo
   ---------------------------------------------------------------------------
   @IDTaiKhoan -> @SDT + @IDCoSo (chu so huu tu 1B la cap SDT x co so).
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_SuaHoSo
    @IDBenhNhan    bigint,
    @SDT           varchar(20),          -- SDT cua PHIEN
    @IDCoSo        bigint,               -- co so cua PHIEN
    @CCCD          varchar(20),
    @TenBN         nvarchar(100),
    @SDTMoi        varchar(20)   = NULL,
    @NgaySinh      datetime      = NULL,
    @HoTenKhongDau varchar(100)  = NULL,
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

        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE ID = @IDBenhNhan AND SDT = @SDT AND IDCoSo = @IDCoSo)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Hồ sơ này không thuộc về bạn tại cơ sở đang đăng nhập.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        UPDATE dbo.DM_BenhNhan
           SET CCCD          = ISNULL(NULLIF(LTRIM(RTRIM(@CCCD)), ''), CCCD),
               TenBN         = ISNULL(NULLIF(LTRIM(RTRIM(@TenBN)), N''), TenBN),
               SDT           = ISNULL(@SDTMoi, SDT),
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
            SET @ResultMessage = N'Số căn cước này đã có hồ sơ khác tại cơ sở.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ===========================================================================
   4. dbo.DM_BenhNhan_XoaHoSo
   ---------------------------------------------------------------------------
   Gio mot dong la mot ho so tai mot co so => xoa dung mot dong, khong con
   phai don bang con.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_XoaHoSo
    @IDBenhNhan    bigint,
    @SDT           varchar(20),
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
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE ID = @IDBenhNhan AND SDT = @SDT AND IDCoSo = @IDCoSo)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Hồ sơ này không thuộc về bạn tại cơ sở đang đăng nhập.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF EXISTS (SELECT 1 FROM dbo.DM_BenhNhan WHERE ID = @IDBenhNhan AND MaBN IS NOT NULL)
        BEGIN
            SET @ResultCode = 3;
            SET @ResultMessage = N'Hồ sơ này đã nối với bệnh án tại cơ sở nên không xoá được. Hãy bỏ nối trước.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF EXISTS (SELECT 1 FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhan = @IDBenhNhan)
           OR EXISTS (SELECT 1 FROM dbo.QL_DotKham      WHERE IDBenhNhan = @IDBenhNhan)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Hồ sơ này đã có dữ liệu khám nên không xoá được.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DELETE FROM dbo.HT_ThongBao   WHERE IDNguoiNhan = @IDBenhNhan;
        DELETE FROM dbo.HT_PushDangKy WHERE IDBenhNhan  = @IDBenhNhan;
        DELETE FROM dbo.DM_BenhNhan   WHERE ID          = @IDBenhNhan;

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
   5. dbo.DM_BenhNhan_TaoTuKhai   (doi ten tu DM_BenhNhanCoSo_TaoTuKhai)
   ---------------------------------------------------------------------------
   Dung mot ho so TU KHAI tai co so: chinh la THANG CAP dong neo, hoac NHAN BAN
   neu nguoi do da co dong o co so khac. Cung ba nhanh voi cua 4, chi khac la
   khong nhan @MaBN (ho so tu khai chua co ma nao).
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_TaoTuKhai
    @IDBenhNhan      bigint,
    @IDCoSo          bigint,
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

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @srcCoSo bigint, @srcCccd varchar(20);

        SELECT @srcCoSo = IDCoSo, @srcCccd = CCCD
          FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
         WHERE ID = @IDBenhNhan;

        IF @srcCccd IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Hồ sơ không tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        SELECT TOP 1 @IDBenhNhanCoSo = ID
          FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
         WHERE IDCoSo = @IDCoSo AND CCCD = @srcCccd;

        IF @IDBenhNhanCoSo IS NULL
        BEGIN
            IF @srcCoSo IS NULL
            BEGIN
                UPDATE dbo.DM_BenhNhan
                   SET IDCoSo = @IDCoSo, DaMoTaiLieu = 1
                 WHERE ID = @IDBenhNhan;
                SET @IDBenhNhanCoSo = @IDBenhNhan;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.DM_BenhNhan
                    (IDCoSo, MaBN, CCCD, TenBN, SDT, Email, DiaChi, NgaySinh,
                     HoTenKhongDau, GioiTinh, DaMoTaiLieu)
                SELECT @IDCoSo, NULL, CCCD, TenBN, SDT, Email, DiaChi, NgaySinh,
                       HoTenKhongDau, GioiTinh, 1
                  FROM dbo.DM_BenhNhan WHERE ID = @IDBenhNhan;
                SET @IDBenhNhanCoSo = SCOPE_IDENTITY();
            END;
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
   6. dbo.DM_BenhNhan_DoiMocXemLich   (doi ten tu DM_BenhNhanCoSo_DoiMocXemLich)
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_DoiMocXemLich
    @IDBenhNhan    bigint,
    @IDCoSo        bigint,
    @SDT           varchar(20),
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                    WHERE ID = @IDBenhNhan AND IDCoSo = @IDCoSo AND SDT = @SDT)
    BEGIN
        SET @ResultCode = 2;
        SET @ResultMessage = N'Hồ sơ này không thuộc về bạn tại cơ sở đang đăng nhập.';
        RETURN;
    END;

    UPDATE dbo.DM_BenhNhan SET NgayXemLichCuoi = GETDATE() WHERE ID = @IDBenhNhan;

    SET @ResultCode = 1;
    SET @ResultMessage = N'OK';
END;
GO

/* ===========================================================================
   7. dbo.DM_BenhNhan_DoiMa   (doi ten tu DM_BenhNhanCoSo_DoiMa)
   ---------------------------------------------------------------------------
   Cua DOI MA rieng - ADR 0032. Day la cua DUY NHAT duoc doi mot ho so tu ma
   nay sang ma khac; cua 4 chi duoc DIEN ma vao cho trong.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_DoiMa
    @IDBenhNhan      bigint,
    @IDCoSo          bigint,
    @MaBN            varchar(20),
    @LyDo            nvarchar(200)  = NULL,
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
    DECLARE @MaMoi    varchar(20) = NULLIF(LTRIM(RTRIM(ISNULL(@MaBN, ''))), '');

    BEGIN TRY
        IF @MaMoi IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Thiếu mã bệnh nhân mới.';
            RETURN;
        END;

        BEGIN TRANSACTION;

        SELECT @IDBenhNhanCoSo = ID,
               @MaDangCo = NULLIF(LTRIM(RTRIM(ISNULL(MaBN, ''))), '')
          FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
         WHERE ID = @IDBenhNhan AND IDCoSo = @IDCoSo;

        IF @IDBenhNhanCoSo IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Không tìm thấy hồ sơ tại cơ sở này.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @MaDangCo IS NOT NULL AND @MaDangCo = @MaMoi
        BEGIN
            COMMIT TRANSACTION;
            SET @ResultCode = 1;
            SET @ResultMessage = N'Hồ sơ đã đang nối đúng mã này.';
            RETURN;
        END;

        UPDATE dbo.DM_BenhNhan SET MaBN = @MaMoi WHERE ID = @IDBenhNhanCoSo;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';

        /* Ghi nhat ky NGOAI transaction: doi ma la viec dang truy nguyen. */
        BEGIN TRY
            EXEC dbo.HT_LogApiCoSo_Ghi
                 @IDCoSo   = @IDCoSo,
                 @Endpoint = 'DM_BenhNhan_DoiMa',
                 @MaBN     = @MaMoi,
                 @KetQua   = 'DOI_MA',
                 @LyDo     = 'DOI_MA_THU_CONG';
        END TRY
        BEGIN CATCH
        END CATCH;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Mã bệnh nhân này đã được cơ sở cấp cho người khác.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ===========================================================================
   8. dbo.DM_BenhNhan_GoNoi   (doi ten tu DM_BenhNhanCoSo_GoNoi)
   ---------------------------------------------------------------------------
   Go noi = nha ma + xoa tai lieu/dot kham cua co so do, nhung KHONG lam ho so
   bien mat: no "tut ve" ho so tu khai. Gio mot dong la mot ho so, nen thay vi
   DELETE roi INSERT lai, chi can XOA MA - dong van con, ID khong doi.
   Do la cai LOI hon ban cu: claim HoSoDangChon trong cookie khong bi mo coi.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhan_GoNoi
    @IDBenhNhan    bigint,
    @SDT           varchar(20),
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
        BEGIN TRANSACTION;

        DECLARE @maBN varchar(20);

        SELECT @maBN = NULLIF(LTRIM(RTRIM(ISNULL(MaBN, ''))), '')
          FROM dbo.DM_BenhNhan WITH (UPDLOCK, HOLDLOCK)
         WHERE ID = @IDBenhNhan AND IDCoSo = @IDCoSo AND SDT = @SDT;

        IF @@ROWCOUNT = 0
        BEGIN
            SET @ResultCode = 5;
            SET @ResultMessage = N'Hồ sơ này không thuộc về bạn tại cơ sở đang đăng nhập.';
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

        INSERT INTO bak3.GoNoi_HoSo_V002
        SELECT *, @bayGio FROM dbo.DM_BenhNhan WHERE ID = @IDBenhNhan;

        INSERT INTO bak.GoNoi_TaiLieu_V001
        SELECT *, @bayGio FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhan = @IDBenhNhan;

        INSERT INTO bak.GoNoi_DotKham_V001
        SELECT *, @bayGio FROM dbo.QL_DotKham WHERE IDBenhNhan = @IDBenhNhan;

        DELETE FROM dbo.QL_TaiLieuBenhNhan WHERE IDBenhNhan = @IDBenhNhan;
        DELETE FROM dbo.QL_DotKham         WHERE IDBenhNhan = @IDBenhNhan;

        /* Ho so O LAI, chi nha ma ra. Khong DELETE + INSERT nhu ban cu. */
        UPDATE dbo.DM_BenhNhan
           SET MaBN = NULL, DaMoTaiLieu = 1
         WHERE ID = @IDBenhNhan;

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

/* --- bo 4 stored ten cu (than da chuyen sang ten moi) ------------------- */
IF OBJECT_ID('dbo.DM_BenhNhanCoSo_DoiMa', 'P')         IS NOT NULL DROP PROCEDURE dbo.DM_BenhNhanCoSo_DoiMa;
IF OBJECT_ID('dbo.DM_BenhNhanCoSo_DoiMocXemLich', 'P') IS NOT NULL DROP PROCEDURE dbo.DM_BenhNhanCoSo_DoiMocXemLich;
IF OBJECT_ID('dbo.DM_BenhNhanCoSo_GoNoi', 'P')         IS NOT NULL DROP PROCEDURE dbo.DM_BenhNhanCoSo_GoNoi;
IF OBJECT_ID('dbo.DM_BenhNhanCoSo_TaoTuKhai', 'P')     IS NOT NULL DROP PROCEDURE dbo.DM_BenhNhanCoSo_TaoTuKhai;
GO

/* ===========================================================================
   9. dbo.HT_TaiKhoan_Loc   - chi con Admin
   ---------------------------------------------------------------------------
   Moi nhanh loc theo benh nhan (CCCD / MaBN / LoaiCS) chet theo: bang nay
   khong con dinh gi toi benh nhan. Giu @Role de C# cu khong vo, nhung no chi
   con mot gia tri hop le.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.HT_TaiKhoan_Loc
    @Trang    int           = 1,
    @SoDong   int           = 50,
    @SDT      varchar(20)   = NULL,
    @CCCD     varchar(20)   = NULL,   -- nhan roi bo
    @MaBN     varchar(50)   = NULL,   -- nhan roi bo
    @Role     varchar(20)   = NULL,   -- nhan roi bo (chi con Admin)
    @LoaiCS   varchar(50)   = NULL    -- nhan roi bo
AS
BEGIN
    SET NOCOUNT ON;

    IF @Trang  IS NULL OR @Trang  < 1 SET @Trang = 1;
    IF @SoDong IS NULL OR @SoDong < 1 SET @SoDong = 50;

    ;WITH loc AS (
        SELECT tk.ID, tk.SDT, tk.Email, tk.Role, tk.NgayTao,
               CAST(CASE WHEN tk.MatKhauNoiBo IS NULL THEN 0 ELSE 1 END AS bit) AS CoMatKhau
        FROM dbo.HT_TaiKhoan tk WITH (NOLOCK)
        WHERE (NULLIF(LTRIM(RTRIM(@SDT)), '') IS NULL OR tk.SDT LIKE '%' + @SDT + '%')
    )
    SELECT *, (SELECT COUNT(*) FROM loc) AS TongSoDong
    FROM loc
    ORDER BY ID
    OFFSET (@Trang - 1) * @SoDong ROWS FETCH NEXT @SoDong ROWS ONLY;
END;
GO

/* ===========================================================================
   10-12. HT_ThongBao_* / HT_PushDangKy_Save  - C15 / PA-2a
   ---------------------------------------------------------------------------
   IDNguoiGui  -> HT_TaiKhoan(ID)  [Admin]
   IDNguoiNhan -> DM_BenhNhan(ID)  [THEO CO SO]
   Hai cot tro HAI BANG KHAC NHAU nen phep "gui cho chinh minh" bi bo: so hai
   ID cua hai bang khac nhau la vo nghia.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.HT_ThongBao_Save
    @IDNguoiGui    bigint,
    @IDNguoiNhan   bigint,
    @NoiDung       nvarchar(1000),
    @IDThongBao    bigint         OUTPUT,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDThongBao = NULL;

    BEGIN TRY
        IF NULLIF(LTRIM(RTRIM(@NoiDung)), N'') IS NULL
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Nội dung thông báo trống.';
            RETURN;
        END;

        /* Nguoi nhan phai la ho so DA GAN CO SO - dong neo khong nhan tin duoc. */
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhan
                        WHERE ID = @IDNguoiNhan AND IDCoSo IS NOT NULL)
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Hồ sơ người nhận không tồn tại tại cơ sở nào.';
            RETURN;
        END;

        INSERT INTO dbo.HT_ThongBao (IDNguoiGui, IDNguoiNhan, NoiDung, ThoiGian, DaDoc)
        VALUES (@IDNguoiGui, @IDNguoiNhan, @NoiDung, GETDATE(), 0);
        SET @IDThongBao = SCOPE_IDENTITY();

        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() = 547
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Người gửi hoặc người nhận không tồn tại.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.HT_ThongBao_DanhDauDaDoc
    @IDNguoiNhan bigint          -- DM_BenhNhan.ID (ho so tai co so)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.HT_ThongBao
       SET DaDoc = 1
     WHERE IDNguoiNhan = @IDNguoiNhan AND DaDoc = 0;

    SELECT @@ROWCOUNT AS SoDongDaDanhDau;
END;
GO

CREATE OR ALTER PROCEDURE dbo.HT_PushDangKy_Save
    @IDBenhNhan    bigint,          -- doi ten tu @IDTaiKhoan
    @Endpoint      nvarchar(1000),
    @P256dh        nvarchar(500),
    @Auth          nvarchar(200),
    @IDDangKy      bigint         OUTPUT,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;
    SET @IDDangKy = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @IDDangKy = ID
          FROM dbo.HT_PushDangKy WITH (UPDLOCK, HOLDLOCK)
         WHERE IDBenhNhan = @IDBenhNhan AND Endpoint = @Endpoint;

        IF @IDDangKy IS NULL
        BEGIN
            INSERT INTO dbo.HT_PushDangKy (IDBenhNhan, Endpoint, P256dh, Auth, ThoiGian)
            VALUES (@IDBenhNhan, @Endpoint, @P256dh, @Auth, GETDATE());
            SET @IDDangKy = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE dbo.HT_PushDangKy
               SET P256dh = @P256dh, Auth = @Auth, ThoiGian = GETDATE()
             WHERE ID = @IDDangKy;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF ERROR_NUMBER() = 547
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Hồ sơ bệnh nhân không tồn tại.';
            RETURN;
        END;
        SET @ResultCode = 99;
        SET @ResultMessage = ERROR_MESSAGE();
    END CATCH;
END;
GO

/* ===========================================================================
   13. dbo.QL_TaiLieuBenhNhan_TimTrungNoiDung  - doi ten cot tra ve
   ---------------------------------------------------------------------------
   🔴 BAN TREN DB DANG HONG SAN TU DOT A: no SELECT MaBN va DungLuongByte, ma
   dot A da XOA hai cot do khoi QL_TaiLieuBenhNhan (buoc A05 "bo 16 cot chet").
   Goi vao la nem "Invalid column name". Build xanh, khong ai phat hien, vi
   KHONG DONG C# NAO GOI stored nay (da grep ca cay nguon).
   Buoc 1B nay lam lo ra vi CREATE OR ALTER co kiem cot cua bang DA TON TAI
   (phan giai ten tre chi ap cho BANG thieu, khong ap cho COT thieu).
   Sua luon cho dung schema that.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.QL_TaiLieuBenhNhan_TimTrungNoiDung
    @IDCoSo      bigint,
    @LoaiTaiLieu nvarchar(50),
    @MaNguonHIS  nvarchar(100),
    @BamNoiDung  char(64)
AS
BEGIN
    SET NOCOUNT ON;

    IF @MaNguonHIS IS NULL OR LEN(LTRIM(RTRIM(@MaNguonHIS))) = 0
       OR @BamNoiDung IS NULL OR LEN(LTRIM(RTRIM(@BamNoiDung))) = 0
        RETURN;

    SELECT TOP 1
           ID, IDBenhNhan, LoaiTaiLieu, TenTaiLieu,
           DuongDanFtp, PhienBan, NgayTao
      FROM dbo.QL_TaiLieuBenhNhan WITH (NOLOCK)
     WHERE IDCoSo       = @IDCoSo
       AND LoaiTaiLieu  = @LoaiTaiLieu
       AND MaNguonHIS   = @MaNguonHIS
       AND BamNoiDung   = @BamNoiDung
       AND LaBanMoiNhat = 1
     ORDER BY ID DESC;
END;
GO

/* ===========================================================================
   14. dbo.DM_CSKCB_Delete  - bang DM_BenhNhanCoSo khong con
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_CSKCB_Delete
    @ID            bigint,
    @ResultCode    int            OUTPUT,
    @ResultMessage nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @ID)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Cơ sở không tồn tại.';
            RETURN;
        END;

        BEGIN TRANSACTION;

        /* Xoa theo dung thu tu phu thuoc. Ho so benh nhan cua co so nay bi xoa
           theo - nhung phai don thong bao / push neo vao chung TRUOC. */
        DELETE tb FROM dbo.HT_ThongBao tb
          JOIN dbo.DM_BenhNhan b ON b.ID = tb.IDNguoiNhan WHERE b.IDCoSo = @ID;
        DELETE p FROM dbo.HT_PushDangKy p
          JOIN dbo.DM_BenhNhan b ON b.ID = p.IDBenhNhan  WHERE b.IDCoSo = @ID;

        DELETE FROM dbo.QL_TaiLieuBenhNhan   WHERE IDCoSo = @ID;
        DELETE FROM dbo.QL_DotKham           WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_BenhNhan          WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_GioLamViec  WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_NoiDung     WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB_CapQuangCao WHERE IDCoSo = @ID;
        DELETE FROM dbo.DM_CSKCB             WHERE ID     = @ID;

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
   15. dbo.DM_CSKCB_Save  - doi khoa ngoai cong ty thanh cot ten phang (C16)
   ---------------------------------------------------------------------------
   Bang DM_DoiTac da bo o B08. Than stored giu NGUYEN VEN moi thu khac, ke ca
   luat "3 cot bi mat: NULL = GIU NGUYEN, khong phai xoa".
   =========================================================================== */


/* ===========================================================================
   2. DM_CSKCB_Save — mot cho luu CA 4 nhom (goc / quang cao / ket noi / FTP)
   ---------------------------------------------------------------------------
   🔴 LUAT GIU BI MAT: @KhoaBam / @Ftp_MatKhau / @KetNoi_KhoaGoiHIS truyen NULL
   thi GIU NGUYEN gia tri cu, KHONG ghi de NULL. Man Admin khong hien cac o do
   (Ftp_MatKhau luu THO - co y, vi FTP can dang nhap lai duoc) nen no khong gui
   lai gia tri; neu ghi de NULL thi moi lan Admin sua dia chi la mat ket noi.
   =========================================================================== */
CREATE OR ALTER PROCEDURE dbo.DM_CSKCB_Save
    @ID                    bigint,
    @MaCoSo                varchar(10),
    @TenCoSo               nvarchar(200),
    @Slug                  varchar(100),
    @IDNhomCS              bigint         = NULL,
    @DiaChi                nvarchar(255)  = NULL,
    @Tinh                  int            = NULL,
    @PhuongXa              int            = NULL,
    @SDT                   varchar(20)    = NULL,
    @Email                 varchar(100)   = NULL,
    @AnhBia                nvarchar(500)  = NULL,
    @Logo                  nvarchar(max)  = NULL,
    @HienThiCongKhai       bit            = 0,
    @QcSoTienDaTra         decimal(15,0)  = NULL,   -- so nguyen VND la CO Y (I-01)
    @QcNoiDung             nvarchar(max)  = NULL,
    @QcAnh                 nvarchar(max)  = NULL,
    @TenCongTy             nvarchar(200)  = NULL,   -- C16: thay khoa ngoai cong ty bang ten phang
    @KetNoi_UrlChuyenHuong nvarchar(255)  = NULL,
    @KetNoi_BaseUrlHIS     nvarchar(255)  = NULL,
    @KetNoi_KhoaGoiHIS     nvarchar(500)  = NULL,   -- NULL = giu nguyen
    @KetNoi_Active         bit            = 1,
    @KhoaBam               varbinary(32)  = NULL,   -- NULL = giu nguyen
    @Khoa_NgayHetHan       datetime       = NULL,
    @Ftp_Host              nvarchar(200)  = NULL,
    @Ftp_TaiKhoan          nvarchar(100)  = NULL,
    @Ftp_MatKhau           nvarchar(200)  = NULL,   -- NULL = giu nguyen
    @Ftp_ThuMucGoc         nvarchar(200)  = NULL,
    @Ftp_Active            bit            = 0,
    @ResultCode            int            OUTPUT,
    @ResultMessage         nvarchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @ID = 0
        BEGIN
            /* 🔴 Chot 41 (giu tu HT_KhoFtpCoSo_Save): kho MOI thi chua the da
               "Thu ket noi dat" => ep Ftp_Active = 0, Ftp_NgayThuDat = NULL. */
            INSERT INTO dbo.DM_CSKCB
                (MaCoSo, TenCoSo, Slug, IDNhomCS, DiaChi, Tinh, PhuongXa, SDT, Email,
                 AnhBia, Logo, HienThiCongKhai, QcSoTienDaTra, QcNoiDung, QcAnh, TenCongTy,
                 KetNoi_UrlChuyenHuong, KetNoi_BaseUrlHIS, KetNoi_KhoaGoiHIS, KetNoi_Active,
                 KhoaBam, Khoa_NgayCap, Khoa_NgayHetHan,
                 Ftp_Host, Ftp_TaiKhoan, Ftp_MatKhau, Ftp_ThuMucGoc, Ftp_Active, Ftp_NgayThuDat, NgayTao)
            VALUES
                (@MaCoSo, @TenCoSo, @Slug, @IDNhomCS, @DiaChi, @Tinh, @PhuongXa, @SDT, @Email,
                 @AnhBia, @Logo, @HienThiCongKhai, @QcSoTienDaTra, @QcNoiDung, @QcAnh, @TenCongTy,
                 @KetNoi_UrlChuyenHuong, @KetNoi_BaseUrlHIS, @KetNoi_KhoaGoiHIS, @KetNoi_Active,
                 @KhoaBam,
                 CASE WHEN @KhoaBam IS NULL THEN NULL ELSE GETDATE() END,
                 @Khoa_NgayHetHan,
                 @Ftp_Host, @Ftp_TaiKhoan, @Ftp_MatKhau, ISNULL(@Ftp_ThuMucGoc, N''), 0, NULL,
                 GETDATE());
            SET @ID = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE ID = @ID)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Cơ sở y tế không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            /* ================= 🔴 Chot 41 — giu nguyen luat cu cua kho FTP =====
               Truoc dot A luat nay nam o HT_KhoFtpCoSo_Save. Gop bang thi luat
               phai theo, khong duoc bay hoi: chua "Thu ket noi DAT" thi KHONG
               duoc bat Ftp_Active; va doi Host/TaiKhoan/MatKhau/ThuMucGoc la lan
               thu cu HET HIEU LUC, phai thu lai.
               Khac ban cu mot cho: @Ftp_MatKhau = NULL nghia la GIU NGUYEN (man
               Admin khong hien lai mat khau tho), nen NULL KHONG tinh la doi. */
            DECLARE @ThuDatCu  datetime = NULL;
            DECLARE @DoiKetNoi bit      = 0;

            SELECT @ThuDatCu   = Ftp_NgayThuDat,
                   @DoiKetNoi  = CASE
                       WHEN ISNULL(Ftp_Host,      N'') =  ISNULL(@Ftp_Host,     N'')
                        AND ISNULL(Ftp_ThuMucGoc, N'') =  ISNULL(@Ftp_ThuMucGoc, N'')
                        /* 2 o bi mat: NULL = khong gui lai = KHONG PHAI doi */
                        AND (@Ftp_TaiKhoan IS NULL OR ISNULL(Ftp_TaiKhoan, N'') = @Ftp_TaiKhoan)
                        AND (@Ftp_MatKhau  IS NULL OR ISNULL(Ftp_MatKhau,  N'') = @Ftp_MatKhau)
                       THEN 0 ELSE 1 END
            FROM dbo.DM_CSKCB WHERE ID = @ID;

            DECLARE @ThuDatMoi datetime = CASE WHEN @DoiKetNoi = 1 THEN NULL ELSE @ThuDatCu END;

            IF @Ftp_Active = 1 AND @ThuDatMoi IS NULL
            BEGIN
                SET @ResultCode = 6;
                SET @ResultMessage = N'Phải Thử kết nối kho đạt trước khi bật.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;
            /* ================================================================ */

            UPDATE dbo.DM_CSKCB
               SET MaCoSo = @MaCoSo, TenCoSo = @TenCoSo, Slug = @Slug, IDNhomCS = @IDNhomCS,
                   DiaChi = @DiaChi, Tinh = @Tinh, PhuongXa = @PhuongXa,
                   SDT = @SDT, Email = @Email, AnhBia = @AnhBia, Logo = @Logo,
                   HienThiCongKhai = @HienThiCongKhai, QcSoTienDaTra = @QcSoTienDaTra,
                   QcNoiDung = @QcNoiDung, QcAnh = @QcAnh, TenCongTy = @TenCongTy,
                   KetNoi_UrlChuyenHuong = @KetNoi_UrlChuyenHuong,
                   KetNoi_BaseUrlHIS     = @KetNoi_BaseUrlHIS,
                   /* 3 cot bi mat: NULL = GIU NGUYEN, khong phai xoa */
                   KetNoi_KhoaGoiHIS     = ISNULL(@KetNoi_KhoaGoiHIS, KetNoi_KhoaGoiHIS),
                   KetNoi_Active         = @KetNoi_Active,
                   KhoaBam               = ISNULL(@KhoaBam, KhoaBam),
                   Khoa_NgayCap          = CASE WHEN @KhoaBam IS NULL THEN Khoa_NgayCap ELSE GETDATE() END,
                   /* 🔴 Man Admin KHONG co o nhap han khoa => luon gui NULL.
                      Ghi thang la moi lan luu co so lai xoa trang han khoa API. */
                   Khoa_NgayHetHan       = ISNULL(@Khoa_NgayHetHan, Khoa_NgayHetHan),
                   Ftp_Host              = @Ftp_Host,
                   /* o bi mat, man Sua de trong => NULL = giu nguyen */
                   Ftp_TaiKhoan          = ISNULL(@Ftp_TaiKhoan, Ftp_TaiKhoan),
                   Ftp_MatKhau           = ISNULL(@Ftp_MatKhau, Ftp_MatKhau),
                   Ftp_ThuMucGoc         = ISNULL(@Ftp_ThuMucGoc, N''),
                   Ftp_Active            = @Ftp_Active,
                   Ftp_NgayThuDat        = @ThuDatMoi,
                   NgayCapNhat           = GETDATE()
             WHERE ID = @ID;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

        /* Rang buoc duy nhat nam o DB chu khong o day (W-01). Dich loi cua SQL
           Server sang thong diep nguoi dung doc duoc. */
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = CASE
                WHEN ERROR_MESSAGE() LIKE '%UK_DM_CSKCB_MaCoSo%'  THEN N'Mã cơ sở đã tồn tại.'
                WHEN ERROR_MESSAGE() LIKE '%UK_DM_CSKCB_Slug%'    THEN N'Đường dẫn (slug) đã được dùng cho cơ sở khác.'
                WHEN ERROR_MESSAGE() LIKE '%UK_DM_CSKCB_KhoaBam%' THEN N'Khoá API này đã được cấp cho cơ sở khác.'
                ELSE N'Dữ liệu bị trùng.' END;
            RETURN;
        END;
        THROW;
    END CATCH;
END;

GO

/* --- tu kiem B10b ------------------------------------------------------- */
SELECT 'B10b stored cong' AS Buoc,
       (SELECT COUNT(*) FROM sys.objects WHERE type = 'P')                     AS TongStored_ky_vong_29,
       (SELECT COUNT(*) FROM sys.objects WHERE type = 'P'
         AND name LIKE 'DM_BenhNhanCoSo[_]%')                                  AS ConTenCu_phai_1,
       (SELECT COUNT(*) FROM sys.objects WHERE type = 'P'
         AND name IN ('DM_BenhNhan_DoiMa','DM_BenhNhan_DoiMocXemLich',
                      'DM_BenhNhan_GoNoi','DM_BenhNhan_TaoTuKhai'))            AS TenMoi_phai_4,
       (SELECT COUNT(*) FROM sys.objects WHERE name = 'DM_DoiTac_Save')        AS DoiTacSave_phai_0,
       (SELECT COUNT(*) FROM sys.sql_modules m JOIN sys.objects o
          ON o.object_id = m.object_id
        WHERE o.type = 'P' AND m.definition LIKE '%dbo.DM_BenhNhanCoSo %')     AS Con_doc_bang_cu_phai_0;
GO
/* ConTenCu_phai_1 = dbo.DM_BenhNhanCoSo_Save - GIU NGUYEN TEN theo hop dong. */
