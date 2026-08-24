
import re

file_path = "SixosPwa/Areas/Admin/Models/AdminViewModels.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

patient_stat_class = """
public sealed class PatientAccountStat
{
    public long Id { get; set; }
    public string SDT { get; set; } = "";
    public string CCCD { get; set; } = "";
}
"""

if "PatientAccountStat" not in content:
    content += "\n" + patient_stat_class + "\n"

# Add PatientAccounts to FacilityStat
prop_to_add = "    public List<PatientAccountStat> PatientAccounts { get; set; } = new();"
if "PatientAccounts" not in content:
    content = re.sub(r'(public sealed class FacilityStat\s*{[^}]+?)(?=\s*})', r'\1\n' + prop_to_add, content)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

