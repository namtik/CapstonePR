"""Card_DB.xlsx -> Assets/Resources/CardDB/*.json 변환기.

기획자 워크플로우:
  1) Card_DB.xlsx 수정
  2) `python Tools/ConvertCardDB.py <엑셀경로>` 실행
  3) Unity 에디터가 Resources 변경 감지 -> 게임에 반영

사용법:
  python Tools/ConvertCardDB.py                       # 기본: C:\\Users\\dusdn\\Downloads\\Card_DB.xlsx
  python Tools/ConvertCardDB.py path/to/Card_DB.xlsx  # 임의 경로
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import pandas as pd

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_XLSX = Path(r"C:\Users\dusdn\Downloads\Card_DB.xlsx")
OUT_DIR = REPO_ROOT / "Assets" / "Resources" / "CardDB"


def _clean(value):
    if value is None:
        return ""
    if isinstance(value, float):
        if math.isnan(value):
            return ""
        if value.is_integer():
            return int(value)
        return value
    return str(value).strip()


def _bool(value) -> bool:
    s = str(_clean(value)).upper()
    return s in ("TRUE", "1", "Y", "YES")


def _int(value, default: int = 0) -> int:
    v = _clean(value)
    if v == "":
        return default
    try:
        return int(float(v))
    except (TypeError, ValueError):
        return default


def _tags(value) -> list[str]:
    raw = _clean(value)
    if not raw:
        return []
    return [t.strip() for t in str(raw).split(";") if t.strip()]


def convert_cards(df: pd.DataFrame) -> list[dict]:
    cards = []
    for _, row in df.iterrows():
        card_id = _int(row.get("CardID"), -1)
        if card_id < 0:
            continue
        cards.append({
            "id": card_id,
            "name": str(_clean(row.get("Name"))),
            "element": str(_clean(row.get("Element"))).upper(),
            "type": str(_clean(row.get("CardType"))).upper(),
            "gauge": _int(row.get("GaugeCost"), 0),
            "tags": _tags(row.get("Tags")),
            "comboSlot": _bool(row.get("ComboSlot")),
            "effectName": str(_clean(row.get("EffectName"))),
            "skillImg": str(_clean(row.get("SkillImg"))),
            "description": str(_clean(row.get("DescriptionKR"))),
        })
    cards.sort(key=lambda c: c["id"])
    return cards


EFFECT_FIELDS = [
    ("EffectIndex", "index", "int"),
    ("When", "when", "str"),
    ("If", "ifCond", "str"),
    ("Do", "doAction", "str"),
    ("Target", "target", "str"),
    ("Amount", "amount", "int"),
    ("Formula", "formula", "str"),
    ("Hits", "hits", "int"),
    ("HitFormula", "hitFormula", "str"),
    ("Status", "status", "str"),
    ("CardFilter", "cardFilter", "str"),
    ("FromZone", "fromZone", "str"),
    ("ToZone", "toZone", "str"),
    ("Select", "select", "str"),
    ("Repeat", "repeat", "str"),
    ("Extra", "extra", "str"),
    ("RuntimeKey", "runtimeKey", "str"),
]


def convert_effects(df: pd.DataFrame) -> list[dict]:
    effects = []
    for _, row in df.iterrows():
        card_id = _int(row.get("CardID"), -1)
        if card_id < 0:
            continue
        entry = {"cardId": card_id}
        for col, key, kind in EFFECT_FIELDS:
            val = row.get(col)
            if kind == "int":
                entry[key] = _int(val, 0)
            else:
                entry[key] = str(_clean(val))
        effects.append(entry)
    effects.sort(key=lambda e: (e["cardId"], e["index"]))
    return effects


def main() -> int:
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_XLSX
    if not src.exists():
        print(f"[ERROR] Excel not found: {src}", file=sys.stderr)
        return 2

    OUT_DIR.mkdir(parents=True, exist_ok=True)

    # 일부 xlsx 파일은 openpyxl 호환성 이슈가 있어 calamine을 사용.
    sheets = pd.read_excel(src, sheet_name=None, engine="calamine")

    if "Cards" not in sheets:
        print("[ERROR] Sheet 'Cards' missing", file=sys.stderr)
        return 3
    if "CardEffects" not in sheets:
        print("[ERROR] Sheet 'CardEffects' missing", file=sys.stderr)
        return 3

    cards = convert_cards(sheets["Cards"])
    effects = convert_effects(sheets["CardEffects"])

    cards_path = OUT_DIR / "Cards.json"
    effects_path = OUT_DIR / "CardEffects.json"

    cards_payload = {"cards": cards}
    effects_payload = {"effects": effects}

    cards_path.write_text(
        json.dumps(cards_payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    effects_path.write_text(
        json.dumps(effects_payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    print(f"[OK] {len(cards)} cards   -> {cards_path}")
    print(f"[OK] {len(effects)} effects -> {effects_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
