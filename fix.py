
import os

file_path = "SixosPwa/Areas/Admin/Models/AdminViewModels.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

bad_string = """    public long Id { get; set;
    public List<PatientAccountStat> PatientAccounts { get; set; } = new(); }"""
good_string = """    public long Id { get; set; }
    public List<PatientAccountStat> PatientAccounts { get; set; } = new();"""

content = content.replace(bad_string, good_string)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

