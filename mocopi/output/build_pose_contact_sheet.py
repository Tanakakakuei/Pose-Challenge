import json
import base64
import io
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageStat

root = Path(__file__).resolve().parent
folder = Path((root / 'random-pose-capture-latest.txt').read_text(encoding='utf-8-sig').strip())
report = json.loads((folder / 'manifest.json').read_text(encoding='utf-8-sig'))
captures = report['captures']
assert report['status'] == 'complete' and len(captures) == 30
assert len({''.join(c['expandedRows']) for c in captures}) == 30
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 18)
small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 15)
title = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', 30)
sheet = Image.new('RGB', (1600, 2370), '#101c2b')
draw = ImageDraw.Draw(sheet)
draw.text((24, 20), '30 RANDOM POSES / CAPTURE MODE', font=title, fill='#edf4ff')
draw.text((24, 64), '6m x 3m wall  |  Random horizontal placement  |  Green = added 5cm margin', font=font, fill='#a7e8bb')
for i, c in enumerate(captures):
    saved = json.loads(Path(c['savedPath']).read_text(encoding='utf-8-sig'))
    copy = json.loads((folder / c['file']).read_text(encoding='utf-8-sig'))
    assert saved == copy and saved['rows'] == c['expandedRows']
    raw, expanded = c['originalRows'], c['expandedRows']
    assert len(expanded) == 30 and all(len(row) == 60 for row in expanded)
    assert c['expandedHoleCells'] > c['originalHoleCells']
    assert all(e == '0' for a, e in zip(''.join(raw), ''.join(expanded)) if a == '0')
    assert '0' not in expanded[0] and all(row[0] == row[-1] == '1' for row in expanded)
    x, y = 16 + (i % 5) * 316, 110 + (i // 5) * 374
    draw.rounded_rectangle((x, y, x + 303, y + 360), 8, fill='#1a2b40')
    draw.text((x + 10, y + 8), f'{i+1:02d}   X = {c["horizontalOffset"]:+.2f} m', font=font, fill='#edf4ff')
    thumbnail = Image.open(folder / c['image']).convert('RGB')
    assert max(ImageStat.Stat(thumbnail).stddev) > 5, 'Empty pose thumbnail'
    thumbnail = thumbnail.resize((144, 180), Image.Resampling.LANCZOS)
    sheet.paste(thumbnail, (x + 80, y + 34))
    mask = Image.new('RGB', (60, 30))
    for row in range(30):
        for col in range(60):
            color = '#929bc2' if expanded[row][col] == '1' else '#9fffad' if raw[row][col] == '1' else '#0a1320'
            mask.putpixel((col, row), tuple(bytes.fromhex(color[1:])))
    sheet.paste(mask.resize((288, 144), Image.Resampling.NEAREST), (x + 8, y + 214))
png = io.BytesIO()
sheet.save(png, format='PNG')
summary = {'count': len(captures), 'distinct': 30, 'dimensions': [30, 60], 'marginMeters': report['marginMeters'],
           'minHorizontalOffset': min(c['horizontalOffset'] for c in captures),
           'maxHorizontalOffset': max(c['horizontalOffset'] for c in captures),
           'savedCopiesMatch': True, 'expansionContainsOriginal': True, 'topAndSidesClear': True,
           'allPoseImagesRendered': True}
print(json.dumps({'summary': summary, 'png': base64.b64encode(png.getvalue()).decode('ascii')}))
