#!/usr/bin/env python3
"""Offline FBX vertex-bounds probe.

Reads the raw vertex coordinates straight out of a binary FBX (no Unity
required) and prints the bounding-box size of the mesh, so ModelRawHeight in
ModelLoader.cs can be re-measured when a pack changes.

    python3 Tools/fbx_probe.py Assets/Resources/Models/Quaternius/Monsters/Dragon.fbx

Validated against the values already recorded in ModelLoader.ModelRawHeight:
the reported Y size matches Unity's imported bounds for the CuteMonsters /
Quaternius packs.
"""
import struct
import sys
import zlib

ELEM_SIZE = {b'f': 4, b'd': 8, b'l': 8, b'i': 4, b'b': 1}


def read_vertices(path):
    data = open(path, 'rb').read()
    if not data.startswith(b'Kaydara FBX Binary'):
        raise SystemExit('not a binary FBX: ' + path)
    big = struct.unpack_from('<I', data, 23)[0] >= 7500
    hdr = 25 if big else 13
    found = []

    def node(off):
        if big:
            end, num, plen = struct.unpack_from('<QQQ', data, off)
            nl = data[off + 24]
        else:
            end, num, plen = struct.unpack_from('<III', data, off)
            nl = data[off + 12]
        name = data[off + hdr:off + hdr + nl].decode('latin1')
        return end, num, name, off + hdr + nl

    def walk(off, end):
        term = b'\x00' * hdr
        while off < end:
            if data[off:off + len(term)] == term:
                break
            nend, num, name, p = node(off)
            cp = p
            for _ in range(num):
                t = data[cp:cp + 1]
                if t in ELEM_SIZE:
                    # FBX array record: element count, encoding (0 raw / 1 zlib),
                    # then the length of the payload on disk.
                    count, enc, payload = struct.unpack_from('<III', data, cp + 1)
                    body = data[cp + 13:cp + 13 + payload]
                    if t in (b'f', b'd') and name == 'Vertices':
                        if enc == 0:
                            body = body[:count * ELEM_SIZE[t]]
                        else:
                            body = zlib.decompress(body)
                        found.append(struct.unpack('<%d%s' % (count, t.decode()), body))
                    cp += 13 + payload
                elif t in (b'Y', b'C'):
                    cp += 2
                elif t in (b'I', b'F'):
                    cp += 5
                elif t in (b'D', b'L'):
                    cp += 9
                elif t in (b'S', b'R'):
                    cp += 5 + struct.unpack_from('<I', data, cp + 1)[0]
                else:
                    return
            walk(cp, nend)
            off = nend

    walk(27, len(data))
    return found


def main():
    heights = []
    for path in sys.argv[1:]:
        arrs = read_vertices(path)
        xs = [v for a in arrs for v in a[0::3]]
        ys = [v for a in arrs for v in a[1::3]]
        zs = [v for a in arrs for v in a[2::3]]
        if not ys:
            print('%s: no vertices' % path)
            continue
        h = max(ys) - min(ys)
        w = max(xs) - min(xs)
        d = max(zs) - min(zs)
        print('%s  X=%.2f Y=%.2f Z=%.2f  (meshes=%d)' % (path, w, h, d, len(arrs)))
        heights.append(h)
    return heights


if __name__ == '__main__':
    main()
