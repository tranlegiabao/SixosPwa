# 0032 — Một hồ sơ giữ đúng một mã, và đổi mã là một cửa riêng

- **Tác giả:** Nam · **Ngày:** 2026-09-16 · **Trạng thái:** Đề xuất
- **Bối cảnh liên quan:** [0008](0008-moi-duong-ghi-qua-stored-procedure.md) ·
  [0020](0020-tin-cccd-o-loi-vao-chan-o-tang-tai-lieu.md) ·
  [0024](0024-noi-ho-so-tu-dong-hai-tang-va-go-noi.md) ·
  [0028](0028-ho-so-khong-co-can-cuoc-thi-khong-noi-benh-an.md) ·
  [0031](0031-his-tu-dung-ho-so-ben-cong.md)

## Bối cảnh

ADR 0031 mở *Cửa dựng hồ sơ*: HIS tự tạo tài khoản + hồ sơ + nối mã bên cổng. Kèm theo nó là một luật
nghiệp vụ: **một hồ sơ ở một cơ sở giữ đúng một mã bệnh nhân**. Đổi mã ngầm là đổi chủ sở hữu của cả
tập tài liệu — người A sẽ mở ra kết quả xét nghiệm của người B.

Luật đó **đã được cài, nhưng cài nhầm chỗ**: hàng rào nằm ở `S00_UploadOnline` phía **HIS**
(bước 3 và bước 7). Nghĩa là nó chỉ canh **một** đường. Cửa thật sự ghi xuống đĩa là
`DM_BenhNhanCoSo_Save`, và nhánh `ELSE` của nó làm đúng một câu, không kiểm gì:

```sql
UPDATE dbo.DM_BenhNhanCoSo SET MaBN = @MaBN WHERE ID = @IDBenhNhanCoSo;
```

Rà 16/09, có **năm** nơi gọi cửa đó:

| Nơi gọi | Có đổi mã đang có không? |
|---|---|
| `Controllers/HomeController.cs:318` | không — chỉ chạy khi `MaBN` trống |
| `Services/Partner/LuongCongBenhNhan.cs:845` | không — chỉ chạy khi hồ sơ null / `MaBN` trống |
| `Services/Partner/LuongCongBenhNhan.cs:726` | **có** — quét QR có `MaBN` thì gọi thẳng |
| `Services/HoSoBenhNhanService.cs:588` | **có** — chỉ chặn *"mã đã có chủ"*, không chặn *"hồ sơ đã có mã khác"* |
| `Areas/Admin/Controllers/TaiKhoanController.cs:350` | **có, và cố ý** — công cụ của bộ phận hỗ trợ |

Hai dòng giữa là lỗ hổng thật. Dòng cuối **không** phải lỗ hổng: đó là đường duy nhất để sửa một lần
nối sai, và giết nó đi thì mỗi lần khách nối nhầm là phải nhờ người chạy SQL tay trên CSDL thật —
đổi một lỗ hổng lấy một quy trình thủ công nguy hiểm hơn.

## Quyết định

Tách **một câu lệnh** thành **hai ý định**, mỗi ý định một cửa:

- **`DM_BenhNhanCoSo_Save` — lưu hồ sơ.** Điền được vào chỗ **trống**, lưu lại **cùng mã** vẫn OK,
  nhưng **từ chối** đổi một mã đã có sang mã khác: trả `ResultCode = 3` và không ghi gì.
  Từ chối **mềm**, không `THROW` — 4/5 nơi gọi không bắt exception, ném ra là vỡ luồng chạy.
- **`DM_BenhNhanCoSo_DoiMa` — cửa đổi mã.** Làm đúng việc đó, **có ghi sổ** (`HT_LogApiCoSo`), và
  **không đụng `DaMoTaiLieu`** (giữ luật của ADR 0020: không ai được lặng lẽ mở một cửa đã đóng).
  Màn Admin gọi cửa này khi cửa Lưu trả `3`.

🔴 **Và `spwa_his` không được cấp `EXECUTE` trên `_DoiMa`.** Đây mới là phần cốt lõi của quyết định:
hàng rào giữ bằng **quyền**, không giữ bằng quy ước.

## Phương án đã cân nhắc và loại

**Thêm tham số `@ChoPhepDoiMa bit = 0` vào `_Save`, màn Admin truyền `1`.** Rẻ hơn nhiều: ~15 dòng SQL
và 2 dòng C#, không sinh stored mới.

Loại, vì `Database/25_GRANT_LOGIN_HIS.sql` đang cấp `EXECUTE` trên `DM_BenhNhanCoSo_Save` cho login
`spwa_his` (nó là *cửa 4* của Cửa dựng hồ sơ). HIS gọi cửa đó **qua linked server**. Một tham số
cho-phép-bỏ-qua nằm trên chính cửa mà HIS có quyền mở thì HIS chỉ cần truyền thêm một cờ là xuyên
rào — hàng rào thành một lời hứa giữa hai codebase, và lời hứa ấy sẽ bị phá bởi người không đọc ADR
này. Tách cửa thì `GRANT` giữ hộ: HIS **không thể** đổi mã, dù ai đó có muốn.

**Chặn cứng, không cửa thoát nào.** Loại vì giết đường sửa lỗi của bộ phận hỗ trợ (xem Bối cảnh).

## Hệ quả

- Màn Admin *"nhập mã bệnh nhân mới"* **vẫn đổi được mã**, nhưng nay đi qua cửa có ghi sổ ⇒ mỗi lần
  đổi đều truy được ai/khi nào/từ mã nào sang mã nào.
- `LuongCongBenhNhan.cs:726` và `HoSoBenhNhanService.cs:588` **mất khả năng đổi mã ngầm**. Đó là chủ
  đích, không phải tác dụng phụ: hai chỗ đó đang đổi mã mà không ai bảo chúng đổi.
- 🔴 **Thêm `_DoiMa` vào danh sách `GRANT` ở file 25 là phá bỏ chính ADR này.** File
  `Database/28_HANG_RAO_MOT_HO_SO_MOT_MA.sql` kết thúc bằng một truy vấn in ra ai đang được cấp cửa
  đó; kết quả đúng là **không có `spwa_his`**.
- Đường Trỏ đường **không đổi hành vi**: `S00_UploadOnline` vẫn giữ hàng rào của nó ở bước 3. Hai
  hàng rào là cố ý — bên HIS chặn sớm để đỡ một vòng gọi chéo (stored đó đang tối ưu từ ~460 xuống
  ~310 lượt RPC mỗi lần gửi), bên cổng là chốt chặn cuối cho mọi đường khác.
- Ca **"hồ sơ có sẵn nhưng trắng mã"** vẫn điền được bình thường. Nếu hàng rào chặn cả ca này thì
  Cửa dựng hồ sơ gãy ngay — đó chính là ca thật đã vá ngày 16/09.
