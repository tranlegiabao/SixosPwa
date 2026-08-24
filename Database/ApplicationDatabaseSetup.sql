/*
    Script khởi tạo/cập nhật dữ liệu nền cho SixosPwa.
    Chạy thủ công trên đúng database trước khi khởi động ứng dụng.
    Các batch được viết idempotent để có thể chạy lại an toàn.
*/

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'ThongBao' AND xtype = 'U')
BEGIN
    CREATE TABLE ThongBao (
        Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
        NoiDung     NVARCHAR(1000) NOT NULL,
        ThoiGian    DATETIME2 NOT NULL DEFAULT GETDATE(),
        NguoiGui    NVARCHAR(50) NOT NULL,
        NguoiNhan   NVARCHAR(50) NOT NULL,
        DaDoc       BIT NOT NULL DEFAULT 0
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'PushDangKy' AND xtype = 'U')
BEGIN
    CREATE TABLE PushDangKy (
        Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
        SDT         NVARCHAR(50) NOT NULL,
        Endpoint    NVARCHAR(1000) NOT NULL,
        P256dh      NVARCHAR(500) NOT NULL,
        Auth        NVARCHAR(200) NOT NULL,
        ThoiGian    DATETIME2 NOT NULL DEFAULT GETDATE(),
        IdThietBi   NVARCHAR(100) NULL
    );
END;
GO

IF OBJECT_ID('PushDangKy', 'U') IS NOT NULL
   AND COL_LENGTH('PushDangKy', 'IdThietBi') IS NULL
BEGIN
    ALTER TABLE PushDangKy ADD IdThietBi NVARCHAR(100) NULL;
END;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'PhongKham' AND xtype = 'U')
BEGIN
    CREATE TABLE PhongKham (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        MaPhongKham     NVARCHAR(20) NOT NULL,
        TenPhongKham    NVARCHAR(200) NOT NULL,
        DiaChi          NVARCHAR(500) NULL,
        SoDienThoai     NVARCHAR(20) NULL,
        MoTa            NVARCHAR(1000) NULL,
        LogoUrl         NVARCHAR(500) NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'LichSuKham' AND xtype = 'U')
BEGIN
    CREATE TABLE LichSuKham (
        Id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
        MaBN                NVARCHAR(20) NOT NULL,
        PhongKhamId         BIGINT NOT NULL,
        NgayKhamDau         DATETIME2 NOT NULL DEFAULT GETDATE(),
        NgayKhamGanNhat     DATETIME2 NOT NULL DEFAULT GETDATE(),
        SoLanKham           INT NOT NULL DEFAULT 1,
        TrangThai           NVARCHAR(50) NULL,
        FOREIGN KEY (PhongKhamId) REFERENCES PhongKham(Id)
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'DMThietBi' AND xtype = 'U')
BEGIN
    CREATE TABLE DMThietBi (
        ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
        SDT         VARCHAR(20) NULL,
        MaBN        NVARCHAR(255) NULL,
        IDThietBi   NVARCHAR(100) NULL,
        TrangThai   BIT NOT NULL DEFAULT 1,
        TenThietBi  NVARCHAR(255) NULL
    );
END;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'ND_CSKCB' AND xtype = 'U')
BEGIN
    CREATE TABLE ND_CSKCB (
        ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
        MaCoSo     NVARCHAR(10) NULL,
        TenCoSo    NVARCHAR(100) NULL,
        NoiDung    NVARCHAR(MAX) NULL,
        LoaiND     NVARCHAR(20) NULL
    );
END;
GO

IF OBJECT_ID('ND_CSKCB', 'U') IS NOT NULL
   AND COL_LENGTH('ND_CSKCB', 'LoaiND') IS NOT NULL
   AND (SELECT max_length FROM sys.columns WHERE object_id = OBJECT_ID('ND_CSKCB') AND name = 'LoaiND') < 40
BEGIN
    ALTER TABLE ND_CSKCB ALTER COLUMN LoaiND NVARCHAR(20) NULL;
END;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'QC_KCB' AND xtype = 'U')
BEGIN
    CREATE TABLE QC_KCB (
        ID          BIGINT IDENTITY(1,1) PRIMARY KEY,
        MaCoSo     NVARCHAR(10) NULL,
        TenCoSo    NVARCHAR(100) NULL,
        NoiDung    NVARCHAR(MAX) NULL,
        Img        NVARCHAR(MAX) NULL
    );
END;
GO

IF OBJECT_ID('ND_CSKCB', 'U') IS NOT NULL
   AND OBJECT_ID('DMChuDe', 'U') IS NOT NULL
BEGIN
    UPDATE nd
    SET LoaiND = CONVERT(NVARCHAR(20), cd.ID)
    FROM ND_CSKCB nd
    INNER JOIN DMChuDe cd ON cd.LoaiND = nd.LoaiND
    WHERE TRY_CONVERT(BIGINT, nd.LoaiND) IS NULL;
END;
GO

IF OBJECT_ID('DMCSKCB', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('DMCSKCB', 'Slug') IS NULL
        ALTER TABLE DMCSKCB ADD Slug NVARCHAR(100) NULL;

    IF COL_LENGTH('DMCSKCB', 'SoToaNha') IS NULL
        ALTER TABLE DMCSKCB ADD SoToaNha NVARCHAR(100) NULL;

    IF COL_LENGTH('DMCSKCB', 'PhuongXa') IS NULL
        ALTER TABLE DMCSKCB ADD PhuongXa INT NULL;
END;
GO

IF OBJECT_ID('DMCSKCB', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('DMCSKCB', 'NgayLamViec') IS NULL
        ALTER TABLE DMCSKCB ADD NgayLamViec NVARCHAR(50) NULL;

    IF COL_LENGTH('DMCSKCB', 'GioMoCua') IS NULL
        ALTER TABLE DMCSKCB ADD GioMoCua TIME(0) NULL;

    IF COL_LENGTH('DMCSKCB', 'GioDongCua') IS NULL
        ALTER TABLE DMCSKCB ADD GioDongCua TIME(0) NULL;
END;
GO

IF OBJECT_ID('DMCSKCB', 'U') IS NOT NULL
BEGIN
    UPDATE DMCSKCB
    SET NgayLamViec = LEFT(TGLamViec, CHARINDEX('|', TGLamViec) - 1),
        GioMoCua = TRY_CONVERT(TIME(0), SUBSTRING(TGLamViec, CHARINDEX('|', TGLamViec) + 1, 5)),
        GioDongCua = TRY_CONVERT(TIME(0), RIGHT(TGLamViec, 5))
    WHERE (NgayLamViec IS NULL OR GioMoCua IS NULL OR GioDongCua IS NULL)
      AND TGLamViec IS NOT NULL
      AND CHARINDEX('|', TGLamViec) > 0;
END;
GO

IF NOT EXISTS (SELECT * FROM PhongKham)
BEGIN
    INSERT INTO PhongKham (MaPhongKham, TenPhongKham, DiaChi, SoDienThoai, MoTa, LogoUrl) VALUES
    ('PKDK-BM', N'PKDK Bảo Minh', N'Địa chỉ: Nguyễn Văn Trỗi, Phường Phú Hòa, TP. Bến Cát, Tỉnh Bình Dương', '0911-449-115', N'Phòng khám đa khoa Bảo Minh - Giấy phép hoạt động số: 01083/BĐ-GPHĐ. Tiếp nhận tất cả trường hợp khám chữa bệnh BHYT trong và ngoài tỉnh', '/static/logo-baominh.png'),
    ('PKDK-TĐ', N'PKDK Tâm Đức', N'456 Lê Văn Việt, Quận 9, TP.HCM', '028-3777-7888', N'Phòng khám đa khoa Tâm Đức - Chuyên khoa Tim mạch, Nội tổng quát, tầm soát và điều trị bệnh tim mạch', '/static/logo-tamduc.png'),
    ('PKĐK-AĐ', N'PKĐK Ánh Dương', N'789 Hoàng Diệu, Quận 4, TP.HCM', '028-3666-6777', N'Phòng khám đa khoa Ánh Dương - Chuyên khoa Nhi, Sản phụ khoa, chăm sóc sức khỏe toàn diện', '/static/logo-anhdương.png'),
    ('BV-ĐK', N'Bệnh Viện Đa Khoa Saigon', N'125 Lê Lợi, Quận 1, TP.HCM', '028-3829-2071', N'Bệnh viện đa khoa hạng I - Khám bệnh tổng quát, chuyên khoa sâu, cấp cứu 24/7', '/static/logo-bvdk.png'),
    ('PK-TMH', N'PK Tai Mũi Họng Sài Gòn', N'234 Võ Văn Tần, Quận 3, TP.HCM', '028-3930-3456', N'Chuyên khoa Tai Mũi Họng - Điều trị viêm xoang, viêm amidan, polyp mũi bằng công nghệ hiện đại', '/static/logo-tmh.png'),
    ('PK-MẮT', N'PK Mắt Quốc Tế', N'567 Nguyễn Thị Minh Khai, Quận 3, TP.HCM', '028-3822-5678', N'Phòng khám chuyên khoa Mắt - Khám, điều trị các bệnh về mắt, phẫu thuật mắt laser', '/static/logo-mat.png');

    INSERT INTO LichSuKham (MaBN, PhongKhamId, NgayKhamDau, NgayKhamGanNhat, SoLanKham, TrangThai) VALUES
    ('BN-2026-8892', 1, '2024-03-15', '2026-08-10', 8, N'Đang theo dõi định kỳ'),
    ('BN-2026-8892', 2, '2025-06-10', '2026-07-20', 3, N'Ổn định'),
    ('BN-2026-8892', 4, '2025-01-05', '2026-05-15', 5, N'Đang điều trị'),
    ('BN-2026-8892', 5, '2024-11-20', '2025-12-10', 2, N'Đã khỏi');
END;
GO
