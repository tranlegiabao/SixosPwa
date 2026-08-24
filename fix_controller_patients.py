
import re

file_path = "SixosPwa/Areas/Admin/Controllers/DashboardController.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# Replace taiKhoanCounts section
old_code = """        var taiKhoanCounts = await _db.TaiKhoanDoiTacs
            .GroupBy(x => x.MaCoSo)
            .Select(g => new { MaCoSo = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MaCoSo, x => x.Count);"""

new_code = """        var facilityPatients = await _db.TaiKhoanDoiTacs
            .Join(_db.TaiKhoans, 
                  td => td.IdTaiKhoan, 
                  tk => tk.Id, 
                  (td, tk) => new { td.MaCoSo, tk.SDT, tk.Id, tk.CCCD })
            .ToListAsync();
            
        var patientsByFacility = facilityPatients
            .GroupBy(x => x.MaCoSo)
            .ToDictionary(
                g => g.Key, 
                g => g.Select(x => new PatientAccountStat { Id = x.Id, SDT = x.SDT ?? "", CCCD = x.CCCD ?? "" }).ToList()
            );"""

content = content.replace(old_code, new_code)

old_stat = """PatientCount = taiKhoanCounts.GetValueOrDefault(f.MaCoSo ?? "", 0)"""
new_stat = """PatientCount = patientsByFacility.GetValueOrDefault(f.MaCoSo ?? "", new List<PatientAccountStat>()).Count,
                        PatientAccounts = patientsByFacility.GetValueOrDefault(f.MaCoSo ?? "", new List<PatientAccountStat>())"""

content = content.replace(old_stat, new_stat)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

