using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Services;
using SixosPwa.Services.His;
using SixosPwa.Services.Partner;

namespace SixosPwa.Controllers;

/// <summary>
/// O *Lich kham cua toi* tren *Trang benh nhan noi bo* (ADR 0025 — dong no ADR 0023).
///
/// <para>
/// Tron HAI nguon di HAI duong khac nhau:
/// <list type="bullet">
///   <item><b>Sap toi</b> — hen tai kham bac si da ghi, <i>goi thang</i> HIS, khong
///   giu ban sao. Nguon that la <c>QL_ToaThuoc.NgayTaiKham</c> (7.566 hen tuong lai
///   o Thien Nam), KHONG phai bang giay hen (4 dong — gan chet).</item>
///   <item><b>Da kham</b> — *Dot kham* da nam san trong CSDL cong tu Dot 3. Khong
///   goi HIS: mo modal la co ngay, va HIS chet thi phan nay van hien.</item>
/// </list>
/// </para>
/// <para>
/// 🔴 Hoi HIS o day chu khong o luc render trang: chi hoi KHI NGUOI DUNG BAM mo o.
/// Render trang chi quyet dinh CAI VAN (o hien hay an) — do la truy van CSDL cong,
/// khong ton mot cuoc goi nao sang may khach.
/// </para>
/// </summary>
[Authorize]
public sealed class LichKhamController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IHisDocService _his;
    private readonly IHoSoBenhNhanService _hoSo;
    private readonly AdminStoredProcedureService _thuTuc;

    public LichKhamController(ApplicationDbContext db,
                              IHisDocService his,
                              IHoSoBenhNhanService hoSo,
                              AdminStoredProcedureService thuTuc)
    {
        _db = db;
        _his = his;
        _hoSo = hoSo;
        _thuTuc = thuTuc;
    }

    /// <summary>Mot the tren danh sach. <c>nhom</c> = SAP_TOI | DA_KHAM.</summary>
    public sealed record TheLich(
        string NgayIso,
        string NgayHienThi,
        string Nhom,
        string NoiDung,
        string? ChuyenKhoa,
        bool Moi);

    // ─────────────────────────────────────────────────────────────────────────
    // GET /benh-nhan/lich
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("/benh-nhan/lich")]
    public async Task<IActionResult> DanhSach(CancellationToken ct)
    {
        var boi = await LayBoiCanhAsync(ct);

        if (boi is null)
            return Json(new { trangThai = "CHUA_NOI", the = Array.Empty<TheLich>() });

        var (idBenhNhan, idCoSo, idHoSoCoSo, maBN, moc) = boi.Value;

        // ── Da kham: CSDL cong, luon co ────────────────────────────────────
        var dotKham = await _db.DotKhams.AsNoTracking()
            .Where(d => idHoSoCoSo.Contains(d.IdBenhNhanCoSo))
            .OrderByDescending(d => d.NgayGioVao)
            .Take(50)
            .ToListAsync(ct);

        // Dem tai lieu theo tung dot de the noi duoc "da co N tai lieu" — con so
        // do la thu keo nguoi ta bam vao, khong phai trang tri.
        var soTaiLieu = await _db.TaiLieuBenhNhans.AsNoTracking()
            .Where(t => t.IdBenhNhanCoSo != null
                        && idHoSoCoSo.Contains(t.IdBenhNhanCoSo.Value)
                        && t.LaBanMoiNhat
                        && t.NgayKham != null)
            .GroupBy(t => t.NgayKham!.Value.Date)
            .Select(g => new { Ngay = g.Key, So = g.Count() })
            .ToDictionaryAsync(x => x.Ngay, x => x.So, ct);

        var the = new List<TheLich>();

        foreach (var d in dotKham)
        {
            var ngay = d.NgayGioVao.Date;
            soTaiLieu.TryGetValue(ngay, out var so);

            var noiDung = string.IsNullOrWhiteSpace(d.TenKhoa)
                ? "Khám bệnh."
                : $"Khám tại {d.TenKhoa}.";

            if (so > 0) noiDung += $" Đã có {so} tài liệu.";

            the.Add(new TheLich(
                NgayIso: ngay.ToString("yyyy-MM-dd"),
                NgayHienThi: ngay.ToString("dd/MM/yyyy"),
                Nhom: "DA_KHAM",
                NoiDung: noiDung,
                ChuyenKhoa: d.TenKhoa,
                // Moc dua tren NgayCapNhat: dot kham duoc HIS day lai (sua chan
                // doan, bo sung ngay ra) thi no bat lai MOI — dung y do cua *Moc
                // xem lich*, khong phai tac dung phu.
                Moi: moc is null || (d.NgayCapNhat ?? d.NgayTao) > moc.Value));
        }

        // ── Sap toi: goi thang HIS ─────────────────────────────────────────
        var traLoi = await _his.LayLichHenAsync(idCoSo, maBN, ct);

        // 🔴 BA trang thai tach bac. Dich im lang thanh "ban khong co hen" la loi
        // da can mot lan o Dot 3 — benh nhan co hen tai kham that se tin la minh
        // khong co, va khong mot dau hieu nao bao hong.
        if (traLoi.Xong)
        {
            foreach (var h in traLoi.DuLieu ?? new List<LichHenHis>())
            {
                var ngay = h.NgayHen.Date;

                var chan = new List<string>();
                if (!string.IsNullOrWhiteSpace(h.TenKhoa))  chan.Add(h.TenKhoa!);
                if (!string.IsNullOrWhiteSpace(h.TenBacSi)) chan.Add("BS " + h.TenBacSi);

                the.Add(new TheLich(
                    NgayIso: ngay.ToString("yyyy-MM-dd"),
                    NgayHienThi: ngay.ToString("dd/MM/yyyy"),
                    Nhom: "SAP_TOI",
                    // KHONG co gio: nguon ben HIS kieu date. Bia mot gio ra man la
                    // bao benh nhan den sai luc.
                    NoiDung: $"Bác sĩ hẹn tái khám ngày {ngay:dd/MM/yyyy}.",
                    ChuyenKhoa: chan.Count > 0 ? string.Join(" · ", chan) : null,
                    Moi: moc is null || (h.NgayTao ?? DateTime.MinValue) > moc.Value));
            }
        }

        return Json(new
        {
            trangThai = traLoi.TrangThai switch
            {
                TrangThaiHoiHis.Xong          => "XONG",
                TrangThaiHoiHis.KhongHoiDuoc  => "CHUA_HOI_DUOC",
                _                             => "CHUA_NOI"
            },
            // Sap toi len truoc va gan nhat truoc; da kham thi moi nhat truoc.
            the = the
                .OrderBy(x => x.Nhom == "SAP_TOI" ? 0 : 1)
                .ThenBy(x => x.Nhom == "SAP_TOI" ? x.NgayIso : string.Empty)
                .ThenByDescending(x => x.Nhom == "DA_KHAM" ? x.NgayIso : string.Empty)
                .ToList()
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /benh-nhan/lich/da-xem — doi *Moc xem lich*
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost("/benh-nhan/lich/da-xem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DaXem(CancellationToken ct)
    {
        var boi = await LayBoiCanhAsync(ct);

        if (boi is null) return Json(new { xong = false });

        var (idBenhNhan, idCoSo, _, _, _) = boi.Value;

        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(
            User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty);

        if (idTaiKhoan is null) return Json(new { xong = false });

        // Thu tuc tu kiem ho so thuoc tai khoan nao truoc khi ghi.
        var ketQua = await _thuTuc.DoiMocXemLichAsync(idBenhNhan, idCoSo, idTaiKhoan.Value);

        return Json(new { xong = ketQua.Succeeded });
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ho so dang xem + cac ma cua no tai co so dang xem + *Moc xem lich*.
    /// <c>null</c> khi khong du dieu kien de o nay ton tai.
    /// </summary>
    private async Task<(long IdBenhNhan, long IdCoSo, List<long> IdHoSoCoSo,
                        List<string> MaBN, DateTime? Moc)?> LayBoiCanhAsync(CancellationToken ct)
    {
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(maCoSo) || string.IsNullOrWhiteSpace(dinhDanh))
            return null;

        var idCoSo = await _db.DMCSKCBs.AsNoTracking()
            .Where(c => c.MaCoSo == maCoSo)
            .Select(c => (long?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (idCoSo is null) return null;

        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);
        if (idTaiKhoan is null) return null;

        // *Ho so dang chon* (ADR 0019). Claim CHI duoc phat sau khi da kiem ho so
        // thuoc tai khoan, nhung van loc lai theo IdTaiKhoan o day chu khong tra
        // cuu thang theo claim.
        long.TryParse(User.FindFirst(LuongCongBenhNhan.ClaimHoSoDangChon)?.Value, out var idChon);

        var idBenhNhan = await _db.BenhNhans.AsNoTracking()
            .Where(p => p.IdTaiKhoan == idTaiKhoan.Value && (idChon == 0 || p.Id == idChon))
            .OrderBy(p => p.Id)
            .Select(p => (long?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (idBenhNhan is null) return null;

        var dong = await _db.BenhNhanCoSos.AsNoTracking()
            .Where(h => h.IdBenhNhan == idBenhNhan.Value && h.IdCoSo == idCoSo.Value)
            .ToListAsync(ct);

        if (dong.Count == 0) return null;

        // Moc SOM NHAT trong cac dong: mot dong moi duoc noi them (chua tung xem)
        // phai keo ca danh sach ve trang thai "co cai moi", khong thi ma vua noi
        // mang tai lieu vao ma khong ai bao.
        var moc = dong.Any(h => h.NgayXemLichCuoi is null)
            ? (DateTime?)null
            : dong.Min(h => h.NgayXemLichCuoi);

        return (idBenhNhan.Value,
                idCoSo.Value,
                dong.Select(h => h.Id).ToList(),
                dong.Where(h => h.MaBN != null).Select(h => h.MaBN!).ToList(),
                moc);
    }
}
