-- ============================================================================
-- 29 — QL_TaiLieuBenhNhan_Save: MAX(PhienBan) chi tim TRONG PHAM VI BAN MOI NHAT
--
-- 🔴 VI SAO CAN FILE NAY (do duoc o dot dung tai 17/09, KHONG phai suy doan):
--    Cau DAU TIEN trong transaction cua thu tuc --
--        SELECT @PhienBan = ISNULL(MAX(PhienBan),0)+1
--        FROM dbo.QL_TaiLieuBenhNhan WITH (UPDLOCK, HOLDLOCK)
--        WHERE IDCoSo=... AND LoaiTaiLieu=... AND MaNguonHIS=...
--    -- KHONG dung duoc mot index nao. Ca hai index khop dung ba cot loc do
--    (UK_QL_TaiLieuBenhNhan_Nguon, IX_QL_TaiLieuBenhNhan_Bam) deu la index CO
--    LOC tren LaBanMoiNhat = 1, ma cau nay CO Y khong loc LaBanMoiNhat.
--    => QUET TOAN BANG, ngay ben trong transaction dang giu UPDLOCK/HOLDLOCK.
--
--    O 245 dong thi vo hinh. O 593.297 dong (bang dung tai) thi:
--      * 28.625 logical reads / 515 ms CPU cho MOI luot ghi;
--      * quet toan bang => optimizer chon KE HOACH SONG SONG;
--      * UPDLOCK + quet toan bang => dat khoa U len GAN NHU MOI TRANG cua bang;
--      * 8 luong cung lam vay => 85 deadlock trong 40 phut, va
--        746/800 luot ghi HONG (ResultCode 99).
--    Thu tuc NUOT loi trong CATCH nen tang goi chi thay "luu khong thanh cong"
--    => o tai that day la MAT TAI LIEU, khong phai chi cham.
--    Benh nay nang dan TUYEN TINH theo so dong va KHONG bien mat khi doi may
--    manh hon -- no la benh cua THIET KE INDEX.
--
-- Noi dung: NGUYEN VAN OBJECT_DEFINITION lay tu HIS_CSKH@118 ngay 17/09/2026
-- (tuc la ban sau file 26), chi THEM DUNG MOT DONG:
--        AND LaBanMoiNhat = 1
-- vao cau SELECT MAX(PhienBan). Sau va: 6 logical reads (giam 4.771 lan),
-- ke hoach uoc luong co 0 toan tu Scan va 0 toan tu Parallelism.
--
-- 🔴 DONG DO TRONG NHU MOT LOI -- chu thich goc ngay ben tren no noi thu tuc
--    can MAX qua MOI phien ban. DUNG GO NO RA. Ly le day du + cau canh gac bat
--    bien nam o docs/adr/0034-phien-ban-lay-max-trong-pham-vi-ban-moi-nhat.md.
--    Tom tat ba bang chung:
--      (1) Bat bien "dong LaBanMoiNhat=1 luon giu MAX(PhienBan)" dung tren
--          TOAN BO du lieu: 0/571.282 nhom vi pham.
--      (2) Chinh thu tuc nay TU BAO TOAN bat bien do (ha co cac ban truoc roi
--          moi chen ban moi voi LaBanMoiNhat = 1).
--      (3) Day la duong GHI DUY NHAT vao bang trong toan bo C#
--          (AdminStoredProcedureService.cs:450). Khong co EF Add/Update/Remove;
--          PhienBan va LaBanMoiNhat CHUA BAO GIO duoc C# ghi.
--    => Them AND LaBanMoiNhat = 1 la TUONG DUONG NGU NGHIA, khong phai danh doi.
--
-- 🔴 CO Y KHONG them index nao. Nut that dang nam o duong GHI; moi index moi la
--    them viec cho INSERT/UPDATE/DELETE.
-- Rollback: doan doi xung o cuoi Database/99_ROLLBACK.sql (muc "Nguoc 29").
-- ============================================================================
GO

/* 🔴 BAT BUOC O DAU MOI FILE TAO STORED -- do song 16/09, mat gan mot gio:
   `sqlcmd` mac dinh chay voi QUOTED_IDENTIFIER **OFF**, va SQL Server GHI LAI
   thiet lap do vao chinh module (sys.sql_modules.uses_quoted_identifier).
   Stored tao ra khi do se VO MOI LAN ghi vao bang co FILTERED INDEX:
     "UPDATE failed because the following SET options have incorrect settings:
      'QUOTED_IDENTIFIER'."
   Va khong cach nao va tu ben goi: SET trong chuoi EXEC chi doi thiet lap
   RUNTIME, con QUOTED_IDENTIFIER cua mot module la thu DONG CUNG LUC TAO.
   (SSMS mac dinh ON nen chay tay o SSMS khong lo ra loi nay -- cang de sot.) */
SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
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
    -- Tran 2000 (file 27). 🔴 Con so nay nam o SAU cho -- doi mot cho ma quen
    -- cac cho kia la quay lai dung benh CAT IM LANG ma chot chan sinh ra de chong.
    @DuongDanFtp    NVARCHAR(2000),
    @DungLuongByte  BIGINT,
    @NgayKham       DATETIME = NULL,
    @GhiChu         NVARCHAR(MAX) = NULL,
    @MaNguonHIS     VARCHAR(50) = NULL,
    @BamNoiDung     CHAR(64) = NULL,
    /* THEM 26: kho chua tep. Mac dinh N'CONG' => moi cho goi cu (C# cua cong)
       giu nguyen hanh vi, khong phai sua mot dong C# nao. Chi che do Tro duong
       ben HIS truyen N'COSO'. */
    @NguonKho       NVARCHAR(20) = N'CONG',
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
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu AND MaNguonHIS = @MaNguonHIS
                  AND LaBanMoiNhat = 1;   -- 29: xem ADR 0034 truoc khi go dong nay

                UPDATE dbo.QL_TaiLieuBenhNhan
                SET LaBanMoiNhat = 0
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu
                  AND MaNguonHIS = @MaNguonHIS AND LaBanMoiNhat = 1;
            END;

            INSERT INTO dbo.QL_TaiLieuBenhNhan (
                IDCoSo, IDBenhNhanCoSo, MaBN, LoaiTaiLieu, TenTaiLieu, DuongDanFtp,
                DungLuongByte, NgayKham, GhiChu, MaNguonHIS, BamNoiDung, NguonKho,
                PhienBan, LaBanMoiNhat, NgayTao)
            VALUES (
                @IDCoSo, @IDBenhNhanCoSo, @MaBN, @LoaiTaiLieu, @TenTaiLieu, @DuongDanFtp,
                @DungLuongByte, @NgayKham, @GhiChu, @MaNguonHIS, @BamNoiDung,
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
