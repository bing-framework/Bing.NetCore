# Agent 全局执行规范

本文件用于约束 Copilot、Codex、Gemini、Claude 等 AI 工具在当前项目中的执行方式。

## 默认行为

- 默认使用简体中文回答。
- 生成代码时，优先补充必要的中文注释。
- 所有命令、脚本、文件读写、日志导出、Markdown 生成，都必须优先考虑 Windows + VS Code + PowerShell 环境下的中文乱码问题。
- 默认编码统一使用 UTF-8。

## UTF-8 规则（强制）

- 所有文本文件读取必须显式指定 `UTF-8`。
- 所有文本文件写入必须显式指定 `UTF-8`。
- 禁止使用 PowerShell 默认编码直接写入源码、Markdown、XML、Gradle、Kotlin、Java、YAML、JSON、Properties 文件。
- 优先使用 Python 进行文件读写：

```python
from pathlib import Path

text = Path("input.txt").read_text(encoding="utf-8")
Path("output.txt").write_text(text, encoding="utf-8")
```

- 在 shell 命令中修改中文内容时，避免直接内联中文大段文本；优先使用 Python 组装字符串后以 UTF-8 写入。
- 终端出现 `????` 时，不要直接判断文件已损坏；应优先用编辑器或 `unicode_escape` 检查文件真实内容。

## PowerShell 编码规则

在生成 PowerShell 脚本时，必须优先设置控制台编码：

```powershell
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
```

写入文件时必须显式指定编码：

```powershell
Set-Content -Path $path -Value $content -Encoding utf8
Add-Content -Path $path -Value $content -Encoding utf8
Out-File -FilePath $path -Encoding utf8
Export-Csv -Path $path -Encoding utf8 -NoTypeInformation
```

禁止在未确认编码的情况下，直接使用：

```powershell
"中文内容" > file.md
"中文内容" >> file.md
```

## Python 编码规则

生成 Python 脚本时，所有文本文件读写必须显式指定编码：

```python
from pathlib import Path

content = Path("input.txt").read_text(encoding="utf-8")
Path("output.txt").write_text(content, encoding="utf-8")
```

禁止使用不带 `encoding` 的文件读写：

```python
open("input.txt").read()
open("output.txt", "w").write("内容")
```

## .NET / C# 编码规则

生成 .NET / C# 文件读写代码时，必须显式指定 UTF-8：

```csharp
using System.Text;

var text = File.ReadAllText(path, Encoding.UTF8);
File.WriteAllText(path, text, Encoding.UTF8);
```

控制台程序如需输出中文，优先设置：

```csharp
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
```

## Node.js 编码规则

生成 Node.js / TypeScript 文件读写代码时，必须显式指定 `utf8`：

```ts
import { readFileSync, writeFileSync } from "node:fs";

const text = readFileSync("input.txt", "utf8");
writeFileSync("output.txt", text, "utf8");
```

## 文件类型规则

以下文件必须按 UTF-8 处理：

- `.cs`
- `.csproj`
- `.sln`
- `.props`
- `.targets`
- `.json`
- `.yaml`
- `.yml`
- `.xml`
- `.md`
- `.sql`
- `.ps1`
- `.bat`
- `.cmd`
- `.js`
- `.ts`
- `.vue`
- `.java`
- `.kt`
- `.gradle`
- `.properties`

## 乱码排查规则

如果用户反馈乱码，优先按以下顺序排查：

1. VS Code 当前文件编码是否为 UTF-8。
2. PowerShell Profile 是否设置 UTF-8。
3. `[Console]::OutputEncoding` 是否为 UTF-8。
4. `$OutputEncoding` 是否为 UTF-8。
5. `chcp` 是否为 `65001`。
6. 文件写入命令是否显式指定 `-Encoding utf8`。
7. Python / Node.js / .NET 是否显式指定 UTF-8。
8. 是否由旧文件本身就是 GBK / ANSI 编码导致。

## 禁止行为

- 禁止默认依赖 Windows ANSI / GBK 编码。
- 禁止在未确认编码时批量替换中文内容。
- 禁止使用未指定编码的文件迁移脚本。
- 禁止生成可能导致中文变成 `????` 的命令。
- 禁止把终端显示乱码直接等同于文件内容损坏。

## 推荐处理方式

当需要批量修改文件内容时，优先生成 Python 脚本，并使用：

```python
from pathlib import Path

path = Path("target-file.md")
text = path.read_text(encoding="utf-8")
text = text.replace("旧内容", "新内容")
path.write_text(text, encoding="utf-8")
```

当需要验证文件真实内容时，可以使用：

```python
from pathlib import Path

text = Path("target-file.md").read_text(encoding="utf-8")
print(text.encode("unicode_escape").decode("ascii"))
```