-- ============================================================================
-- 02 — HT_LogApiCoSo: nhat ky doi soat cho khu API nhan
--
-- Ghi MOI cuoc goi vao api/v1/*, ke ca lan bi tu choi. Day la CHO DUY NHAT
-- tra loi duoc hai cau khach hay hoi:
--   * "Hom qua co so day 50 phieu, cong nhan may?"
--   * "Bao nhieu phieu roi vi chua co nguoi nhan?"  (chot 3 dot grill 2)
-- Bam khuon LIS_LogDuLieu ben HisSoft dang chay that.
--
-- KHONG ghi noi dung tep, chi ghi dau vet.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HT_LogApiCoSo')
BEGIN
    CREATE TABLE dbo.HT_LogApiCoSo (
        ID          bigint IDENTITY(1,1) NOT NULL,
        IDCoSo      bigint        NULL,          -- NULL khi khoa sai, chua biet co so nao
        IDKhoa      bigint        NULL,
        Endpoint    varchar(100)  NOT NULL,
        MaBN        varchar(20)   NULL,
        MaNguonHIS  varchar(50)   NULL,
        KetQua      varchar(20)   NOT NULL,      -- NHAN | TU_CHOI | LOI
        LyDo        varchar(50)   NULL,          -- KHOA_SAI | KHOA_TAT | CHUA_CO_NGUOI_NHAN | ...
        SoLuong     int           NULL,          -- so dong trong mot lo (cua dot-kham)
        IpGoi       varchar(45)   NULL,          -- du cho IPv6
        NgayTao     datetime      NOT NULL CONSTRAINT DF_HT_LogApiCoSo_NgayTao DEFAULT (GETDATE()),
        CONSTRAINT PK_HT_LogApiCoSo PRIMARY KEY CLUSTERED (ID ASC)
    );
END;
GO

-- Khong dat FK sang DM_CSKCB/HT_KhoaApiCoSo: nhat ky phai ghi duoc CA khi khoa
-- sai va chua xac dinh duoc co so nao, va phai song sot khi khoa bi xoa.

-- ---------------------------------------------------------------------------
-- Ghi mot dong nhat ky. Tien the danh dau lan dung cuoi cua khoa — de mot cho,
-- khoi phai goi hai luot tu C#.
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.HT_LogApiCoSo_Ghi
    @IDCoSo     bigint       = NULL,
    @IDKhoa     bigint       = NULL,
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

    INSERT INTO dbo.HT_LogApiCoSo
        (IDCoSo, IDKhoa, Endpoint, MaBN, MaNguonHIS, KetQua, LyDo, SoLuong, IpGoi, NgayTao)
    VALUES
        (@IDCoSo, @IDKhoa, @Endpoint, @MaBN, @MaNguonHIS, @KetQua, @LyDo, @SoLuong, @IpGoi, GETDATE());

    IF @IDKhoa IS NOT NULL
    BEGIN
        UPDATE dbo.HT_KhoaApiCoSo SET NgayDungCuoi = GETDATE() WHERE ID = @IDKhoa;
    END;
END;
GO
