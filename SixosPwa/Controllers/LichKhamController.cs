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
/// Ở *Lịch khám của tôi* trên *Trang bệnh nhân nội bộ* (ADR 0025 — dòng nợ ADR 0023).
///
/// <para>
/// Trộn HAI nguồn đi HAI đường khác nhau:
/// <list type="bullet">
///   <item><b>Sắp tới</b> — hẹn tái khám bác sĩ đã ghi, <i>gọi thẳng</i> HIS, không
///   giữ bản sao. Nguồn thật là <c>QL_ToaThuoc.NgayTaiKham</c> (7.566 hẹn tương lai
///   ở Thiện Nam), KHÔNG phải bảng giấy hẹn (4 dòng — gần chết).</item>
///   <item><b>Đã khám</b> — *Đợt khám* đã nằm sẵn trong CSDL cổng từ Đợt 3. Không
///   gọi HIS: mở modal là có ngay, và HIS chết thì phần này vẫn hiện.</item>
/// </list>
/// </para>
/// <para>
/// 🔴 Hỏi HIS ở đây chứ không ở lúc render trang: chỉ hỏi KHI NGƯỜI DÙNG BẤM mở ô.
/// Render trang chỉ quyết định CÁI VỎ (ô hiện hay ẩn) — đó là truy vấn CSDL cổng,
/// không tốn một cuộc gọi nào sang máy khách.
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

    /// <summary>Một thẻ trên danh sách. <c>nhom</c> = SAP_TOI | DA_KHAM.</summary>
    public sealed record TheLich(
        string NgayIso,
        string NgayHienThi,
        string Nhom,
        string NoiDung,
        string? ChuyenKhoa,
        bool Moi);

    [HttpGet("/benh-nhan/lich")]
    public async Task<IActionResult> DanhSach(CancellationToken ct)
    {
        var boi = await LayBoiCanhAsync(ct);

        if (boi is null)
            return Json(new { trangThai = "CHUA_NOI", the = Array.Empty<TheLich>() });

        var (idBenhNhan, idCoSo, idHoSoCoSo, maBN, moc) = boi.Value;

        // ── Đã khám: CSDL công, luôn có ────────────────────────────────────
        var dotKham = await _db.DotKhams.AsNoTracking()
            .Where(d => idHoSoCoSo.Contains(d.IdBenhNhan))
            .OrderByDescending(d => d.NgayGioVao)
            .Take(50)
            .ToListAsync(ct);

        // Đếm tài liệu theo từng đợt để thẻ nói được "đã có N tài liệu" — con số
        // đó là thứ kéo người ta bấm vào, không phải trang trí.
        var soTaiLieu = await _db.TaiLieuBenhNhans.AsNoTracking()
            .Where(t => t.IdBenhNhan != null
                        && idHoSoCoSo.Contains(t.IdBenhNhan.Value)
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
                // Mốc dựa trên NgayCapNhat: đợt khám được HIS đẩy lại (sửa chẩn
                // đoán, bổ sung ngày ra) thì nó bật lại MỚI — đúng ý đồ của *Mốc
                // xem lịch*, không phải tác dụng phụ.
                Moi: moc is null || (d.NgayCapNhat ?? d.NgayTao) > moc.Value));
        }

        // ── Sắp tới: gọi thẳng HIS ─────────────────────────────────────────
        var traLoi = await _his.LayLichHenAsync(idCoSo, maBN, ct);

        // 🔴 BA trạng thái tách bạch. Dịch im lặng thành "bạn không có hẹn" là lỗi
        // đã cắn một lần ở Đợt 3 — bệnh nhân có hẹn tái khám thật sẽ tin là mình
        // không có, và không một dấu hiệu nào báo hỏng.
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
                    // KHÔNG có giờ: nguồn bên HIS kiểu date. Bịa một giờ ra màn là
                    // báo bệnh nhân đến sai lúc.
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
            // Sắp tới lên trước và gần nhất trước; đã khám thì mới nhất trước.
            the = the
                .OrderBy(x => x.Nhom == "SAP_TOI" ? 0 : 1)
                .ThenBy(x => x.Nhom == "SAP_TOI" ? x.NgayIso : string.Empty)
                .ThenByDescending(x => x.Nhom == "DA_KHAM" ? x.NgayIso : string.Empty)
                .ToList()
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /benh-nhan/lich/da-xem — đổi *Mốc xem lịch*
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost("/benh-nhan/lich/da-xem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DaXem(CancellationToken ct)
    {
        var boi = await LayBoiCanhAsync(ct);

        if (boi is null) return Json(new { xong = false });

        var (idBenhNhan, idCoSo, _, _, _) = boi.Value;

        var sdtPhien = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sdtPhien)) return Json(new { xong = false });

        // Thủ tục tự kiểm hồ sơ có thuộc về (SDT x cơ sở) của phiên không.
        var ketQua = await _thuTuc.DoiMocXemLichAsync(idBenhNhan, idCoSo, sdtPhien);

        return Json(new { xong = ketQua.Succeeded });
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hồ sơ đang xem + các mã của nó tại cơ sở đang xem + *Mốc xem lịch*.
    /// <c>null</c> khi không đủ điều kiện để ô này tồn tại.
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

        if (string.IsNullOrWhiteSpace(dinhDanh)) return null;

        // *Hồ sơ đang chọn*. Claim CHỈ được phát sau khi đã kiểm hồ sơ thuộc về
        // phiên, nhưng vẫn lọc lại theo (SDT x cơ sở) ở đây chứ không tra cứu
        // thẳng theo claim.
        long.TryParse(User.FindFirst(LuongCongBenhNhan.ClaimHoSoDangChon)?.Value, out var idChon);

        var idBenhNhan = await _db.BenhNhans.AsNoTracking()
            .Where(p => p.SDT == dinhDanh && p.IdCoSo == idCoSo.Value && (idChon == 0 || p.Id == idChon))
            .OrderBy(p => p.Id)
            .Select(p => (long?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (idBenhNhan is null) return null;

        var dong = await _db.BenhNhans.AsNoTracking()
            .Where(h => h.Id == idBenhNhan.Value && h.IdCoSo == idCoSo.Value)
            .ToListAsync(ct);

        if (dong.Count == 0) return null;

        // Mốc SỚM NHẤT trong các dòng: một dòng mới được nối thêm (chưa từng xem)
        // phải kéo cả danh sách về trạng thái "có cái mới", không thì mã vừa nối
        // mang tài liệu vào mà không ai báo.
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
