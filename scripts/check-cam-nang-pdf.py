from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root / '.tmp' / 'pydeps'))
import fitz

book = root / 'docs' / 'cam-nang-du-an' / 'Cam-nang-ky-thuat-He-thong-Quan-ly-Du-an-Scrum-tich-hop-AI.pdf'
preview = root / '.tmp' / 'cam-nang-pdf-preview'
preview.mkdir(parents=True, exist_ok=True)
doc = fitz.open(book)
print('pages', len(doc))
needles = ['Mục lục', 'Phần I.', 'Phần II.', 'Phần III.', 'Phần IV.', 'Phần V.', 'Hình 8.3', 'Phụ lục C.']
selected = {0, min(1, len(doc)-1), min(15, len(doc)-1), min(16, len(doc)-1), min(17, len(doc)-1), min(18, len(doc)-1), min(26, len(doc)-1), min(31, len(doc)-1), len(doc)-1}
for needle in needles:
    matches = [i for i, p in enumerate(doc) if needle in p.get_text()]
    print(needle, [i+1 for i in matches[:4]])
    if matches:
        selected.add(matches[0])
for i in sorted(selected):
    page = doc[i]
    page.get_pixmap(matrix=fitz.Matrix(1.25, 1.25), alpha=False).save(preview / f'page-{i+1:03}.png')
    print(f'page {i+1}: {len(page.get_text())} chars; size {page.rect.width:.0f}x{page.rect.height:.0f}')
print('preview', preview)
