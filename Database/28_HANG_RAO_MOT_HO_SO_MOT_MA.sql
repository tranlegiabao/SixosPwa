-- ============================================================================
-- 28 — HANG RAO "mot ho so giu DUNG MOT ma" (chot 47, ADR 0032)
--
-- VAN DE: nhanh UPDATE cua DM_BenhNhanCoSo_Save (file 08, dong 72) lam
--   UPDATE dbo.DM_BenhNhanCoSo SET MaBN = @MaBN WHERE ID = @IDBenhNhanCoSo;
-- KHONG kiem gi ca. Ho so dang noi ma A, ai goi voi ma B la ma doi ngay. Hang
-- rao duy nhat cua he thong dang nam BEN HIS (S00_UploadOnline buoc 3) nen no
-- chi chan DUNG MOT DUONG; bon duong con lai vao thang:
--   HomeController:318 ............ chi chay khi MaBN TRONG   -> khong sao
--   LuongCongBenhNhan:845 ......... chi chay khi MaBN TRONG   -> khong sao
--   LuongCongBenhNhan:726 ......... quet QR co MaBN, goi thang -> LO HONG
--   HoSoBenhNhanService:588 ....... chan "ma da co chu", KHONG chan
--                                   "ho so da co ma khac"     -> LO HONG
--   Admin/TaiKhoanController:350 .. CO Y doi ma (bo phan ho tro)
--
-- CACH LAM (chot 60): dung MOT tham so "@ChoPhepDoiMa" thi vo dung, vi login
-- spwa_his dang co EXECUTE tren DM_BenhNhanCoSo_Save (file 25 dong 92) => HIS
-- chi can truyen co la xuyen rao. Nen tach hai Y DINH thanh HAI CUA:
--   * DM_BenhNhanCoSo_Save  — luu ho so. DIEN duoc vao cho TRONG, KHONG doi
--                             duoc ma da co. Ai cung goi duoc.
--   * DM_BenhNhanCoSo_DoiMa — CO Y doi ma, CO GHI SO. 🔴 KHONG cap cho
--                             spwa_his => HIS khong the goi, du co muon.
-- Hang rao giu bang QUYEN, khong giu bang quy uoc.
--
-- 🔴 KHONG sua file 25 (GRANT). Do chinh la hang rao.
-- 🔴 KHONG sua S00_UploadOnline ben HIS: giu CA HAI hang rao (chot 61) — ben
--    HIS chan som de tiet kiem RPC (~460 -> ~310 luot), ben cong la chot cuoi.
--
-- Chay SAU 08 va 09.
-- ============================================================================

/* 🔴 BAT BUOC O DAU MOI FILE TAO STORED -- do song 16/09 (bay so 2):
   `sqlcmd` mac dinh chay voi QUOTED_IDENTIFIER **OFF**, va SQL Server GHI LAI
   thiet lap do vao chinh module. Stored tao ra khi do VO MOI LAN ghi vao bang
   co FILTERED INDEX -- ma DM_BenhNhanCoSo co UK loc. Khong va duoc tu ben goi.
   (SSMS mac dinh ON nen chay tay khong lo ra -- cang de sot.) */
SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO

PRINT N'--- 28: truoc khi chay ---';
SELECT SoHoSoCoMa = COUNT(*) FROM dbo.DM_BenhNhanCoSo
 WHERE NULLIF(LTRIM(RTRIM(ISNULL(MaBN, ''))), '') IS NOT NULL;
GO

-- ---------------------------------------------------------------------------
-- CUA 1 — LUU HO SO. Y het ban o file 08, THEM DUNG MOT hang rao o nhanh ELSE.
--
-- 🔴 CREATE OR ALTER, tuyet doi khong DROP+CREATE: DROP lam BAY HET GRANT da
--    cap cho spwa_his (bay so 3 cua phien 8).
-- ---------------------------------------------------------------------------
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

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @IDBenhNhanCoSo = ID
        FROM dbo.DM_BenhNhanCoSo WITH (UPDLOCK, HOLDLOCK)
        WHERE IDBenhNhan = @IDBenhNhan AND IDCoSo = @IDCoSo;

        IF @IDBenhNhanCoSo IS NULL
        BEGIN
            INSERT INTO dbo.DM_BenhNhanCoSo (IDBenhNhan, IDCoSo, MaBN, DaMoTaiLieu)
            VALUES (@IDBenhNhan, @IDCoSo, @MaBN, @DaMoTaiLieu);
            SET @IDBenhNhanCoSo = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            /* ------------------- HANG RAO CHOT 47 ---------------------------
               Ba ca, chi MOT ca bi chan:
                 (a) @MaDangCo NULL  -> ho so TRANG MA, dien binh thuong.
                     🔴 Khong duoc chan ca nay: Cua 4 cua S00_UploadOnline goi
                     vao day dung o ca "ho so co san nhung trang ma" (do song
                     16/09 phien 8). Chan la gay duong Tro duong.
                 (b) @MaDangCo = @MaBN -> luu lai cung ma, HOP LE, tra 1.
                     Luu nhieu lan la chuyen binh thuong cua mot ham Save.
                 (c) @MaDangCo khac rong VA khac @MaBN -> TU CHOI MEM.
                     Khong THROW: 4/5 noi goi khong bat exception, nem ra la vo
                     luong chay. Tra ResultCode 3 thi ca 5 noi deu xu ly tu te
                     (AdminStoredProcedureResult.Succeeded => Code == 1).
               ---------------------------------------------------------------- */
            SELECT @MaDangCo = NULLIF(LTRIM(RTRIM(ISNULL(MaBN, ''))), '')
            FROM dbo.DM_BenhNhanCoSo WHERE ID = @IDBenhNhanCoSo;

            IF @MaDangCo IS NOT NULL AND @MaDangCo <> LTRIM(RTRIM(@MaBN))
            BEGIN
                ROLLBACK TRANSACTION;
                SET @ResultCode = 3;
                SET @ResultMessage = N'Hồ sơ này đang nối mã ' + @MaDangCo
                                   + N' — muốn đổi sang ' + LTRIM(RTRIM(@MaBN))
                                   + N' thì phải đi Cửa đổi mã.';
                RETURN;
            END;

            /* Nhanh nay CO Y khong dung toi DaMoTaiLieu: mot cua da bi dong CO
               Y thi khong duoc lang le mo lai chi vi ai do luu lai ho so. */
            UPDATE dbo.DM_BenhNhanCoSo SET MaBN = @MaBN WHERE ID = @IDBenhNhanCoSo;
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
            SET @ResultMessage = N'Mã bệnh nhân này đã được cơ sở cấp cho người khác.';
            RETURN;
        END;
        IF ERROR_NUMBER() = 547
        BEGIN
            SET @ResultCode = 4;
            SET @ResultMessage = N'Bệnh nhân hoặc cơ sở không tồn tại.';
            RETURN;
        END;
        THROW;
    END CATCH;
END;
GO

-- ---------------------------------------------------------------------------
-- CUA 2 — DOI MA. Viec CO Y, CO GHI SO, danh cho bo phan ho tro sua mot lan
-- noi sai.
--
-- Chu ky GIONG HET _Save (cong @LyDo) de ben C# dung lai duoc ExecuteWithIdAsync
-- khong phai viet duong goi moi.
--
-- 🔴 KHONG dung toi cot DaMoTaiLieu — cung luat voi nhanh UPDATE cua _Save
--    (chot 49): doi ma khong duoc lang le mo mot cua da dong.
-- 🔴 KHONG cap EXECUTE cho spwa_his. Xem dau file.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.DM_BenhNhanCoSo_DoiMa
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
    DECLARE @MaMoi    varchar(20) = LTRIM(RTRIM(ISNULL(@MaBN, '')));
    DECLARE @GhiChu   varchar(50);

    IF @MaMoi = ''
    BEGIN
        SET @ResultCode = 4;
        SET @ResultMessage = N'Chưa nhập mã bệnh nhân mới.';
        RETURN;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @IDBenhNhanCoSo = ID,
               @MaDangCo = NULLIF(LTRIM(RTRIM(ISNULL(MaBN, ''))), '')
        FROM dbo.DM_BenhNhanCoSo WITH (UPDLOCK, HOLDLOCK)
        WHERE IDBenhNhan = @IDBenhNhan AND IDCoSo = @IDCoSo;

        /* Khong co dong -> KHONG tu tao. Tao ho so la viec cua _Save; gop hai
           viec vao mot cua la mat luon y nghia cua viec tach cua. */
        IF @IDBenhNhanCoSo IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            SET @ResultCode = 4;
            SET @ResultMessage = N'Hồ sơ chưa tồn tại ở cơ sở này — dùng cửa Lưu hồ sơ trước.';
            RETURN;
        END;

        /* Doi sang dung cai dang co -> khong phai mot lan doi ma, tra OK luon,
           KHONG ghi so (ghi so mot viec khong xay ra la lam ban nhat ky). */
        IF @MaDangCo IS NOT NULL AND @MaDangCo = @MaMoi
        BEGIN
            COMMIT TRANSACTION;
            SET @ResultCode = 1;
            SET @ResultMessage = N'Hồ sơ đã mang đúng mã này rồi.';
            RETURN;
        END;

        UPDATE dbo.DM_BenhNhanCoSo SET MaBN = @MaMoi WHERE ID = @IDBenhNhanCoSo;

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
        THROW;
    END CATCH;

    /* GHI SO NGOAI transaction: nhat ky hong khong duoc lam hong viec doi ma.
       LyDo chi 50 ky tu nen chi nhet duoc "cu -> moi"; van ban dai cua nguoi
       ho tro khong co cho, va do la co y (bang nhat ky khong phai bang ghi chu). */
    BEGIN TRY
        SET @GhiChu = LEFT(ISNULL(@MaDangCo, '(trong)') + '->' + @MaMoi, 50);
        EXEC dbo.HT_LogApiCoSo_Ghi
             @IDCoSo     = @IDCoSo,
             @Endpoint   = 'DM_BenhNhanCoSo_DoiMa',
             @MaBN       = @MaMoi,
             @MaNguonHIS = @MaDangCo,
             @KetQua     = 'NHAN',
             @LyDo       = @GhiChu;
    END TRY
    BEGIN CATCH
        PRINT N'  [!] 28: doi ma XONG nhung ghi so that bai: ' + ERROR_MESSAGE();
    END CATCH;
END;
GO

PRINT N'--- 28: kiem tra ---';
SELECT Ten = name,
       QuotedIdentifier = OBJECTPROPERTY(object_id, 'ExecIsQuotedIdentOn')
  FROM sys.procedures
 WHERE name IN ('DM_BenhNhanCoSo_Save', 'DM_BenhNhanCoSo_DoiMa');
GO

/* 🔴 Cot QuotedIdentifier o tren PHAI = 1 cho ca hai dong. Bang 0 la file da
   chay bang sqlcmd khong co SET o dau => moi lan ghi vao DM_BenhNhanCoSo se vo.
   Chay lai file nay (co SET) la sua duoc. */

PRINT N'--- 28: ai duoc phep mo Cua doi ma ---';
SELECT NguoiDuocCap = USER_NAME(p.grantee_principal_id), Quyen = p.permission_name
  FROM sys.database_permissions p
 WHERE p.major_id = OBJECT_ID('dbo.DM_BenhNhanCoSo_DoiMa');
GO

/* 🔴 Ket qua dung cua truy van tren la KHONG CO DONG NAO ngoai chu so huu.
   Neu thay `spwa_his` xuat hien o day thi hang rao chot 47 DA THUNG — ai do da
   them stored nay vao danh sach o file 25. Go ra:
       REVOKE EXECUTE ON dbo.DM_BenhNhanCoSo_DoiMa FROM [spwa_his];
*/
