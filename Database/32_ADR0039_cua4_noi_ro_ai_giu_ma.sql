/* =============================================================================
   32 - ADR 0039: cua 4 NOI RO ai dang giu ma, thay vi "cap cho nguoi khac"
   ---------------------------------------------------------------------------
   Truoc: rc=2 tra cau co dinh "Ma benh nhan nay da duoc co so cap cho nguoi
          khac." -> HIS chi hien duoc chung chung, nguoi van hanh khong biet
          phai sua ho so NAO.
   Sau  : nhanh CATCH tra them mot cau SELECT lay ten + CCCD nguoi dang giu ma.

   🔴 Tra O DAY (ben cong) chu KHONG de HIS tra nguoc: HIS tra nguoc nghia la
      HIS phai biet ten bang/cot ben cong - mot rang buoc ngam NGOAI 7 doi
      tuong hop dong, doi mot cot la vo trong im lang.
   🔴 CREATE OR ALTER, KHONG BAO GIO DROP+CREATE: DROP lam mat GRANT EXECUTE
      cho login spwa_his, HIS mat duong goi ngay lap tuc.
   🔴 Chu ky stored KHONG DOI (ten, tham so, thu tu) - ADR 0035 khoa. Chi doi
      NOI DUNG chuoi, viec nay ADR 0035 cho phep.

   Nghiem thu (da chay that 19-09 tren HIS_CSKH@118):
     rc = 2
     ResultMessage = 'Ma 102483 da duoc co so cap cho PHAM THI LANH (CCCD ...)'
   ========================================================================== */


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
            /* 🔴 ADR 0039 — noi RO ai dang giu ma, dung noi chung chung.
               rc=2 sinh trong CATCH tu vi pham UNIQUE nen TOI DAY stored van
               CHUA BIET ai giu ma -> phai tra them mot cau. Tra O DAY (ben cong)
               chu khong de HIS tra nguoc: HIS tra nguoc nghia la HIS phai biet
               ten bang/cot ben cong, mot rang buoc ngam NGOAI 7 doi tuong hop
               dong — doi mot cot la vo trong im lang.
               Cau SELECT nay chay SAU khi transaction da rollback (dong tren). */
            DECLARE @tenChu nvarchar(200), @cccdChu varchar(20);
            SELECT TOP 1 @tenChu = TenBN, @cccdChu = CCCD
              FROM dbo.DM_BenhNhan
             WHERE IDCoSo = @IDCoSo AND MaBN = @MaBN;

            SET @ResultCode = 2;
            SET @ResultMessage =
                N'Mã ' + LTRIM(RTRIM(ISNULL(@MaBN, N'(trống)')))
              + N' đã được cơ sở cấp cho ' + ISNULL(@tenChu, N'một người khác')
              + ISNULL(N' (CCCD ' + @cccdChu + N')', N'') + N'.';
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
