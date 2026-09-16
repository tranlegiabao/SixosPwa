-- ============================================================================
-- 26 — QL_TaiLieuBenhNhan_Save nhan them @NguonKho
--
-- 🔴 VI SAO CAN FILE NAY (lo ra luc go Dot 3, ke hoach §2c-b khong thay truoc):
--    Dot 2 da them cot QL_TaiLieuBenhNhan.NguonKho (file 21, chot 35) nhung
--    THU TUC LUU KHONG CO THAM SO TUONG UNG. Cot co DEFAULT N'CONG', nen moi
--    dong do stored sinh ra deu la kho CUA CONG.
--    Che do Tro duong phai ghi N'COSO' — tep nam o Kho phieu co so, cong chi
--    doc. Ma login HIS (file 25) co 0 quyen ghi bang, khong the UPDATE cot do
--    sau khi INSERT. => phai di qua tham so cua stored, khong co duong nao khac.
--    Ghi nham kho thi cong di tim tep o dung FTP cua chinh no => 404 ma khong
--    ai hieu tai sao (day dung la ly do chot 35 bat phai co cot nay).
--
-- Noi dung: NGUYEN VAN OBJECT_DEFINITION lay tu HIS_CSKH@118 ngay 16/09/2026
-- (trung khop file 04_ALTER_QL_TaiLieuBenhNhan.sql), chi them:
--    (a) tham so @NguonKho NVARCHAR(20) = N'CONG'  — CO DEFAULT nen moi cho goi
--        cu cua C# ben cong chay y nguyen, 0 file C# phai sua;
--    (b) cot NguonKho vao dung cau INSERT cua nhanh @ID = 0.
-- 🔴 CO Y KHONG dung toi nhanh UPDATE (@ID <> 0): che do Tro duong KHONG BAO
--    GIO truyen @ID <> 0 (chot 46 — duong doi thi sinh PHIEN BAN MOI), va sua
--    nhanh do la doi hanh vi cua duong API dang chay tot.
-- ============================================================================
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
                WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu AND MaNguonHIS = @MaNguonHIS;

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
