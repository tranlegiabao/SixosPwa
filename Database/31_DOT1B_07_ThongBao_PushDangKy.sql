/* =============================================================================
   DOT 1B - buoc B07: neo lai HT_ThongBao / HT_PushDangKy (C15 / PA-2a)
   ---------------------------------------------------------------------------
   THU TU CHAY: B07 -> B08 -> B06.
   Ly do: B06 xoa dong HT_TaiKhoan cua benh nhan; muon xoa duoc thi FK tro toi
   HT_TaiKhoan phai duoc go/neo lai TRUOC (chinh la file nay), va dong Role
   ='DoiTac' phai duoc B08 don truoc.

   PA-2a - hai cot tro HAI BANG KHAC NHAU:
     HT_ThongBao.IDNguoiGui  -> HT_TaiKhoan(ID)  [Admin]   giu FK cu
     HT_ThongBao.IDNguoiNhan -> DM_BenhNhan(ID)            FK moi
     HT_PushDangKy.IDTaiKhoan -> doi ten IDBenhNhan -> DM_BenhNhan(ID)  FK moi

   🔴 THONG BAO THANH THEO CO SO - he qua bat buoc cua C12: moi dong DM_BenhNhan
   la mot cap (nguoi x co so), nen benh nhan chi thay thong bao cua co so dang
   dang nhap.

   ---------------------------------------------------------------------------
   CACH DI TRU DA CHON (ghi ro theo yeu cau plan §5):
     Tra nguoc HT_TaiKhoan.SDT -> dong DM_BenhNhan co SDT khop VA IDCoSo IS NOT NULL.
       - HT_ThongBao : mot SDT ung nhieu co so  -> NHAN BAN dong thong bao cho
                       tung co so (thong bao la noi dung, nhan ban duoc).
       - HT_PushDangKy: mot SDT ung nhieu co so -> LAY DONG ID LON NHAT, khong
                       nhan ban (day la dang ky THIET BI, nhan ban se ban trung
                       push nhieu lan ve cung mot may).
     Dong khong tra nguoc duoc thi XOA (khong con cho neo).

   🔴 SO DO THAT ngay 19-09-2026 - KHAC voi du doan cua plan §5:
     HT_ThongBao 112 dong:
        8  benh nhan gui  -> XOA (nguoi gui phai la Admin)
        41 gui doi tac    -> XOA (khong con vai tro DoiTac)
        1  doi tac gui    -> XOA
        62 Admin -> benh nhan: plan tuong GIU duoc, nhung do that thi
           KHONG MOT DONG NAO tra nguoc duoc. 4 tai khoan benh nhan nhan tin
           deu co SDT that, nhung KHONG SO NAO xuat hien trong DM_BenhNhan.SDT
           (dung voi §1.2: tai khoan la seed, SDT ho so khac SDT tai khoan).
        => HT_ThongBao con 0 dong. Day la du lieu THU tren DB dev.
     HT_PushDangKy 29 dong: 10 Admin + 5 DoiTac + 14 benh nhan, trong do chi
        4 dong benh nhan tra nguoc duoc => con 4 dong.
   File van tinh bang cau lenh (khong cheo so cung) de chay dung ca khi DB trôi.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('bak3.HT_ThongBao_B01') IS NULL OR OBJECT_ID('bak3.HT_PushDangKy_B01') IS NULL
    THROW 50070, 'Chua chay B01 snapshot - DUNG LAI.', 1;
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NULL
    THROW 50071, 'Chua chay B02 - DM_BenhNhan chua co IDCoSo. DUNG LAI.', 1;
GO

/* --- 1. go FK tro HT_TaiKhoan ------------------------------------------- */
IF OBJECT_ID('dbo.FK_HT_ThongBao_NguoiNhan') IS NOT NULL
    ALTER TABLE dbo.HT_ThongBao DROP CONSTRAINT FK_HT_ThongBao_NguoiNhan;
IF OBJECT_ID('dbo.FK_HT_PushDangKy_TaiKhoan') IS NOT NULL
    ALTER TABLE dbo.HT_PushDangKy DROP CONSTRAINT FK_HT_PushDangKy_TaiKhoan;
GO
/* CK_HT_ThongBao_KhacNhau so IDNguoiGui <> IDNguoiNhan. Tu nay hai cot tro HAI
   BANG KHAC NHAU nen phep so sanh do vo nghia (va se chan nham khi hai bang
   tinh co trung so). Bo han. */
IF OBJECT_ID('dbo.CK_HT_ThongBao_KhacNhau') IS NOT NULL
    ALTER TABLE dbo.HT_ThongBao DROP CONSTRAINT CK_HT_ThongBao_KhacNhau;
GO

/* --- 2. HT_ThongBao: dung bang dich roi thay the ------------------------ */
IF OBJECT_ID('dbo.HT_ThongBao_Moi') IS NOT NULL DROP TABLE dbo.HT_ThongBao_Moi;
GO
CREATE TABLE dbo.HT_ThongBao_Moi
(
    ID          bigint         NOT NULL IDENTITY(1,1),
    IDNguoiGui  bigint         NOT NULL,   -- HT_TaiKhoan(ID), Admin
    IDNguoiNhan bigint         NOT NULL,   -- DM_BenhNhan(ID), theo CO SO
    NoiDung     nvarchar(2000) NOT NULL,
    ThoiGian    datetime       NOT NULL CONSTRAINT DF_HT_ThongBao_ThoiGian_Moi DEFAULT (GETDATE()),
    DaDoc       bit            NOT NULL CONSTRAINT DF_HT_ThongBao_DaDoc_Moi    DEFAULT ((0)),
    CONSTRAINT PK_HT_ThongBao_Moi PRIMARY KEY CLUSTERED (ID)
);
GO
/* chi giu tin do ADMIN gui, cho nguoi nhan tra nguoc duoc; nhan ban theo co so */
INSERT INTO dbo.HT_ThongBao_Moi (IDNguoiGui, IDNguoiNhan, NoiDung, ThoiGian, DaDoc)
SELECT t.IDNguoiGui, b.ID, t.NoiDung, t.ThoiGian, t.DaDoc
FROM dbo.HT_ThongBao t
JOIN dbo.HT_TaiKhoan g ON g.ID = t.IDNguoiGui AND g.Role = 'Admin'
JOIN dbo.HT_TaiKhoan n ON n.ID = t.IDNguoiNhan AND n.Role = 'BenhNhan'
JOIN dbo.DM_BenhNhan b ON b.SDT = n.SDT AND b.IDCoSo IS NOT NULL;
GO
DECLARE @truoc int = (SELECT COUNT(*) FROM dbo.HT_ThongBao);
DECLARE @sau   int = (SELECT COUNT(*) FROM dbo.HT_ThongBao_Moi);
SELECT 'B07 HT_ThongBao' AS Buoc, @truoc AS Truoc, @sau AS Sau, @truoc - @sau AS SoDongMat;
GO
DROP TABLE dbo.HT_ThongBao;
GO
EXEC sp_rename 'dbo.HT_ThongBao_Moi',              'HT_ThongBao';
EXEC sp_rename 'dbo.PK_HT_ThongBao_Moi',           'PK_HT_ThongBao',           'OBJECT';
EXEC sp_rename 'dbo.DF_HT_ThongBao_ThoiGian_Moi',  'DF_HT_ThongBao_ThoiGian',  'OBJECT';
EXEC sp_rename 'dbo.DF_HT_ThongBao_DaDoc_Moi',     'DF_HT_ThongBao_DaDoc',     'OBJECT';
GO
ALTER TABLE dbo.HT_ThongBao WITH CHECK
    ADD CONSTRAINT FK_HT_ThongBao_NguoiGui  FOREIGN KEY (IDNguoiGui)  REFERENCES dbo.HT_TaiKhoan (ID);
ALTER TABLE dbo.HT_ThongBao WITH CHECK
    ADD CONSTRAINT FK_HT_ThongBao_NguoiNhan FOREIGN KEY (IDNguoiNhan) REFERENCES dbo.DM_BenhNhan (ID);
GO

/* --- 3. HT_PushDangKy: doi ten cot + neo lai ---------------------------- */
/* Xoa dong khong tra nguoc duoc TRUOC khi doi ten, de cau xoa doc de. */
DELETE p
FROM dbo.HT_PushDangKy p
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.HT_TaiKhoan k
    JOIN dbo.DM_BenhNhan b ON b.SDT = k.SDT AND b.IDCoSo IS NOT NULL
    WHERE k.ID = p.IDTaiKhoan AND k.Role = 'BenhNhan');
GO
/* tro sang DM_BenhNhan.ID - mot SDT nhieu co so thi lay ID LON NHAT
   (dang ky thiet bi, khong nhan ban) */
UPDATE p
SET p.IDTaiKhoan = x.IDMoi
FROM dbo.HT_PushDangKy p
CROSS APPLY (
    SELECT MAX(b.ID) AS IDMoi
    FROM dbo.HT_TaiKhoan k
    JOIN dbo.DM_BenhNhan b ON b.SDT = k.SDT AND b.IDCoSo IS NOT NULL
    WHERE k.ID = p.IDTaiKhoan AND k.Role = 'BenhNhan') x
WHERE x.IDMoi IS NOT NULL;
GO
IF COL_LENGTH('dbo.HT_PushDangKy', 'IDTaiKhoan') IS NOT NULL
   AND COL_LENGTH('dbo.HT_PushDangKy', 'IDBenhNhan') IS NULL
    EXEC sp_rename 'dbo.HT_PushDangKy.IDTaiKhoan', 'IDBenhNhan', 'COLUMN';
GO
IF OBJECT_ID('dbo.FK_HT_PushDangKy_BenhNhan') IS NULL
    ALTER TABLE dbo.HT_PushDangKy WITH CHECK
        ADD CONSTRAINT FK_HT_PushDangKy_BenhNhan
            FOREIGN KEY (IDBenhNhan) REFERENCES dbo.DM_BenhNhan (ID);
GO

/* --- 4. tu kiem --------------------------------------------------------- */
SELECT 'B07 neo lai' AS Buoc,
       (SELECT COUNT(*) FROM dbo.HT_ThongBao)                AS ThongBao_con,
       (SELECT COUNT(*) FROM bak3.HT_ThongBao_B01)           AS ThongBao_truoc_112,
       (SELECT COUNT(*) FROM dbo.HT_PushDangKy)              AS Push_con_ky_vong_4,
       (SELECT COUNT(*) FROM bak3.HT_PushDangKy_B01)         AS Push_truoc_29,
       (SELECT COUNT(*) FROM sys.columns
         WHERE object_id = OBJECT_ID('dbo.HT_PushDangKy') AND name = 'IDBenhNhan') AS Push_doi_ten_phai_1,
       (SELECT COUNT(*) FROM sys.foreign_keys
         WHERE name IN ('FK_HT_ThongBao_NguoiGui','FK_HT_ThongBao_NguoiNhan','FK_HT_PushDangKy_BenhNhan')) AS FK_phai_3,
       (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1) AS FK_khong_tin_phai_0;
GO
