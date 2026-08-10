using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>
/// Service bệnh nhân dùng in-memory (demo) - production nên dùng database
/// </summary>
public class InMemoryBenhNhanService : IBenhNhanService
{
    private static readonly List<BenhNhan> _danhSachBenhNhan = new()
    {
        new BenhNhan { Id = 1, MaBenhNhan = "BN001", HoTen = "Nguyễn Văn An", SoDienThoai = "0901234567", NgaySinh = new DateTime(1990, 5, 15), GioiTinh = "Nam", DiaChi = "Hà Nội", ChoPhepNhanTinNhan = true },
        new BenhNhan { Id = 2, MaBenhNhan = "BN002", HoTen = "Trần Thị Bình", SoDienThoai = "0912345678", NgaySinh = new DateTime(1985, 3, 20), GioiTinh = "Nữ", DiaChi = "TP.HCM", ChoPhepNhanTinNhan = true },
        new BenhNhan { Id = 3, MaBenhNhan = "BN003", HoTen = "Lê Minh Cường", SoDienThoai = "0923456789", NgaySinh = new DateTime(1995, 7, 10), GioiTinh = "Nam", DiaChi = "Đà Nẵng", ChoPhepNhanTinNhan = false },
        new BenhNhan { Id = 4, MaBenhNhan = "BN004", HoTen = "Phạm Thị Dung", SoDienThoai = "0926007363", NgaySinh = new DateTime(1988, 11, 25), GioiTinh = "Nữ", DiaChi = "Hải Phòng", ChoPhepNhanTinNhan = true }
    };

    public Task<List<BenhNhan>> LayDanhSachBenhNhanAsync()
    {
        return Task.FromResult(_danhSachBenhNhan.ToList());
    }

    public Task<BenhNhan?> LayBenhNhanTheoIdAsync(int id)
    {
        return Task.FromResult(_danhSachBenhNhan.FirstOrDefault(b => b.Id == id));
    }

    public Task<BenhNhan?> LayBenhNhanTheoSoDienThoaiAsync(string soDienThoai)
    {
        return Task.FromResult(_danhSachBenhNhan.FirstOrDefault(b => b.SoDienThoai == soDienThoai));
    }

    public Task<bool> ThemBenhNhanAsync(BenhNhan benhNhan)
    {
        benhNhan.Id = _danhSachBenhNhan.Any() ? _danhSachBenhNhan.Max(b => b.Id) + 1 : 1;
        _danhSachBenhNhan.Add(benhNhan);
        return Task.FromResult(true);
    }

    public Task<bool> CapNhatBenhNhanAsync(BenhNhan benhNhan)
    {
        var existing = _danhSachBenhNhan.FirstOrDefault(b => b.Id == benhNhan.Id);
        if (existing == null) return Task.FromResult(false);

        existing.HoTen = benhNhan.HoTen;
        existing.SoDienThoai = benhNhan.SoDienThoai;
        existing.NgaySinh = benhNhan.NgaySinh;
        existing.GioiTinh = benhNhan.GioiTinh;
        existing.DiaChi = benhNhan.DiaChi;
        existing.Email = benhNhan.Email;
        existing.ChoPhepNhanTinNhan = benhNhan.ChoPhepNhanTinNhan;
        existing.NgayCapNhat = DateTime.Now;

        return Task.FromResult(true);
    }

    public Task<int> ImportDanhSachBenhNhanAsync(List<ImportBenhNhanDto> danhSach)
    {
        int count = 0;
        foreach (var dto in danhSach)
        {
            if (string.IsNullOrEmpty(dto.SoDienThoai)) continue;

            var existing = _danhSachBenhNhan.FirstOrDefault(b => b.SoDienThoai == dto.SoDienThoai);
            if (existing != null) continue; // Bỏ qua nếu đã tồn tại

            var benhNhan = new BenhNhan
            {
                Id = _danhSachBenhNhan.Any() ? _danhSachBenhNhan.Max(b => b.Id) + 1 : 1,
                MaBenhNhan = dto.MaBenhNhan,
                HoTen = dto.HoTen,
                SoDienThoai = dto.SoDienThoai,
                GioiTinh = dto.GioiTinh ?? "",
                DiaChi = dto.DiaChi ?? "",
                Email = dto.Email ?? "",
                ChoPhepNhanTinNhan = true
            };

            if (DateTime.TryParse(dto.NgaySinh, out var ngaySinh))
            {
                benhNhan.NgaySinh = ngaySinh;
            }

            _danhSachBenhNhan.Add(benhNhan);
            count++;
        }

        return Task.FromResult(count);
    }

    public Task<bool> CapNhatTrangThaiNhanTinNhanAsync(int benhNhanId, bool choPhep)
    {
        var benhNhan = _danhSachBenhNhan.FirstOrDefault(b => b.Id == benhNhanId);
        if (benhNhan == null) return Task.FromResult(false);

        benhNhan.ChoPhepNhanTinNhan = choPhep;
        benhNhan.NgayCapNhat = DateTime.Now;

        return Task.FromResult(true);
    }
}

/// <summary>
/// Service lịch sử tin nhắn in-memory
/// </summary>
public class InMemoryLichSuTinNhanService : ILichSuTinNhanService
{
    private static readonly List<LichSuTinNhan> _lichSu = new();

    public Task<bool> LuuLichSuAsync(LichSuTinNhan lichSu)
    {
        lichSu.Id = _lichSu.Any() ? _lichSu.Max(l => l.Id) + 1 : 1;
        _lichSu.Add(lichSu);
        return Task.FromResult(true);
    }

    public Task<List<LichSuTinNhan>> LayLichSuTheoBenhNhanAsync(int benhNhanId)
    {
        return Task.FromResult(_lichSu.Where(l => l.BenhNhanId == benhNhanId).OrderByDescending(l => l.ThoiGianGui).ToList());
    }

    public Task<List<LichSuTinNhan>> LayTatCaLichSuAsync(DateTime? tuNgay = null, DateTime? denNgay = null)
    {
        var query = _lichSu.AsQueryable();

        if (tuNgay.HasValue)
            query = query.Where(l => l.ThoiGianGui >= tuNgay.Value);

        if (denNgay.HasValue)
            query = query.Where(l => l.ThoiGianGui <= denNgay.Value);

        return Task.FromResult(query.OrderByDescending(l => l.ThoiGianGui).ToList());
    }

    public Task<bool> CapNhatTrangThaiAsync(int id, string trangThai)
    {
        var lichSu = _lichSu.FirstOrDefault(l => l.Id == id);
        if (lichSu == null) return Task.FromResult(false);

        lichSu.TrangThai = trangThai;
        return Task.FromResult(true);
    }
}

/// <summary>
/// Service mẫu tin nhắn in-memory
/// </summary>
public class InMemoryMauTinNhanService : IMauTinNhanService
{
    private static readonly List<MauTinNhan> _danhSachMau = new()
    {
        new MauTinNhan { Id = 1, TenMau = "Nhắc lịch khám", LoaiTinNhan = "NhacLichKham", NoiDung = "Chào {TenBenhNhan}, nhắc lịch khám: {NgayKham} lúc {GioKham}. Bác sĩ: {TenBacSi}. Vui lòng đến trước 15 phút.", MoTa = "Template nhắc lịch khám cơ bản", KichHoat = true },
        new MauTinNhan { Id = 2, TenMau = "Kết quả xét nghiệm", LoaiTinNhan = "KetQuaXetNghiem", NoiDung = "Chào {TenBenhNhan}, kết quả {LoaiXetNghiem} đã sẵn sàng. Xem tại: {Link}", MoTa = "Thông báo kết quả xét nghiệm", KichHoat = true },
        new MauTinNhan { Id = 3, TenMau = "Nhắc uống thuốc", LoaiTinNhan = "NhacUongThuoc", NoiDung = "Nhắc nhở: Đã đến giờ uống thuốc {TenThuoc}. Liều dùng: {LieuDung}. Chúc mau khỏe!", MoTa = "Nhắc nhở uống thuốc đúng giờ", KichHoat = true },
        new MauTinNhan { Id = 4, TenMau = "Cảm ơn sau khám", LoaiTinNhan = "CamOn", NoiDung = "Cảm ơn {TenBenhNhan} đã tin tưởng sử dụng dịch vụ. Chúc bạn sớm bình phục!", MoTa = "Tin nhắn cảm ơn sau khi khám", KichHoat = true }
    };

    public Task<List<MauTinNhan>> LayDanhSachMauAsync()
    {
        return Task.FromResult(_danhSachMau.Where(m => m.KichHoat).ToList());
    }

    public Task<MauTinNhan?> LayMauTheoIdAsync(int id)
    {
        return Task.FromResult(_danhSachMau.FirstOrDefault(m => m.Id == id));
    }

    public Task<bool> ThemMauAsync(MauTinNhan mau)
    {
        mau.Id = _danhSachMau.Any() ? _danhSachMau.Max(m => m.Id) + 1 : 1;
        _danhSachMau.Add(mau);
        return Task.FromResult(true);
    }

    public Task<bool> CapNhatMauAsync(MauTinNhan mau)
    {
        var existing = _danhSachMau.FirstOrDefault(m => m.Id == mau.Id);
        if (existing == null) return Task.FromResult(false);

        existing.TenMau = mau.TenMau;
        existing.LoaiTinNhan = mau.LoaiTinNhan;
        existing.NoiDung = mau.NoiDung;
        existing.MoTa = mau.MoTa;
        existing.KichHoat = mau.KichHoat;

        return Task.FromResult(true);
    }

    public Task<bool> XoaMauAsync(int id)
    {
        var mau = _danhSachMau.FirstOrDefault(m => m.Id == id);
        if (mau == null) return Task.FromResult(false);

        mau.KichHoat = false;
        return Task.FromResult(true);
    }
}

/// <summary>
/// Service hóa đơn in-memory
/// </summary>
public class InMemoryHoaDonService : IHoaDonService
{
    private static readonly List<HoaDon> _danhSachHoaDon = new()
    {
        new HoaDon { Id = 1, BenhNhanId = 1, MaHoaDon = "HD001", NgayKham = DateTime.Now.AddDays(-7), DichVu = "Khám tim mạch", TongTien = 500000, DaThanhToan = 500000, ConLai = 0, TrangThai = "DaThanhToan", NgayThanhToan = DateTime.Now.AddDays(-7) },
        new HoaDon { Id = 2, BenhNhanId = 1, MaHoaDon = "HD002", NgayKham = DateTime.Now.AddDays(-2), DichVu = "Xét nghiệm máu", TongTien = 300000, DaThanhToan = 150000, ConLai = 150000, TrangThai = "ThanhToanMotPhan" },
        new HoaDon { Id = 3, BenhNhanId = 2, MaHoaDon = "HD003", NgayKham = DateTime.Now.AddDays(-5), DichVu = "Khám nội tổng quát", TongTien = 700000, DaThanhToan = 0, ConLai = 700000, TrangThai = "ChuaThanhToan" }
    };

    public Task<List<HoaDon>> LayDanhSachHoaDonTheoBenhNhanAsync(int benhNhanId)
    {
        return Task.FromResult(_danhSachHoaDon.Where(h => h.BenhNhanId == benhNhanId).OrderByDescending(h => h.NgayKham).ToList());
    }

    public Task<HoaDon?> LayHoaDonTheoIdAsync(int id)
    {
        return Task.FromResult(_danhSachHoaDon.FirstOrDefault(h => h.Id == id));
    }

    public Task<bool> ThemHoaDonAsync(HoaDon hoaDon)
    {
        hoaDon.Id = _danhSachHoaDon.Any() ? _danhSachHoaDon.Max(h => h.Id) + 1 : 1;
        _danhSachHoaDon.Add(hoaDon);
        return Task.FromResult(true);
    }

    public Task<bool> CapNhatHoaDonAsync(HoaDon hoaDon)
    {
        var existing = _danhSachHoaDon.FirstOrDefault(h => h.Id == hoaDon.Id);
        if (existing == null) return Task.FromResult(false);

        existing.TongTien = hoaDon.TongTien;
        existing.DaThanhToan = hoaDon.DaThanhToan;
        existing.ConLai = hoaDon.ConLai;
        existing.TrangThai = hoaDon.TrangThai;
        existing.NgayThanhToan = hoaDon.NgayThanhToan;

        return Task.FromResult(true);
    }
}
