
import os
file_path = "SixosPwa/Areas/Admin/Views/Dashboard/Index.cshtml"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

with open(file_path, "w", encoding="utf-8-sig") as f:
    f.write(content)

