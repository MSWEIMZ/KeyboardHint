import json, os, re, sys, collections

DIR = os.path.join(os.path.dirname(__file__), "..", "src", "KeyboardHint", "shortcuts")
DIR = os.path.normpath(DIR)

KEYS = set(
    [chr(ord('A')+i) for i in range(26)] +
    [f"D{i}" for i in range(10)] +
    [f"F{i}" for i in range(1, 13)] +
    "Esc Back Tab Caps Enter Space PgUp PgDn End Home Ins Del Left Up Right Down".split() +
    "LShift RShift LControl RControl LAlt RAlt LWin RWin".split() +
    "Oem1 Oem2 Oem3 Oem4 Oem5 Oem6 Oem7 OemMinus OemPlus OemComma OemPeriod Oem3".split()
)

ORDER = ["win", "ctrl", "alt", "shift"]
MODS = set(ORDER)

def check_layer_name(name):
    parts = name.split("+")
    if not parts or any(p not in MODS for p in parts):
        return f"bad modifier in layer {name!r}"
    if len(set(parts)) != len(parts):
        return f"dup modifier in layer {name!r}"
    idx = [ORDER.index(p) for p in parts]
    if idx != sorted(idx):
        return f"layer {name!r} not in canonical order (win<ctrl<alt<shift)"
    return None

errors, warnings = [], []
proc_owner = collections.defaultdict(list)   # process -> [profile ids]
stats = []

for fn in sorted(os.listdir(DIR)):
    if not fn.endswith(".json"): continue
    path = os.path.join(DIR, fn)
    try:
        p = json.load(open(path, encoding="utf-8"))
    except Exception as e:
        errors.append(f"{fn}: JSON parse error: {e}")
        continue
    pid = p.get("id", "")
    if not pid: errors.append(f"{fn}: missing id")
    procs = p.get("processes") or []
    if not isinstance(procs, list) or not all(isinstance(x, str) for x in procs):
        errors.append(f"{fn}: processes must be list[str]")
    for x in procs: proc_owner[x.lower()].append(pid)
    layers = p.get("layers") or {}
    if not isinstance(layers, dict):
        errors.append(f"{fn}: layers must be object"); continue
    total = 0
    for lname, keys in layers.items():
        e = check_layer_name(lname)
        if e: errors.append(f"{fn}: {e}")
        if not isinstance(keys, dict):
            errors.append(f"{fn}: layer {lname} not object"); continue
        for k, v in keys.items():
            if k not in KEYS:
                errors.append(f"{fn}: {lname}.{k} not a valid key id")
            if not isinstance(v, str) or not v.strip():
                errors.append(f"{fn}: {lname}.{k} empty desc")
            if len(v) > 6:
                warnings.append(f"{fn}: {lname}.{k} desc {len(v)} chars: {v}")
        total += len(keys)
    stats.append((pid, len(layers), total))

dupes = {k: v for k, v in proc_owner.items() if len(v) > 1}
for k, v in sorted(dupes.items()):
    warnings.append(f"process '{k}' claimed by multiple profiles: {v}")

print(f"profiles: {len(stats)}  total entries: {sum(s[2] for s in stats)}")
print()
if errors:
    print("ERRORS:"); [print(" ", e) for e in errors]
if warnings:
    print("WARNINGS:"); [print(" ", w) for w in warnings]
if not errors: print("ALL VALID")
