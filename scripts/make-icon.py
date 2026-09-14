from pathlib import Path
from PIL import Image, ImageDraw

out = Path(__file__).resolve().parent.parent / 'Assets'
out.mkdir(exist_ok=True)
im = Image.new('RGBA', (256, 256), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
d.rounded_rectangle((12, 18, 244, 244), radius=48, fill='#283544')
d.rounded_rectangle((32, 48, 224, 218), radius=22, fill='#A8D9EF')
d.rectangle((32, 90, 224, 108), fill='#283544')
for x in (78, 178):
    d.rounded_rectangle((x-10, 24, x+10, 70), radius=10, fill='#F2F6FA')
for x in (64, 112, 160):
    for y in (128, 172):
        d.rounded_rectangle((x, y, x+28, y+24), radius=6, fill='#283544')
d.rounded_rectangle((160, 172, 188, 196), radius=6, fill='#579EAF')
im.save(out / 'calendar.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
