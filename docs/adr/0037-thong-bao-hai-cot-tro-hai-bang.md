# 0037 — `HT_ThongBao` có hai cột trỏ hai bảng khác nhau, và đó là bước trung gian

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-09-19
- **Bối cảnh liên quan:** [0034](0034-idtaikhoantheosdt-soi-guong-idbenhnhan.md) ·
  [0036](0036-ho-so-la-cap-nguoi-x-co-so.md)

## Bối cảnh

Trước 1B, `HT_ThongBao` neo cả hai đầu vào `HT_TaiKhoan`: người gửi và người nhận cùng một bảng, và
`CK_HT_ThongBao_KhacNhau` chặn *"gửi cho chính mình"* bằng `IDNguoiGui <> IDNguoiNhan`.

Đợt 1B bỏ tài khoản bệnh nhân. Người **gửi** vẫn là Admin — vẫn nằm ở `HT_TaiKhoan`. Người **nhận** là
bệnh nhân — nay chỉ tồn tại ở `DM_BenhNhan`. Hai đầu rẽ sang hai bảng.

## Quyết định

| Cột | Trỏ tới | Khoá ngoại |
|---|---|---|
| `HT_ThongBao.IDNguoiGui` | `HT_TaiKhoan(ID)` — Admin | `FK_HT_ThongBao_NguoiGui` |
| `HT_ThongBao.IDNguoiNhan` | **`DM_BenhNhan(ID)`** | `FK_HT_ThongBao_NguoiNhan` (mới) |
| `HT_PushDangKy.IDTaiKhoan` → đổi tên **`IDBenhNhan`** | **`DM_BenhNhan(ID)`** | `FK_HT_PushDangKy_BenhNhan` (mới) |

Bỏ `CK_HT_ThongBao_KhacNhau`: so hai ID của **hai bảng khác nhau** là vô nghĩa, và sẽ chặn nhầm khi
hai bảng tình cờ trùng số.

🔴 **Hệ quả bắt buộc: thông báo trở thành THEO CƠ SỞ.** Mỗi dòng `DM_BenhNhan` là một cặp
(người × cơ sở) — ADR 0036 — nên bệnh nhân chỉ thấy thông báo của cơ sở đang đăng nhập. Đây không phải
lựa chọn thiết kế, nó rơi ra từ việc gộp bảng.

## Hệ quả

**Cái giá đã trả, đo được.** Kế hoạch dự tính bỏ 8 tin bệnh nhân gửi + 41 tin gửi đối tác và **giữ 62
tin Admin → bệnh nhân**. Đo thật thì **không một dòng nào trong 62 tin đó tra ngược được**: 4 tài khoản
bệnh nhân nhận tin đều có SĐT thật, nhưng **không số nào xuất hiện trong `DM_BenhNhan.SDT`** — đúng với
sự thật nền *"13.000/13.154 tài khoản là seed giả, SĐT hồ sơ khác SĐT tài khoản"*.

⇒ `HT_ThongBao` **112 → 0 dòng**. `HT_PushDangKy` **29 → 4** (chỉ 4/14 dòng bệnh nhân tra ngược được).
Toàn bộ là dữ liệu thử trên DB dev, nhưng con số phải được ghi ra chứ không giấu.

**Đây là bước trung gian, không phải đích.** Một bảng có hai cột trỏ hai bảng khác nhau là hình dạng
khó đọc và không có khoá ngoại nào diễn tả được *"gửi cho chính mình"* nữa. Người dùng đã nói rõ
*"`HT_ThongBao` về sau sẽ có hướng fix ở Admin"*. Giữ ADR này để đợt sau biết vì sao nó ra nông nỗi
ấy, và biết rằng nó **được phép** thay.

**Đường di trú đã dùng** (ghi lại vì không lặp lại được): tra ngược `HT_TaiKhoan.SDT` → dòng
`DM_BenhNhan` có `SDT` khớp **và `IdCoSo` không NULL**; một SĐT ứng nhiều cơ sở thì *nhân bản* dòng
thông báo cho từng cơ sở, còn *đăng ký push* thì lấy dòng `ID` lớn nhất — push là đăng ký **thiết bị**,
nhân bản là bắn trùng nhiều lần về cùng một máy.
