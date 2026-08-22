CREATE OR ALTER PROCEDURE dbo.Admin_TaiKhoan_Save
    @Id BIGINT,
    @SDT VARCHAR(20),
    @Role NVARCHAR(50),
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @Id = 0
        BEGIN
            IF EXISTS (SELECT 1 FROM TaiKhoan WHERE SDT = @SDT)
            BEGIN
                SET @ResultCode = 2;
                SET @ResultMessage = N'Số điện thoại đã tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            INSERT INTO TaiKhoan (SDT, Role)
            VALUES (@SDT, @Role);
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM TaiKhoan WHERE ID = @Id)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Tài khoản không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            IF EXISTS (SELECT 1 FROM TaiKhoan WHERE SDT = @SDT AND ID <> @Id)
            BEGIN
                SET @ResultCode = 2;
                SET @ResultMessage = N'Số điện thoại đã tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE TaiKhoan
            SET SDT = @SDT,
                Role = @Role
            WHERE ID = @Id;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_NDCSKCB_Get
    @MaCoSo NVARCHAR(10),
    @TenCoSo NVARCHAR(100),
    @LoaiND NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1) NoiDung
    FROM ND_CSKCB
    WHERE LoaiND = @LoaiND
      AND (
            (NULLIF(LTRIM(RTRIM(@MaCoSo)), N'') IS NOT NULL
             AND MaCoSo = NULLIF(LTRIM(RTRIM(@MaCoSo)), N''))
         OR (NULLIF(LTRIM(RTRIM(@MaCoSo)), N'') IS NULL
             AND (MaCoSo IS NULL OR MaCoSo = N'')
             AND TenCoSo = NULLIF(LTRIM(RTRIM(@TenCoSo)), N''))
      )
    ORDER BY ID DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_NDCSKCB_Save
    @MaCoSo NVARCHAR(10),
    @TenCoSo NVARCHAR(100),
    @LoaiND NVARCHAR(20),
    @NoiDung NVARCHAR(MAX),
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    IF @LoaiND IS NULL OR @LoaiND NOT IN (N'gioithieu', N'dichvu', N'doingu', N'trangthietbi', N'lienhe')
    BEGIN
        SET @ResultCode = 4;
        SET @ResultMessage = N'Chủ đề nội dung không hợp lệ.';
        RETURN;
    END;

    DECLARE @NormalizedMaCoSo NVARCHAR(10) = NULLIF(LTRIM(RTRIM(@MaCoSo)), N'');
    DECLARE @NormalizedTenCoSo NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@TenCoSo)), N'');
    DECLARE @NormalizedNoiDung NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@NoiDung)), N'');
    DECLARE @ExistingId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @NormalizedNoiDung IS NULL
        BEGIN
            DELETE FROM ND_CSKCB
            WHERE LoaiND = @LoaiND
              AND (
                    (@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
                 OR (@NormalizedMaCoSo IS NULL
                     AND (MaCoSo IS NULL OR MaCoSo = N'')
                     AND TenCoSo = @NormalizedTenCoSo)
              );
        END
        ELSE
        BEGIN
            SELECT TOP (1) @ExistingId = ID
            FROM ND_CSKCB WITH (UPDLOCK, HOLDLOCK)
            WHERE LoaiND = @LoaiND
              AND (
                    (@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
                 OR (@NormalizedMaCoSo IS NULL
                     AND (MaCoSo IS NULL OR MaCoSo = N'')
                     AND TenCoSo = @NormalizedTenCoSo)
              )
            ORDER BY ID;

            IF @ExistingId IS NULL
            BEGIN
                INSERT INTO ND_CSKCB (MaCoSo, TenCoSo, NoiDung, LoaiND)
                VALUES (@NormalizedMaCoSo, @NormalizedTenCoSo, @NormalizedNoiDung, @LoaiND);
            END
            ELSE
            BEGIN
                UPDATE ND_CSKCB
                SET MaCoSo = @NormalizedMaCoSo,
                    TenCoSo = @NormalizedTenCoSo,
                    NoiDung = @NormalizedNoiDung
                WHERE ID = @ExistingId;

                DELETE FROM ND_CSKCB
                WHERE ID <> @ExistingId
                  AND LoaiND = @LoaiND
                  AND (
                        (@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
                     OR (@NormalizedMaCoSo IS NULL
                         AND (MaCoSo IS NULL OR MaCoSo = N'')
                         AND TenCoSo = @NormalizedTenCoSo)
                  );
            END;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_DoiTac_Save
    @Id BIGINT,
    @MaDT NVARCHAR(20),
    @TenDT NVARCHAR(100),
    @DiaChi NVARCHAR(255),
    @SDT VARCHAR(20),
    @Email VARCHAR(100),
    @BrandName NVARCHAR(100),
    @Password NVARCHAR(255),
    @UpdatePassword BIT,
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM DMDoiTac WHERE MaDT = @MaDT AND ID <> @Id)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Mã đối tác đã tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @Id = 0
        BEGIN
            IF NULLIF(LTRIM(RTRIM(@Password)), N'') IS NULL
            BEGIN
                SET @ResultCode = 4;
                SET @ResultMessage = N'Vui lòng nhập mật khẩu đối tác.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            INSERT INTO DMDoiTac (MaDT, TenDT, DiaChi, SDT, Email, BrandName, Password)
            VALUES (@MaDT, @TenDT, @DiaChi, @SDT, @Email, @BrandName, @Password);
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM DMDoiTac WHERE ID = @Id)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Đối tác không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE DMDoiTac
            SET MaDT = @MaDT,
                TenDT = @TenDT,
                DiaChi = @DiaChi,
                SDT = @SDT,
                Email = @Email,
                BrandName = @BrandName,
                Password = CASE WHEN @UpdatePassword = 1 THEN @Password ELSE Password END
            WHERE ID = @Id;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_CoSoYTe_Save
    @Id BIGINT,
    @MaCoSo NVARCHAR(10),
    @TenCoSo NVARCHAR(100),
    @DiaChi NVARCHAR(255),
    @SoToaNha NVARCHAR(100),
    @Tinh INT,
    @Huyen INT,
    @PhuongXa INT,
    @LoaiCS NVARCHAR(20),
    @TGLamViec NVARCHAR(50),
    @XacMinh INT,
    @Img NVARCHAR(500),
    @Logo NVARCHAR(MAX),
    @QuangCao DECIMAL(15, 0),
    @NoiDungQuangCao NVARCHAR(MAX),
    @QuangCaoImg NVARCHAR(MAX),
    @NoiDungGioiThieu NVARCHAR(MAX),
    @NoiDungDichVu NVARCHAR(MAX),
    @NoiDungDoiNgu NVARCHAR(MAX),
    @NoiDungTrangThietBi NVARCHAR(MAX),
    @NoiDungLienHe NVARCHAR(MAX),
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NULLIF(LTRIM(RTRIM(@MaCoSo)), N'') IS NOT NULL
           AND EXISTS (SELECT 1 FROM DMCSKCB WHERE MaCoSo = @MaCoSo AND ID <> @Id)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Mã cơ sở đã tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        DECLARE @OldMaCoSo NVARCHAR(10) = NULL;
        DECLARE @OldTenCoSo NVARCHAR(100) = NULL;

        IF @Id = 0
        BEGIN
            INSERT INTO DMCSKCB
                (MaCoSo, TenCoSo, DiaChi, SoToaNha, Tinh, Huyen, PhuongXa, LoaiCS,
                 TGLamViec, XacMinh, Img, logo, QuangCao)
            VALUES
                (@MaCoSo, @TenCoSo, @DiaChi, @SoToaNha, @Tinh, @Huyen, @PhuongXa, @LoaiCS,
                 @TGLamViec, @XacMinh, @Img, @Logo, @QuangCao);
        END
        ELSE
        BEGIN
            SELECT @OldMaCoSo = MaCoSo, @OldTenCoSo = TenCoSo
            FROM DMCSKCB WITH (UPDLOCK, HOLDLOCK)
            WHERE ID = @Id;

            IF NOT EXISTS (SELECT 1 FROM DMCSKCB WHERE ID = @Id)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Cơ sở y tế không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE DMCSKCB
            SET MaCoSo = @MaCoSo,
                TenCoSo = @TenCoSo,
                DiaChi = @DiaChi,
                SoToaNha = @SoToaNha,
                Tinh = @Tinh,
                Huyen = @Huyen,
                PhuongXa = @PhuongXa,
                LoaiCS = @LoaiCS,
                TGLamViec = @TGLamViec,
                XacMinh = @XacMinh,
                Img = @Img,
                logo = @Logo,
                QuangCao = @QuangCao
            WHERE ID = @Id;
        END;

        DELETE FROM ND_CSKCB
        WHERE (@OldMaCoSo IS NOT NULL AND @OldMaCoSo <> N'' AND MaCoSo = @OldMaCoSo)
           OR ((@OldMaCoSo IS NULL OR @OldMaCoSo = N'')
               AND (MaCoSo IS NULL OR MaCoSo = N'')
               AND TenCoSo = @OldTenCoSo);

        IF NULLIF(LTRIM(RTRIM(@NoiDungGioiThieu)), N'') IS NOT NULL
            INSERT INTO ND_CSKCB (MaCoSo, TenCoSo, NoiDung, LoaiND)
            VALUES (@MaCoSo, @TenCoSo, LTRIM(RTRIM(@NoiDungGioiThieu)), N'gioithieu');
        IF NULLIF(LTRIM(RTRIM(@NoiDungDichVu)), N'') IS NOT NULL
            INSERT INTO ND_CSKCB (MaCoSo, TenCoSo, NoiDung, LoaiND)
            VALUES (@MaCoSo, @TenCoSo, LTRIM(RTRIM(@NoiDungDichVu)), N'dichvu');
        IF NULLIF(LTRIM(RTRIM(@NoiDungDoiNgu)), N'') IS NOT NULL
            INSERT INTO ND_CSKCB (MaCoSo, TenCoSo, NoiDung, LoaiND)
            VALUES (@MaCoSo, @TenCoSo, LTRIM(RTRIM(@NoiDungDoiNgu)), N'doingu');
        IF NULLIF(LTRIM(RTRIM(@NoiDungTrangThietBi)), N'') IS NOT NULL
            INSERT INTO ND_CSKCB (MaCoSo, TenCoSo, NoiDung, LoaiND)
            VALUES (@MaCoSo, @TenCoSo, LTRIM(RTRIM(@NoiDungTrangThietBi)), N'trangthietbi');
        IF NULLIF(LTRIM(RTRIM(@NoiDungLienHe)), N'') IS NOT NULL
            INSERT INTO ND_CSKCB (MaCoSo, TenCoSo, NoiDung, LoaiND)
            VALUES (@MaCoSo, @TenCoSo, LTRIM(RTRIM(@NoiDungLienHe)), N'lienhe');

        DELETE FROM QC_KCB
        WHERE (@OldMaCoSo IS NOT NULL AND @OldMaCoSo <> N'' AND MaCoSo = @OldMaCoSo)
           OR ((@OldMaCoSo IS NULL OR @OldMaCoSo = N'')
               AND (MaCoSo IS NULL OR MaCoSo = N'')
               AND TenCoSo = @OldTenCoSo);

        IF NULLIF(LTRIM(RTRIM(@NoiDungQuangCao)), N'') IS NOT NULL
           OR NULLIF(LTRIM(RTRIM(@QuangCaoImg)), N'') IS NOT NULL
            INSERT INTO QC_KCB (MaCoSo, TenCoSo, NoiDung, Img)
            VALUES (@MaCoSo, @TenCoSo, NULLIF(LTRIM(RTRIM(@NoiDungQuangCao)), N''),
                    NULLIF(LTRIM(RTRIM(@QuangCaoImg)), N''));

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
