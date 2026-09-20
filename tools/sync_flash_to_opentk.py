#!/usr/bin/env python3
"""Sync classic (Flash) client art + data into the OpenTK client safely.

Flash source : realm-client/src/kabam/rotmg/assets/
                 EmbeddedAssets_<name>Embed_.png  ->  Content/Sheets/<name>.png
                 EmbeddedData_<name>CXML.dat      ->  Content/Xmls/<name>.xml
OpenTK target: realm-opentk/AlloyClient/Content/

What is safe (and all this script will do under `apply`):
  * PNGs are copied ONLY when pixel dimensions are exactly equal, so every
    <Index> in every XML keeps pointing at the same grid cell.
    Sheets the OpenTK client extended (taller/wider) are SKIPPED and reported
    for manual merging -- truncating them would delete sprites.
  * XML entries are APPENDED (never overwritten) and only when the entry's
    (kind, type) is absent from the whole OpenTK Xmls/ tree AND present in
    the server's GameData. Same-type/different-id conflicts (type reuse,
    renames) are reported, never merged.

Usage:
  sync_flash_to_opentk.py check      # report only (default)
  sync_flash_to_opentk.py apply      # safe copies + backfill, then verify
  sync_flash_to_opentk.py full-port  # DESTRUCTIVE: flash bytes win everywhere,
                                     # OpenTK-only sheets are truncated and
                                     # OpenTK-only Xmls are deleted. Then verify.
  sync_flash_to_opentk.py verify     # OpenTK self-consistency check only
"""

import argparse
import os
import re
import shutil
import struct
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
OT_ROOT = os.path.abspath(os.path.join(HERE, ".."))
WS_ROOT = os.path.abspath(os.path.join(OT_ROOT, ".."))


def png_size(path):
    with open(path, "rb") as f:
        sig = f.read(8)
        if sig != b"\x89PNG\r\n\x1a\n":
            raise ValueError("not a PNG: %s" % path)
        while True:
            hdr = f.read(8)
            if len(hdr) < 8:
                raise ValueError("no IHDR: %s" % path)
            (length, ctype) = struct.unpack(">I4s", hdr)
            data = f.read(length + 4)
            if ctype == b"IHDR":
                (w, h) = struct.unpack(">II", data[:8])
                return (w, h)


def flash_core(filename, kind):
    """EmbeddedAssets_lofiObj3Embed_.png -> lofiObj3 ; handles quirk names."""
    assert filename.startswith("EmbeddedAssets_")
    core = filename[len("EmbeddedAssets_"):]
    suffix = "Embed_.%s" % kind
    if core.endswith(suffix):
        core = core[: -len(suffix)]
    elif kind == "png" and core.endswith("_.png"):
        core = core[: -len("_.png")]  # e.g. ...KaratePenguin_.png
    elif "." in core:
        core = core[: core.rfind(".")]
    return core.rstrip("_")


def collect_flash_pngs(flash_dir):
    out = []
    for f in sorted(os.listdir(flash_dir)):
        if f.startswith("EmbeddedAssets_") and f.endswith(".png"):
            out.append((f, flash_core(f, "png")))
    return out


def collect_flash_dats(flash_dir):
    return sorted(f for f in os.listdir(flash_dir)
                  if f.startswith("EmbeddedData_") and f.endswith("CXML.dat"))


def index_types(paths):
    """(kind, type) -> set(ids); kinds: Object, Ground, Region."""
    d = {}
    for p in paths:
        with open(p, "rb") as f:
            txt = f.read().decode("utf-8", errors="replace")
        for m in re.finditer(r'<(Object|Ground|Region)\s+([^>]*?)>', txt):
            (kind, attrs) = m.groups()
            t = re.search(r'type="([^"]+)"', attrs)
            i = re.search(r'id="([^"]+)"', attrs)
            if t and i:
                d.setdefault((kind, t.group(1)), set()).add(i.group(1))
    return d


def dat_to_xml_name(dat):
    return dat[len("EmbeddedData_"): -len("CXML.dat")] + ".xml"


ROOT_CLOSE = {b"<Objects>": b"</Objects>", b"<GroundTypes>": b"</GroundTypes>",
              b"<Regions>": b"</Regions>"}


def parse_atlas(atlas_path):
    """name -> (file, w, h or None)."""
    txt = open(atlas_path, encoding="utf-8-sig").read()
    entries = {}
    for m in re.finditer(r'<(Image|Animated)\s+name="([^"]+)"([^>]*)>([^<]+)</\1>', txt):
        (tag, name, attrs, src) = m.groups()
        w = re.search(r'w="(\d+)"', attrs)
        h = re.search(r'h="(\d+)"', attrs)
        entries[name] = (src.strip(), int(w.group(1)) if w else None,
                         int(h.group(1)) if h else None)
    return entries


def element_bytes(raw, kind, typeid):
    """Extract full <Kind ...type="T"...>...</Kind> element bytes, or None."""
    pat = re.compile(
        rb'<%s\b[^>]*\btype="%s"[^>]*>.*?</%s>' % (kind.encode(), typeid.encode(), kind.encode()),
        re.DOTALL)
    hits = pat.findall(raw)
    return hits[0] if len(hits) == 1 else None


def check_pngs(flash_dir, sheets_dir):
    ot = {f.lower(): f for f in os.listdir(sheets_dir)}
    rows = []
    for (ffile, core) in collect_flash_pngs(flash_dir):
        match = ot.get((core + ".png").lower())
        if match is None:
            rows.append((core, None, "NO-MATCH"))
            continue
        fa = png_size(os.path.join(flash_dir, ffile))
        fb = png_size(os.path.join(sheets_dir, match))
        verdict = "SAFE-COPY" if fa == fb else "SKIP-SIZE-MISMATCH"
        rows.append((core, (match, fa, fb), verdict))
    return rows


def check_xmls(flash_dir, xmls_dir, server_dir):
    import glob
    flash_paths = [os.path.join(flash_dir, f) for f in collect_flash_dats(flash_dir)]
    ot_paths = glob.glob(os.path.join(xmls_dir, "*.xml"))
    sv_paths = glob.glob(os.path.join(server_dir, "*.xml"))
    ot = index_types(ot_paths)
    sv = index_types(sv_paths)
    fl = index_types(flash_paths)
    absent, conflict, stale = [], [], []
    seen = set()
    for p in flash_paths:
        with open(p, "rb") as f:
            raw = f.read()
        txt = raw.decode("utf-8", errors="replace")
        for m in re.finditer(r'<(Object|Ground|Region)\s+([^>]*?)>', txt):
            (kind, attrs) = m.groups()
            t = re.search(r'type="([^"]+)"', attrs)
            i = re.search(r'id="([^"]+)"', attrs)
            if not (t and i) or (kind, t.group(1)) in seen:
                continue
            seen.add((kind, t.group(1)))
            key = (kind, t.group(1))
            if key not in ot and key in sv:
                absent.append((os.path.basename(p), kind, t.group(1), i.group(1)))
            elif key not in ot:
                stale.append((os.path.basename(p), kind, t.group(1), i.group(1)))
            elif i.group(1) not in ot[key]:
                conflict.append((os.path.basename(p), kind, t.group(1), i.group(1),
                                 sorted(ot[key])[:3]))
    return (absent, conflict, stale)


def verify_client(content_dir):
    """Self-consistency: xml parses, atlas files exist, File/Index refs valid."""
    errors = []
    sheets_dir = os.path.join(content_dir, "Sheets")
    xmls_dir = os.path.join(content_dir, "Xmls")
    atlas = parse_atlas(os.path.join(content_dir, "Game.atlas"))
    sheet_dims = {}
    for (name, (src, _w, _h)) in atlas.items():
        p = os.path.join(sheets_dir, src)
        if not os.path.isfile(p):
            errors.append("atlas %s -> missing sheet %s" % (name, src))
            continue
        try:
            sheet_dims[name] = png_size(p)
        except ValueError as e:
            errors.append(str(e))
    n_refs = 0
    for f in sorted(os.listdir(xmls_dir)):
        if not f.endswith(".xml"):
            continue
        p = os.path.join(xmls_dir, f)
        try:
            ET.parse(p)
        except ET.ParseError as e:
            errors.append("%s: XML parse error: %s" % (f, e))
            continue
        txt = open(p, encoding="utf-8", errors="replace").read()
        for m in re.finditer(r'<File>([^<]+)</File>\s*<Index>([^<]+)</Index>', txt):
            (fname, idx) = (m.group(1).strip(), m.group(2).strip())
            n_refs += 1
            if fname not in atlas:
                errors.append("%s: unknown File '%s'" % (f, fname))
                continue
            (src, w, h) = atlas[fname]
            if w is None or fname not in sheet_dims:
                continue
            (W, H) = sheet_dims[fname]
            if W % w or H % h:
                errors.append("%s: sheet %s %dx%d not divisible by %dx%d"
                              % (f, src, W, H, w, h))
                continue
            # Mirror Alloy.Common.Utils.GetBase: hex iff it contains a letter
            # (other than a trailing 'x'); e.g. '05' is decimal 5.
            base = 16 if (re.search(r"[a-zA-Z]", idx) and not idx.endswith("x")) else 10
            try:
                n = int(idx, base)
            except ValueError:
                errors.append("%s: bad Index '%s' for %s" % (f, idx, fname))
                continue
            cap = (W // w) * (H // h)
            if not (0 <= n < cap):
                errors.append("%s: Index %s out of range for %s (cap %d)"
                              % (f, idx, fname, cap))
    return (n_refs, errors)


def cmd_check(flash_dir, content_dir, server_dir):
    sheets_dir = os.path.join(content_dir, "Sheets")
    xmls_dir = os.path.join(content_dir, "Xmls")
    print("== PNGs: flash -> Sheets ==")
    rows = check_pngs(flash_dir, sheets_dir)
    for row in rows:
        if row[1] is None:
            print("  %-32s NO MATCH in Sheets" % row[0])
        else:
            (match, fa, fb, ) = row[1]
            print("  %-32s flash=%-12s ot=%-12s %s"
                  % (row[0], "%dx%d" % fa, "%dx%d" % fb, row[2]))
    print("== XMLs: flash vs OpenTK vs server ==")
    (absent, conflict, stale) = check_xmls(flash_dir, xmls_dir, server_dir)
    print("  backfill candidates (in server+flash, absent in OpenTK): %d" % len(absent))
    for (src, kind, t, i) in absent:
        print("    %s %s %s <- %s" % (t, i, kind, src))
    print("  same-type/different-id conflicts (never auto-merged): %d" % len(conflict))
    for (src, kind, t, i, o) in conflict:
        print("    %s flash=%r ot=%s" % (t, i, o))
    print("  flash-only stale entries (not in server, skipped): %d" % len(stale))
    print("== OpenTK self-consistency ==")
    (n_refs, errors) = verify_client(content_dir)
    print("  texture refs checked: %d, errors: %d" % (n_refs, len(errors)))
    for e in errors[:20]:
        print("  ERROR: %s" % e)
    return (rows, absent, conflict)


def cmd_apply(flash_dir, content_dir, server_dir):
    sheets_dir = os.path.join(content_dir, "Sheets")
    xmls_dir = os.path.join(content_dir, "Xmls")
    (rows, absent, conflict) = cmd_check(flash_dir, content_dir, server_dir)
    print("== APPLY: safe PNG copies ==")
    copied = skipped = 0
    for (core, info, verdict) in rows:
        if verdict != "SAFE-COPY":
            skipped += 1
            continue
        (match, _fa, _fb) = info
        src = os.path.join(flash_dir, "EmbeddedAssets_" + core + "Embed_.png")
        if not os.path.isfile(src):  # quirk names without Embed_ infix
            cands = [os.path.join(flash_dir, f)
                     for (f, c) in collect_flash_pngs(flash_dir) if c == core]
            src = cands[0]
        shutil.copyfile(src, os.path.join(sheets_dir, match))
        copied += 1
    print("  copied %d, skipped %d" % (copied, skipped))
    print("== APPLY: XML backfill ==")
    merged = 0
    for (src, kind, t, i) in absent:
        raw = open(os.path.join(flash_dir, src), "rb").read()
        elem = element_bytes(raw, kind, t)
        dest = os.path.join(xmls_dir, dat_to_xml_name(src))
        if elem is None or not os.path.isfile(dest):
            print("  SKIP %s %s (%s)" % (t, i, "ambiguous element" if elem is None else "no dest file"))
            continue
        try:
            elem.decode("ascii")
        except UnicodeDecodeError:
            print("  SKIP %s %s (non-ascii bytes)" % (t, i))
            continue
        with open(dest, "rb") as f:
            d = f.read()
        close = None
        for (root, c) in ROOT_CLOSE.items():
            if root in d:
                close = c
                break
        if close is None or (b'type="' + t.encode() + b'"') in d:
            print("  SKIP %s %s (no root close tag or already present)" % (t, i))
            continue
        idx = d.rfind(close)
        d = d[:idx] + b"    " + elem + b"\n" + d[idx:]
        with open(dest, "wb") as f:
            f.write(d)
        merged += 1
    print("  merged %d entries" % merged)
    print("== POST-APPLY verify ==")
    (n_refs, errors) = verify_client(content_dir)
    print("  texture refs checked: %d, errors: %d" % (n_refs, len(errors)))
    for e in errors[:20]:
        print("  ERROR: %s" % e)
    return 1 if errors else 0


def cmd_full_port(flash_dir, content_dir, server_dir):
    import glob
    sheets_dir = os.path.join(content_dir, "Sheets")
    xmls_dir = os.path.join(content_dir, "Xmls")
    ot = {f.lower(): f for f in os.listdir(sheets_dir)}

    print("== FULL-PORT: overwrite every overlapping sheet ==")
    copied = 0
    for (ffile, core) in collect_flash_pngs(flash_dir):
        match = ot.get((core + ".png").lower())
        if match is None:
            print("  NO-MATCH (no OpenTK sheet, cannot place): %s" % core)
            continue
        fa = png_size(os.path.join(flash_dir, ffile))
        fb = png_size(os.path.join(sheets_dir, match))
        shutil.copyfile(os.path.join(flash_dir, ffile),
                        os.path.join(sheets_dir, match))
        copied += 1
        if fa != fb:
            print("  TRUNCATED %-28s %dx%d -> %dx%d" % (match, fb[0], fb[1], fa[0], fa[1]))
    print("  overwrote %d sheets (OpenTK-only sheets left in place)" % copied)

    print("== FULL-PORT: replace Xmls with flash dats ==")
    dats = collect_flash_dats(flash_dir)
    blobs = {}
    for dat in dats:  # validate everything before writing anything
        with open(os.path.join(flash_dir, dat), "rb") as f:
            raw = f.read()
        raw.decode("utf-8")  # flash dats must already be valid UTF-8/ASCII
        blobs[dat] = raw
    wanted = set()
    replaced = 0
    for (dat, raw) in blobs.items():
        name = dat_to_xml_name(dat)
        wanted.add(name)
        with open(os.path.join(xmls_dir, name), "wb") as f:
            f.write(raw)
        replaced += 1
    print("  wrote %d xml files from flash dats" % replaced)

    print("== FULL-PORT: delete OpenTK-only Xmls ==")
    deleted = 0
    for p in sorted(glob.glob(os.path.join(xmls_dir, "*.xml"))):
        if os.path.basename(p) not in wanted:
            os.remove(p)
            deleted += 1
            print("  deleted %s" % os.path.basename(p))
    print("  deleted %d OpenTK-only xml files" % deleted)
    print("  (recover with: git -C <realm-opentk> restore AlloyClient/Content)")

    print("== FULL-PORT: pad sheets to atlas cell multiples ==")
    try:
        from PIL import Image as PILImage
    except ImportError:
        PILImage = None
    atlas = parse_atlas(os.path.join(content_dir, "Game.atlas"))
    need = {}  # sheet file -> [cellw, cellh, ...]
    for (name, (src, w, h)) in atlas.items():
        if w:
            need.setdefault(src, []).append((w, h))
    for (src, cells) in sorted(need.items()):
        p = os.path.join(sheets_dir, src)
        if not os.path.isfile(p):
            continue
        (W, H) = png_size(p)
        (nW, nH) = (W, H)
        for (w, h) in cells:  # divisible by every cell size on this sheet
            nW = ((nW + w - 1) // w) * w
            nH = ((nH + h - 1) // h) * h
        if (nW, nH) == (W, H):
            continue
        if PILImage is None:
            print("  CANNOT PAD %s (need Pillow): %dx%d" % (src, W, H))
            continue
        img = PILImage.open(p).convert("RGBA")
        canvas = PILImage.new("RGBA", (nW, nH), (0, 0, 0, 0))
        canvas.paste(img, (0, 0))
        canvas.save(p)
        print("  padded %-28s %dx%d -> %dx%d" % (src, W, H, nW, nH))

    print("== POST-PORT verify ==")
    (n_refs, errors) = verify_client(content_dir)
    print("  texture refs checked: %d, errors: %d" % (n_refs, len(errors)))
    for e in errors[:30]:
        print("  ERROR: %s" % e)
    return 1 if errors else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("command", nargs="?", default="check",
                    choices=["check", "apply", "full-port", "verify"])
    ap.add_argument("--flash", default=os.path.join(WS_ROOT, "realm-client",
                                                    "src", "kabam", "rotmg", "assets"))
    ap.add_argument("--content", default=os.path.join(OT_ROOT, "AlloyClient", "Content"))
    ap.add_argument("--server", default=os.path.join(WS_ROOT, "realm-server",
                                                     "Resources", "GameData"))
    a = ap.parse_args()
    if a.command == "verify":
        (n, errors) = verify_client(a.content)
        print("texture refs checked: %d, errors: %d" % (n, len(errors)))
        for e in errors:
            print("ERROR: %s" % e)
        return 1 if errors else 0
    if a.command == "apply":
        return cmd_apply(a.flash, a.content, a.server)
    if a.command == "full-port":
        return cmd_full_port(a.flash, a.content, a.server)
    cmd_check(a.flash, a.content, a.server)
    return 0


if __name__ == "__main__":
    sys.exit(main())
