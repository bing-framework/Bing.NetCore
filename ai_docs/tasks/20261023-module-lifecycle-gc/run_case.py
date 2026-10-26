from pathlib import Path
import os
import subprocess
import sys

workspace = Path(r"H:\Bing_Framework\Bing.NetCore")
artifact_root = workspace / "ai_docs/tasks/20261023-module-lifecycle-gc/artifacts"
name = sys.argv[1]
tfm = sys.argv[2]
filter_expr = sys.argv[3] if len(sys.argv) > 3 else None
extra = sys.argv[4:]

project = workspace / "framework/tests/Bing.Core.Tests/Bing.Core.Tests.csproj"
out = artifact_root / name
trx_dir = out / "trx"
trx_dir.mkdir(parents=True, exist_ok=True)

args = [
    "dotnet", "test", str(project), "-c", "Release", "-f", tfm,
    "--no-restore", "--nologo",
    "--logger", f"trx;LogFileName={name}.trx",
    "--results-directory", str(trx_dir),
]
if filter_expr and filter_expr not in ("-", "none"):
    args += ["--filter", filter_expr]
args += extra

env = os.environ.copy()
env["DOTNET_CLI_UI_LANGUAGE"] = "en-US"
env["DOTNET_NOLOGO"] = "1"

completed = subprocess.run(
    args,
    cwd=str(workspace),
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True,
    encoding="utf-8",
    errors="replace",
    env=env,
)

(out / "stdout.txt").write_text(completed.stdout, encoding="utf-8")
(out / "stderr.txt").write_text(completed.stderr, encoding="utf-8")
(out / "exit.txt").write_text(f"{completed.returncode}\n", encoding="utf-8")
(out / "command.txt").write_text(
    "workdir=H:/Bing_Framework/Bing.NetCore\n" +
    " ".join(f'"{a}"' if " " in a else a for a in args) + "\n",
    encoding="utf-8",
)
print(f"{name}: exit={completed.returncode}")
print(completed.stdout[-2500:])
if completed.stderr:
    print("STDERR:")
    print(completed.stderr[-2500:])
