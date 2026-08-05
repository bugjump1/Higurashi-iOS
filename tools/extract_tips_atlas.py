"""Extract the original PC NGUI TIPS card sprites without modifying game files."""

from __future__ import annotations

import argparse
import json
import re
import struct
from pathlib import Path
from typing import Any, Iterable

import UnityPy


def walk(value: Any) -> Iterable[dict[str, Any]]:
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


def sprite_name(value: dict[str, Any]) -> str:
    for key in ("name", "m_Name", "Name"):
        candidate = value.get(key)
        if isinstance(candidate, str):
            return candidate
    return ""


def number(value: dict[str, Any], *keys: str) -> int | None:
    for key in keys:
        candidate = value.get(key)
        if isinstance(candidate, (int, float)):
            return round(candidate)
    return None


def rect_from_sprite(value: dict[str, Any]) -> tuple[int, int, int, int] | None:
    outer = value.get("outer") or value.get("m_Outer") or value.get("rect")
    if isinstance(outer, dict):
        x = number(outer, "x", "m_X")
        y = number(outer, "y", "m_Y")
        width = number(outer, "width", "m_Width")
        height = number(outer, "height", "m_Height")
    else:
        x = number(value, "x", "m_X")
        y = number(value, "y", "m_Y")
        width = number(value, "width", "m_Width")
        height = number(value, "height", "m_Height")
    if None in (x, y, width, height) or width <= 0 or height <= 0:
        return None
    return x, y, width, height


def parse_ngui_atlas(raw: bytes) -> dict[str, tuple[int, int, int, int]]:
    # Unity 5 serializes UIAtlas as the standard 32-byte MonoBehaviour header,
    # a 12-byte Material PPtr, then List<UISpriteData>. UISpriteData consists of
    # an aligned UTF-8 name followed by x/y/width/height, borders and padding.
    offset = 44
    if len(raw) < offset + 4:
        return {}
    count = struct.unpack_from("<I", raw, offset)[0]
    offset += 4
    if count <= 0 or count > 10000:
        return {}

    result: dict[str, tuple[int, int, int, int]] = {}
    try:
        for _ in range(count):
            length = struct.unpack_from("<I", raw, offset)[0]
            offset += 4
            if length > 1024 or offset + length > len(raw):
                return {}
            name = raw[offset : offset + length].decode("utf-8")
            offset = (offset + length + 3) & ~3
            values = struct.unpack_from("<12i", raw, offset)
            offset += 48
            result[name] = values[0], values[1], values[2], values[3]
    except (UnicodeDecodeError, struct.error):
        return {}
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("game_data", type=Path)
    parser.add_argument("episode", type=int)
    parser.add_argument("output", type=Path)
    parser.add_argument("--inspect", action="store_true")
    args = parser.parse_args()

    catalog_path = args.game_data / "tips.json"
    if not catalog_path.exists():
        catalog_path = args.game_data / "StreamingAssets" / "Data" / "tips.txt"
    allowed_ids: set[int] = set()
    if catalog_path.exists():
        catalog = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
        groups = catalog.values() if isinstance(catalog, dict) else (catalog,)
        for group in groups:
            for entry in group:
                allowed_ids.add(int(entry["Id"]))

    paths = [
        args.game_data / "mainData",
        args.game_data / "resources.assets",
        args.game_data / "sharedassets0.assets",
    ]
    env = UnityPy.load(*(str(path) for path in paths if path.exists()))

    atlas = None
    for obj in env.objects:
        if obj.type.name != "Texture2D":
            continue
        try:
            data = obj.read()
        except Exception:
            continue
        if data.m_Name == "TipsPrefab":
            atlas = data.image.convert("RGBA")
            break
    if atlas is None:
        raise RuntimeError("TipsPrefab texture atlas was not found")

    sprites: dict[str, tuple[int, int, int, int]] = {}
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        raw = obj.get_raw_data()
        if b"tips" not in raw:
            continue
        parsed = parse_ngui_atlas(raw)
        sprites.update({name: rect for name, rect in parsed.items()
                        if name.lower().startswith("tips")})

    if args.inspect:
        print(f"atlas={atlas.width}x{atlas.height} sprites={len(sprites)}")
        for name, rect in sorted(sprites.items()):
            print(name, rect)
        return

    args.output.mkdir(parents=True, exist_ok=True)
    for old_preview in args.output.glob("*.png"):
        old_preview.unlink()
    written = 0
    for name, (x, y, width, height) in sorted(sprites.items()):
        lower = name.lower()
        if not (lower.endswith("_normal") or lower.endswith("_hover")):
            continue
        if "na_" in lower:
            continue
        match = re.match(r"^tips_?(\d{3})(?:na)?_(?:normal|hover)$", lower)
        if match is None or (allowed_ids and int(match.group(1)) not in allowed_ids):
            continue
        # NGUI stores sprite Y coordinates from the top of its texture atlas.
        image = atlas.crop((x, y, x + width, y + height))
        image.save(args.output / f"{name}.png", optimize=True)
        written += 1

    if written == 0:
        raise RuntimeError("No TIPS normal/hover sprites were extracted")
    print(f"EP{args.episode:02}: extracted {written} sprites to {args.output}")


if __name__ == "__main__":
    main()
