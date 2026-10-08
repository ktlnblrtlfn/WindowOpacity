"""Generate WindowOpacity's original icon using only the Python standard library."""
from pathlib import Path
import struct

size = 32
pixels = bytearray()
for y in range(size - 1, -1, -1):
    for x in range(size):
        inside = (x - 15.5) ** 2 + (y - 15.5) ** 2 < 13 ** 2
        blue, green, red = (212, 120, 0) if x < 16 else (245, 230, 208)
        pixels += bytes((blue, green, red, 255 if inside else 0))
dib = struct.pack('<IIIHHIIIIII', 40, size, size * 2, 1, 32, 0, len(pixels), 0, 0, 0, 0)
dib += pixels + bytes(4 * size)
icon = struct.pack('<HHH', 0, 1, 1)
icon += struct.pack('<BBBBHHII', size, size, 0, 0, 1, 32, len(dib), 22) + dib
path = Path(__file__).resolve().parents[1] / 'WindowOpacity' / 'Assets' / 'WindowOpacity.ico'
path.parent.mkdir(parents=True, exist_ok=True)
path.write_bytes(icon)
