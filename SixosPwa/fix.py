import re
path = r'D:\web\SixosPwaTemplate\SixosPwaTemplate\SixosPwa\wwwroot\css\thong-tin-benh-nhan.css'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()
content = re.sub(r'(\.ytv-dropdown a \{[^\}]*text-decoration:\s*none;)', r'\1\n        white-space: nowrap;', content)
with open(path, 'w', encoding='utf-8') as f:
    f.write(content)
