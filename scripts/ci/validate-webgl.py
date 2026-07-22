#!/usr/bin/env python3
import json, pathlib, re, sys

root = pathlib.Path(sys.argv[1]).resolve()
index = (root / "index.html").read_text(encoding="utf-8", errors="replace")
refs = set(re.findall(r'''(?:src|href)=["']([^"'?#]+)''', index, re.I))
for cfg in root.glob("Build/*.json"):
    try:
        data = json.loads(cfg.read_text(encoding="utf-8"))
        refs.update(v for k, v in data.items() if isinstance(v, str) and (k.endswith("Url") or k.endswith("Filename")))
    except (ValueError, OSError):
        pass
missing = []
for ref in refs:
    if re.match(r"^(?:https?:|data:|//|#)", ref):
        continue
    candidate = (root / ref).resolve()
    try:
        candidate.relative_to(root)
    except ValueError:
        missing.append(f"unsafe path: {ref}")
        continue
    if not candidate.is_file():
        missing.append(ref)
if missing:
    print("Missing/unsafe WebGL references:\n" + "\n".join(missing), file=sys.stderr)
    raise SystemExit(1)
print(f"Validated {len(refs)} WebGL HTML/config references.")
