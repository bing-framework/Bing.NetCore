from pathlib import Path
import os
import subprocess
import sys

workspace = Path(r"H:\Bing_Framework\Bing.NetCore")
root = workspace / "ai_docs/tasks/20261023-module-lifecycle-gc/artifacts"
prefix = sys.argv[1]
tfm = sys.argv[2]
count = int(sys.argv[3])
project = workspace / "framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj"
env = os.environ.copy()
env["DOTNET_CLI_UI_LANGUAGE"] = "en-US"
env["DOTNET_NOLOGO"] = "1"

for index in range(1, count + 1):
    name = f"{prefix}-{index:02d}"
    out = root / name
    trx_dir = out / "trx"
    trx_dir.mkdir(parents=True, exist_ok=True)
    args = [
        "dotnet", "test", str(project), "-c", "Release", "-f", tfm,
        "--no-build", "--no-restore", "--nologo",
        "--logger", f"trx;LogFileName={name}.trx",
        "--results-directory", str(trx_dir),
    ]
    completed = subprocess.run(
        args, cwd=str(workspace), stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, encoding="utf-8", errors="replace", env=env,
    )
    (out / "stdout.txt").write_text(completed.stdout, encoding="utf-8")
    (out / "stderr.txt").write_text(completed.stderr, encoding="utf-8")
    (out / "exit.txt").write_text(f"{completed.returncode}\n", encoding="utf-8")
    (out / "command.txt").write_text(
        "workdir=H:/Bing_Framework/Bing.NetCore\n" +
        " ".join(f'"{a}"' if " " in a else a for a in args) + "\n",
        encoding="utf-8",
    )
    tail = "\n".join(line for line in completed.stdout.splitlines() if "Passed!" in line or "Failed!" in line or "- Failed:" in line)
    print(f"{name}: exit={completed.returncode} {tail}")
