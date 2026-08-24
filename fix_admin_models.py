import re

file_path = "SixosPwa/Areas/Admin/Models/AdminViewModels.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

new_classes = """
public sealed class FacilityGroupStat
{
    public string TenLoaiCS { get; set; } = "";
    public string LoaiCS { get; set; } = "";
    public int FacilityCount => Facilities.Count;
    public int TotalPatientCount => Facilities.Sum(x => x.PatientCount);
    public List<FacilityStat> Facilities { get; set; } = new();
}

public sealed class FacilityStat
{
    public long Id { get; set; }
    public string MaCoSo { get; set; } = "";
    public string TenCoSo { get; set; } = "";
    public int PatientCount { get; set; }
}
"""

if "FacilityGroupStat" not in content:
    content = content.strip() + "\n" + new_classes + "\n"

prop_to_add = "    public IReadOnlyList<FacilityGroupStat> FacilityGroupStats { get; init; } = Array.Empty<FacilityGroupStat>();"
if prop_to_add not in content:
    content = re.sub(r'(public sealed class DashboardViewModel\s*{[^}]+?)(?=\s*})', r'\1\n' + prop_to_add, content)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

