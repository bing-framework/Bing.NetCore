from pathlib import Path
from collections import Counter
import json
import re
import xml.etree.ElementTree as ET

root = Path(r"H:\Bing_Framework\Bing.NetCore\ai_docs/tasks/20261023-module-lifecycle-gc/artifacts")
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
runs = []
for trx in sorted(root.glob("*/trx/*.trx")):
    name = trx.parent.parent.name
    tree = ET.parse(trx)
    doc = tree.getroot()
    outcomes = Counter()
    failed = []
    for item in doc.findall(".//t:Results/t:UnitTestResult", ns):
        outcome = item.get("outcome")
        outcomes[outcome] += 1
        if outcome == "Failed":
            failed.append(item.get("testName"))
    exit_file = trx.parent.parent / "exit.txt"
    exit_value = exit_file.read_text(encoding="utf-8-sig").strip() if exit_file.exists() else ""
    runs.append({
        "run": name,
        "tfm": "net6.0" if "net6" in name else "net8.0" if "net8" in name else "unknown",
        "total": sum(outcomes.values()),
        "passed": outcomes.get("Passed", 0),
        "failed": outcomes.get("Failed", 0),
        "notExecuted": sum(v for k, v in outcomes.items() if k not in ("Passed", "Failed")),
        "outcomes": dict(outcomes),
        "exit": exit_value,
        "failedTests": failed,
        "trx": str(trx.relative_to(root)).replace("\\", "/"),
    })

summary = {
    "runCount": len(runs),
    "runs": runs,
    "anyFailed": [run for run in runs if run["failed"] > 0],
}
(root / "run-summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
for run in runs:
    print(run["run"], run["tfm"], f'{run["passed"]}/{run["failed"]}/{run["notExecuted"]}', "exit=" + run["exit"])
