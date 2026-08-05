"""Verify every PC TIPS catalog item has bundled normal/hover art and a Chinese title."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("repo", type=Path)
    parser.add_argument("games", type=Path)
    args = parser.parse_args()

    resource_root = args.repo / "ios-port/Assets/Higurashi/Resources/TipsPreviews"
    title_items = json.loads((resource_root / "titles.json").read_text(encoding="utf-8"))["Items"]
    title_keys = {(item["Episode"], item["Id"]) for item in title_items}
    failed = False

    for episode in range(1, 9):
        data = (args.games / f"Higurashi When They Cry {episode:02}" /
                f"HigurashiEp{episode:02}_Data")
        catalog_path = data / "tips.json"
        if not catalog_path.exists():
            catalog_path = data / "StreamingAssets/Data/tips.txt"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
        groups = catalog.values() if isinstance(catalog, dict) else (catalog,)
        entries = [entry for group in groups for entry in group]
        missing: list[int] = []
        for entry in entries:
            tip_id = int(entry["Id"])
            prefix = "tips" if episode == 1 else "tips_"
            base = resource_root / f"ep{episode:02}" / f"{prefix}{tip_id:03}"
            if not ((base.parent / f"{base.name}_normal.png").exists() and
                    (base.parent / f"{base.name}_hover.png").exists() and
                    (episode, tip_id) in title_keys):
                missing.append(tip_id)
        print(f"EP{episode:02}: catalog={len(entries)} missing={missing}")
        failed = failed or bool(missing)

    raise SystemExit(1 if failed else 0)


if __name__ == "__main__":
    main()
