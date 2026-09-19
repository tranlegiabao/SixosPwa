/* =============================================================================
   DOT 1B - buoc B04: DOI CHIEU truoc khi xoa DM_BenhNhanCoSo
   ---------------------------------------------------------------------------
   File nay KHONG SUA GI. Chay xong doc cot KetLuan: phai la "DAT" o moi phep.
   Con mot phep "HONG" thi DUNG, dung chay B05 - bang cu van con de lui.

   Khong dung CHECKSUM_AGG de doi chieu: no la XOR, so chan dong doi giong nhau
   thi triet tieu -> bao khop nham. Doi chieu bang dem + so sanh tung dong.
   ========================================================================== */
SET NOCOUNT ON;
GO
IF OBJECT_ID('bak3.DM_BenhNhanCoSo_B01') IS NULL
    THROW 50020, 'Khong co snapshot bak3 - khong doi chieu duoc. DUNG LAI.', 1;
GO

/* --- P1. so dong ------------------------------------------------------- */
SELECT 'P1. So dong DM_BenhNhan' AS Phep,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan)                                    AS ThucTe,
       (SELECT COUNT(*) FROM bak3.DM_BenhNhanCoSo_B01)
     + (SELECT COUNT(*) FROM bak3.DM_BenhNhan_B01 b
         WHERE NOT EXISTS (SELECT 1 FROM bak3.DM_BenhNhanCoSo_B01 cs WHERE cs.IDBenhNhan = b.ID)) AS KyVong,
       CASE WHEN (SELECT COUNT(*) FROM dbo.DM_BenhNhan)
               = (SELECT COUNT(*) FROM bak3.DM_BenhNhanCoSo_B01)
               + (SELECT COUNT(*) FROM bak3.DM_BenhNhan_B01 b
                   WHERE NOT EXISTS (SELECT 1 FROM bak3.DM_BenhNhanCoSo_B01 cs WHERE cs.IDBenhNhan = b.ID))
            THEN 'DAT' ELSE 'HONG' END AS KetLuan;

/* --- P2. tung dong co so: IDCoSo / MaBN / DaMoTaiLieu / NgayXemLichCuoi -- */
SELECT 'P2. Lech noi dung dong co so' AS Phep,
       COUNT(*) AS SoDongLech, 0 AS KyVong,
       CASE WHEN COUNT(*) = 0 THEN 'DAT' ELSE 'HONG' END AS KetLuan
FROM bak3.DM_BenhNhanCoSo_B01 s
LEFT JOIN dbo.DM_BenhNhan d ON d.ID = s.ID
WHERE d.ID IS NULL
   OR d.IDCoSo <> s.IDCoSo
   OR ISNULL(d.MaBN, '~')      <> ISNULL(s.MaBN, '~')
   OR d.DaMoTaiLieu            <> s.DaMoTaiLieu
   OR ISNULL(d.NgayXemLichCuoi, '1900-01-01') <> ISNULL(s.NgayXemLichCuoi, '1900-01-01')
   OR d.NgayTao                <> s.NgayTao;

/* --- P3. truong con nguoi nhan ban dung nguoi ---------------------------- */
SELECT 'P3. Lech truong con nguoi' AS Phep,
       COUNT(*) AS SoDongLech, 0 AS KyVong,
       CASE WHEN COUNT(*) = 0 THEN 'DAT' ELSE 'HONG' END AS KetLuan
FROM bak3.DM_BenhNhanCoSo_B01 s
JOIN bak3.DM_BenhNhan_B01 b ON b.ID = s.IDBenhNhan
JOIN dbo.DM_BenhNhan d      ON d.ID = s.ID
WHERE d.CCCD <> b.CCCD
   OR d.TenBN <> b.TenBN
   OR ISNULL(d.SDT, '~')           <> ISNULL(b.SDT, '~')
   OR ISNULL(d.Email, '~')         <> ISNULL(b.Email, '~')
   OR ISNULL(d.DiaChi, N'~')       <> ISNULL(b.DiaChi, N'~')
   OR ISNULL(d.HoTenKhongDau, N'~')<> ISNULL(b.HoTenKhongDau, N'~')
   OR ISNULL(d.GioiTinh, '~')      <> ISNULL(b.GioiTinh, '~')
   OR ISNULL(d.NgaySinh, '1900-01-01') <> ISNULL(b.NgaySinh, '1900-01-01');

/* --- P4. 20 dong mo coi co mat du, IDCoSo NULL -------------------------- */
SELECT 'P4. Dong neo (IDCoSo NULL)' AS Phep,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan WHERE IDCoSo IS NULL) AS ThucTe,
       (SELECT COUNT(*) FROM bak3.DM_BenhNhan_B01 b
         WHERE NOT EXISTS (SELECT 1 FROM bak3.DM_BenhNhanCoSo_B01 cs WHERE cs.IDBenhNhan = b.ID)) AS KyVong,
       CASE WHEN (SELECT COUNT(*) FROM dbo.DM_BenhNhan WHERE IDCoSo IS NULL)
               = (SELECT COUNT(*) FROM bak3.DM_BenhNhan_B01 b
                   WHERE NOT EXISTS (SELECT 1 FROM bak3.DM_BenhNhanCoSo_B01 cs WHERE cs.IDBenhNhan = b.ID))
            THEN 'DAT' ELSE 'HONG' END AS KetLuan;

/* --- P5. khong dong con nao mo coi (day la phep song con cua KEEP-ID) ---- */
SELECT 'P5. QL_DotKham mo coi' AS Phep,
       COUNT(*) AS ThucTe, 0 AS KyVong,
       CASE WHEN COUNT(*) = 0 THEN 'DAT' ELSE 'HONG' END AS KetLuan
FROM dbo.QL_DotKham t
LEFT JOIN dbo.DM_BenhNhan b ON b.ID = t.IDBenhNhan
WHERE b.ID IS NULL;

SELECT 'P6. QL_TaiLieuBenhNhan mo coi' AS Phep,
       COUNT(*) AS ThucTe, 0 AS KyVong,
       CASE WHEN COUNT(*) = 0 THEN 'DAT' ELSE 'HONG' END AS KetLuan
FROM dbo.QL_TaiLieuBenhNhan t
LEFT JOIN dbo.DM_BenhNhan b ON b.ID = t.IDBenhNhan
WHERE b.ID IS NULL;

/* --- P7. con tro DUNG NGUOI: doi chieu qua ban sao cu -------------------- */
/* Truoc: QL_DotKham.IDBenhNhanCoSo -> DM_BenhNhanCoSo.ID -> (IDBenhNhan, IDCoSo)
   Sau  : QL_DotKham.IDBenhNhan     -> DM_BenhNhan.ID     -> (IDCoSo)
   Cung mot ID nen co so phai trung. */
SELECT 'P7. DotKham lech co so' AS Phep,
       COUNT(*) AS SoDongLech, 0 AS KyVong,
       CASE WHEN COUNT(*) = 0 THEN 'DAT' ELSE 'HONG' END AS KetLuan
FROM dbo.QL_DotKham t
JOIN bak3.DM_BenhNhanCoSo_B01 s ON s.ID = t.IDBenhNhan
JOIN dbo.DM_BenhNhan d          ON d.ID = t.IDBenhNhan
WHERE d.IDCoSo <> s.IDCoSo;

SELECT 'P8. TaiLieu lech co so' AS Phep,
       COUNT(*) AS SoDongLech, 0 AS KyVong,
       CASE WHEN COUNT(*) = 0 THEN 'DAT' ELSE 'HONG' END AS KetLuan
FROM dbo.QL_TaiLieuBenhNhan t
JOIN bak3.DM_BenhNhanCoSo_B01 s ON s.ID = t.IDBenhNhan
JOIN dbo.DM_BenhNhan d          ON d.ID = t.IDBenhNhan
WHERE d.IDCoSo <> s.IDCoSo;

/* --- P9. rang buoc da dung ---------------------------------------------- */
SELECT 'P9. Rang buoc tren DM_BenhNhan' AS Phep,
       (SELECT COUNT(*) FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.DM_BenhNhan')
           AND name IN ('PK_DM_BenhNhan','UK_DM_BenhNhan_MaBN','UK_DM_BenhNhan_CCCD')) AS ThucTe,
       3 AS KyVong,
       CASE WHEN (SELECT COUNT(*) FROM sys.indexes
                   WHERE object_id = OBJECT_ID('dbo.DM_BenhNhan')
                     AND name IN ('PK_DM_BenhNhan','UK_DM_BenhNhan_MaBN','UK_DM_BenhNhan_CCCD')) = 3
            THEN 'DAT' ELSE 'HONG' END AS KetLuan;
GO
