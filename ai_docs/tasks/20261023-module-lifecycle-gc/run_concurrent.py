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

processes = []
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
    stdout = (out / "stdout.txt").open("w", encoding="utf-8")
    stderr = (out / "stderr.txt").open("w", encoding="utf-8")
    process = subprocess.Popen(
        args, cwd=str(workspace), stdout=stdout, stderr=stderr,
        text=True, encoding="utf-8", errors="replace", env=env,
    )
    (out / "command.txt").write_text(
        "workdir=H:/Bing_Framework/Bing.NetCore\n" +
        " ".join(f'"{a}"' if " " in a else a for a in args) + "\n",
        encoding="utf-8",
    )
    processes.append((name, process, stdout, stderr, out))

for name, process, stdout, stderr, out in processes:
    code = process.wait()
    stdout.close()
    stderr.close()
    (out / "exit.txt").write_text(f"{code}\n", encoding="utf-8")
    text = (out / "stdout.txt").read_text(encoding="utf-8")
    tail = "\n".join(line for line in text.splitlines() if "Passed!" in line or "Failed!" in line or "- Failed:" in line)
    print(f"{name}: exit={code} {tail}")
