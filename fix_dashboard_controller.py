import re

file_path = "SixosPwa/Areas/Admin/Controllers/DashboardController.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# Add TaiKhoanDoiTacs and Groups
addition = """
        // Count of patient accounts by MaCoSo
        var taiKhoanCounts = await _db.TaiKhoanDoiTacs
            .GroupBy(x => x.MaCoSo)
            .Select(g => new { MaCoSo = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MaCoSo, x => x.Count);

        var groups = nhomCSList
            .Where(x => new[] { "benhvien", "nhakhoa", "pkdk", "nhathuoc" }.Contains(x.LoaiCS?.ToLower()))
            .Select(n => new FacilityGroupStat
            {
                TenLoaiCS = n.TenLoaiCS ?? "",
                LoaiCS = n.LoaiCS ?? "",
                Facilities = facilityList
                    .Where(f => string.Equals(f.LoaiCS, n.LoaiCS, StringComparison.OrdinalIgnoreCase))
                    .Select(f => new FacilityStat
                    {
                        Id = f.Id,
                        MaCoSo = f.MaCoSo ?? "",
                        TenCoSo = f.TenCoSo ?? "",
                        PatientCount = taiKhoanCounts.GetValueOrDefault(f.MaCoSo ?? "", 0)
                    })
                    .ToList()
            })
            .ToList();
"""

if "FacilityGroupStat" not in content:
    # Insert before "var model = new DashboardViewModel"
    content = content.replace("var model = new DashboardViewModel", addition + "\n        var model = new DashboardViewModel")
    
    # Add FacilityGroupStats = groups inside DashboardViewModel initialization
    content = content.replace("NoiDung = noiDung", "NoiDung = noiDung,\n            FacilityGroupStats = groups")
    
with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

