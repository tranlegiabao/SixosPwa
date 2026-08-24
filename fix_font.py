import os

file_path = "SixosPwa/Areas/Admin/Views/Dashboard/Index.cshtml"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace('<h5 class="fw-bold mb-3 text-primary">', '<div class="fw-bold mb-3 text-primary" style="font-size: 18px; text-transform: uppercase;">')
content = content.replace('</h5>', '</div>')
content = content.replace('<div class="d-flex justify-content-between text-muted small">', '<div class="d-flex justify-content-between text-secondary" style="font-size: 15px; font-weight: 500;">')

content = content.replace('<h5 class="fw-bold text-secondary mb-0">', '<div class="fw-bold text-secondary mb-0" style="font-size: 16px;">')

content = content.replace('<span class="fw-bold text-dark text-truncate me-2"', '<span class="fw-semibold text-dark text-truncate me-2" style="font-size: 15px;"')

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

