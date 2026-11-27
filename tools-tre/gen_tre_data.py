"""Regenerate PKHeX.Core/Saves/TRE/tre_data.json from a patched English TRE ROM.
usage: python gen_tre_data.py <english.gba> <out.json>
Needs g3text.py (Gen 3 text codec) from the translation repo's tools/ on the path."""
import sys, json, struct
from g3text import decode
rom = open(sys.argv[1], 'rb').read()
u16 = lambda o: struct.unpack_from('<H', rom, o)[0]
u32 = lambda o: struct.unpack_from('<I', rom, o)[0]
def text_at_ptr(o):
    p = u32(o)
    return (decode(rom, p - 0x08000000, 40)[0] or '') if 0x08000000 <= p < 0x0A000000 else ''
SP, SP_N, SP_W = 0x9758c0, 1698, 0xA8
species = []
for i in range(SP_N):
    b = SP + i * SP_W
    species.append({'id': i, 'name': (decode(rom, b + 44, 20)[0] or '').strip(), 'nat': u16(b + 60),
        'stats': list(rom[b:b + 6]), 'types': [rom[b + 6], rom[b + 7]], 'catch': rom[b + 8],
        'exp': u16(b + 10), 'ev': [(u16(b + 12) >> (2 * k)) & 3 for k in range(6)],
        'items': [u16(b + 14), u16(b + 16)], 'gender': rom[b + 18], 'egg': rom[b + 19],
        'fr': rom[b + 20], 'growth': rom[b + 21], 'eggs': [rom[b + 22], rom[b + 23]],
        'abil': [u16(b + 24), u16(b + 26), u16(b + 28)]})
def ptr_at(o):
    p = u32(o)
    return p - 0x08000000 if 0x08000000 <= p < 0x0A000000 else None
def u16_list(o, step=2, limit=400):
    out = []
    while o is not None and u16(o) != 0xFFFF and len(out) < limit:
        out.append(o); o += step
    return out
for e in species:  # learnsets + evolutions (pointers inside the species struct)
    b = SP + e['id'] * SP_W
    e['lv'] = [x for o in u16_list(ptr_at(b + 144), 4) for x in (u16(o), u16(o + 2))]  # move, level
    e['tm'] = [u16(o) for o in u16_list(ptr_at(b + 148))]                               # TMs + tutors
    e['eggm'] = [u16(o) for o in u16_list(ptr_at(b + 152))]
    e['evo'] = [u16(o + 4) for o in u16_list(ptr_at(b + 156), 12, 20)]                  # target raw species
def title(s):
    import re
    s = re.sub(r'\{[^}]*\}\s*', '', s).replace('[PK][MN]', 'POKéMON')
    return ' '.join(w.capitalize() for w in s.split(' ')).replace("'S", "'s").replace('S.s.', 'S.S.').replace('S.a.', 'S.A.').replace(' Of ', ' of ')
MS = 0xe6f864  # region map sections: x, y, w, h, name*
locations = []
while (p := ptr_at(MS + len(locations) * 8 + 4)) is not None and len(locations) < 1000:
    locations.append(title((decode(rom, p, 40)[0] or '').strip()))
items = [text_at_ptr(0x8d1800 + i * 44) for i in range(1049)]
tm_moves = [u16(0x8d041c + 2 * i) for i in range(254)]  # TM01-TM246, then HM01-HM08 (hack move IDs)
item_pockets = [rom[0x8d1800 + i * 44 + 10] >> 3 for i in range(1049)]  # 0 items, 1 medicine, 2 TM/HM, 3 key, 4 balls, 5 held, 6 berries, 7 mega
moves = [text_at_ptr(0x939a4c + i * 60) for i in range(935)]
abilities = [decode(rom, 0xe65b68 + i * 0x1C, 0x1C)[0] for i in range(320)]
json.dump({'species': species, 'items': items, 'moves': moves, 'abilities': abilities, 'locations': locations, 'item_pockets': item_pockets, 'tm_moves': tm_moves},
          open(sys.argv[2], 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
