# Mọi đường ghi đi qua stored procedure, EF chỉ còn đọc

Trước đợt 2026-08-24, `HIS_CSKH` có **hai** lối ghi song song vào cùng những bảng: khu Admin ghi qua 5
thủ tục `Admin_*_Save`, phần cổng bệnh nhân ghi qua EF (`SaveChanges` ở 12 chỗ). Hai lối không thi hành
cùng một bộ luật.

Bằng chứng cụ thể: `Admin_CoSoYTe_Save` tự kiểm trùng `MaCoSo` rồi trả `ResultCode = 2`, nhưng `DMCSKCB`
**không hề có ràng buộc duy nhất ở DB** — nên mọi đường ghi qua EF vẫn tạo trùng được như thường. Ràng
buộc chỉ tồn tại ở một trong hai cửa.

Từ đợt này: **mọi thao tác ghi đi qua stored procedure**, EF chỉ dùng để đọc. Hợp với khuôn HisSoft —
12 DB khách đều nặng stored procedure, người trong nhà đọc là hiểu ngay.

Đồng thời, những gì là *toàn vẹn dữ liệu* được đẩy xuống thành ràng buộc thật ở DB (`UNIQUE`, khoá ngoại,
`CHECK`) thay vì sống trong thân thủ tục — chỗ duy nhất không đường nào lách được.

## Consequences

- Nguồn stored procedure **nằm ngoài git**, ở `.claude/prompts/2026-08-24_audit-redesign-his-cskh/sql/procedures/`
  (quyết định của người dùng). Nghĩa là nghiệp vụ ghi của hệ thống **không có lịch sử thay đổi và không
  review được qua PR**. Đây là cái giá đã biết trước, không phải sơ suất.
- Việc chuẩn hoá làm **biến mất code**, không chỉ làm đẹp schema: 40/112 dòng của `Admin_CoSoYTe_Save`
  chỉ tồn tại để lan `TenCoSo` sang hai bảng khác; toàn bộ `@LegacyLoaiND` trong `Admin_NDCSKCB_Save`
  chỉ tồn tại vì một cột chứa hai cách mã hoá. Bỏ lặp là bỏ luôn cả hai khối.
- Kiểm tra "đã xong chưa" là một lệnh đo được:
  `grep -rn "SaveChangesAsync\|SaveChanges()" SixosPwa/ --include="*.cs"` phải trả về **0**.
- Đổi lại, test tự động khó hơn: logic ghi không còn nằm trong code C# để viết unit test. Bù bằng lớp
  kiểm chứng SQL trong `V008` và nghiệm thu Playwright ở Đợt 3.
