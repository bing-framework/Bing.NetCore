"""运行指定测试项目的单个 TFM，并把输出保存到本 task artifacts。"""
from pathlib import Path
import os
import subprocess
import sys

workspace = Path(r"H:\Bing_Framework\Bing.NetCore")
task = workspace / "ai_docs/tasks/20261023-module-lifecycle-retention-fix"
name = sys.argv[1]
project = workspace / sys.argv[2]
tfm = sys.argv[3]
filter_expr = sys.argv[4] if len(sys.argv) > 4 else None
out = task / "artifacts" / name
trx_dir = out / "trx"
trx_dir.mkdir(parents=True, exist_ok=True)
args = [
    "dotnet", "test", str(project), "-c", "Release", "-f", tfm,
    "--no-restore", "--nologo", "--logger", "trx",
    "--results-directory", str(trx_dir),
]
if filter_expr:
    args += ["--filter", filter_expr]
env = os.environ.copy()
env["DOTNET_CLI_UI_LANGUAGE"] = "en-US"
env["DOTNET_NOLOGO"] = "1"
completed = subprocess.run(args, cwd=str(workspace), env=env, stdout=subprocess.PIPE,
                           stderr=subprocess.PIPE, text=True, encoding="utf-8", errors="replace")
(out / "stdout.txt").write_text(completed.stdout, encoding="utf-8")
(out / "stderr.txt").write_text(completed.stderr, encoding="utf-8")
(out / "exit.txt").write_text(f"{completed.returncode}\n", encoding="utf-8")
(out / "command.txt").write_text(
    "workdir=H:/Bing_Framework/Bing.NetCore\n" +
    " ".join(f'"{a}"' if " " in a else a for a in args) + "\n", encoding="utf-8")
print(name, "exit", completed.returncode)
print(completed.stdout[-3000:])
if completed.stderr:
    print(completed.stderr[-2000:])
