import re
import json
from pathlib import Path

ROOT = Path(r"d:\Unity Project\SRPG-SRPG\Assets")


def unescape_yaml_text(text: str) -> str:
    text = text.strip()
    if text.startswith("'") and text.endswith("'"):
        return text[1:-1]
    if text.startswith('"') and text.endswith('"'):
        return text[1:-1]
    return text


def make_key(go_name: str, parent_name: str, text: str, suffix: str = "") -> str:
    base = re.sub(r"[^a-zA-Z0-9]+", "_", go_name).strip("_")
    if not base:
        base = "Text"
    key = f"{base}_key"
    if suffix:
        key = f"{base}_{suffix}_key"
    return key


def parse_unity_file(path: Path):
    content = path.read_text(encoding="utf-8", errors="replace")

    go_names = {}
    go_parents = {}
    for block in re.split(r"--- !u!1 &", content):
        if not block.startswith("\n"):
            continue
        fid_m = re.match(r"(\d+)", block)
        if not fid_m:
            continue
        fid = fid_m.group(1)
        name_m = re.search(r"\n  m_Name: (.+)", block)
        if name_m:
            go_names[fid] = name_m.group(1).strip()
        parent_m = re.search(r"\n  m_Father: \{fileID: (\d+)\}", block)
        if parent_m:
            go_parents[fid] = parent_m.group(1)

    def get_parent_name(go_id: str) -> str:
        parent_id = go_parents.get(go_id)
        if parent_id and parent_id != "0":
            return go_names.get(parent_id, "Root")
        return "Root"

    results = []
    seen = set()

    # MonoBehaviour blocks with m_text (TMP) or m_Text (legacy)
    for block in re.split(r"--- !u!", content):
        if "m_GameObject:" not in block:
            continue
        go_m = re.search(r"  m_GameObject: \{fileID: (\d+)\}", block)
        if not go_m:
            continue
        go_id = go_m.group(1)
        go_name = go_names.get(go_id, "Unknown")

        text = None
        comp_type = None
        if "m_text:" in block:
            tm = re.search(r"  m_text: (.+)", block)
            if tm:
                text = unescape_yaml_text(tm.group(1))
                comp_type = "TextMeshProUGUI"
        elif "m_Text:" in block and "114" in block[:20]:
            tm = re.search(r"  m_Text: (.+)", block)
            if tm:
                text = unescape_yaml_text(tm.group(1))
                comp_type = "TextLegacy"

        if text is None:
            continue

        parent = get_parent_name(go_id)
        dedup = (go_name, text, comp_type)
        if dedup in seen:
            continue
        seen.add(dedup)

        results.append(
            {
                "gameObject": go_name,
                "parent": parent,
                "type": comp_type,
                "text": text,
                "suggestedKey": make_key(go_name, parent, text),
            }
        )

    # Dropdown options (standalone)
    for m in re.finditer(r"    - m_Text: (.+)", content):
        text = unescape_yaml_text(m.group(1))
        dedup = ("DropdownOption", text, "Dropdown")
        if dedup in seen:
            continue
        seen.add(dedup)
        opt_key = re.sub(r"[^a-zA-Z0-9]+", "_", text).strip("_") + "_key"
        results.append(
            {
                "gameObject": "DropdownOption",
                "parent": "Dropdown",
                "type": "Dropdown",
                "text": text,
                "suggestedKey": opt_key,
            }
        )

    return results


def scene_to_table(path: Path) -> str:
    name = path.stem
    mapping = {
        "0_Bootstrap": None,
        "1_Login": "Login",
        "2_DevConnect": "DevConnect",
        "3_MainMenu": "MainMenu",
        "4_Lobby": "Lobby",
        "4_SingleStoryLobby": "StoryLobby",
        "5_ClientMatch": "ClientMatch",
        "5_ServerMatch": "ServerMatch",
        "6_MatchResult": "MatchResult",
        "7_UnitList": "UnitList",
        "8_UnitConfig": "UnitConfig",
        "9_Gacha": "Gacha",
        "C0_S0": "Story_C0_S0",
        "C0_S1": "Story_C0_S1",
        "C0_S1_Server": "Story_C0_S1_Server",
    }
    if name in mapping:
        return mapping[name]
    if name.startswith("C"):
        return f"Story_{name}"
    return name


def prefab_to_table(path: Path) -> str:
    name = path.stem
    mapping = {
        "UnitContainer": "Shared_UnitContainer",
        "OptionContainer": "Shared_OptionContainer",
        "LoadingScreen": "Shared_LoadingScreen",
        "MatchSessionCanvas": "Shared_MatchSession",
        "UnitCanvas": "Shared_UnitCanvas",
    }
    return mapping.get(name, f"Shared_{name}")


def main():
    output = {"tables": {}, "dynamicStrings": []}

    for p in sorted(ROOT.glob("Scenes/**/*.unity")):
        table = scene_to_table(p)
        if table is None:
            continue
        items = parse_unity_file(p)
        rel = str(p.relative_to(ROOT)).replace("\\", "/")
        output["tables"].setdefault(table, {"source": rel, "entries": []})
        for item in items:
            item["table"] = table
            output["tables"][table]["entries"].append(item)

    for p in sorted(ROOT.glob("Prefabs/**/*.prefab")):
        items = parse_unity_file(p)
        if not items:
            continue
        table = prefab_to_table(p)
        rel = str(p.relative_to(ROOT)).replace("\\", "/")
        output["tables"].setdefault(table, {"source": rel, "entries": []})
        for item in items:
            item["table"] = table
            output["tables"][table]["entries"].append(item)

    out_path = ROOT.parent / "Tools" / "localization_scan.json"
    out_path.write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {out_path}")
    for table, data in sorted(output["tables"].items()):
        print(f"{table}: {len(data['entries'])} entries")


if __name__ == "__main__":
    main()
