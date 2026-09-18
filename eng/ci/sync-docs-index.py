#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""docs/index.md 派生脚本。

背景
----
``docs/index.md`` 是 docfx 站点的首页，而仓库根目录的 ``README.md`` 是给 GitHub
访客看的。两者的内容应始终一致，但链接基准不同（README 里写 ``docs/xxx.md``，
index.md 里必须写 ``xxx.md``）。历史上这两份文件靠手工维护，已经漂移过：

* 包表漏掉 13 个可发包（55/67）；
* 测试栈写错（NSubstitute -> 实际是 Shouldly / Moq）；
* PostgreSQL 驱动名拼错（NPostgreSql -> Npgsql）；
* 某个徽章 URL 多了一个句点；
* 「按需求快速入口」整节只在 README 里存在。

因此本脚本以 **根 README.md 为唯一来源**，自动派生 ``docs/index.md``。
请勿手工编辑 ``docs/index.md``——改动会在下次派生时丢失。

用法
----
    python eng/ci/sync-docs-index.py            # 生成 / 覆盖 docs/index.md
    python eng/ci/sync-docs-index.py --check    # 只校验是否一致，不一致退出码 1

派生规则
--------
1. 链接前缀 ``](docs/`` 去掉 ``docs/``，改为 docs 内相对路径；
2. 指向仓库根目录文件的链接加上 ``../``（``CONTRIBUTING.md`` / ``global.json`` 等）；
3. 文件开头插入「自动派生」横幅注释。

注意：横幅内容必须稳定（不含时间戳），否则 ``--check`` 无法幂等。
"""
from __future__ import annotations

import argparse
import io
import os
import re
import sys

# 仓库根目录（本脚本位于 eng/ci/ 下）
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
README = os.path.join(ROOT, "README.md")
INDEX = os.path.join(ROOT, "docs", "index.md")

BANNER = (
    "<!--\n"
    "  ⚠ 本文件由仓库根目录 README.md 自动派生，请勿手工编辑。\n"
    "  修改根 README.md 后运行：python eng/ci/sync-docs-index.py\n"
    "  一致性由 CI 校验：python eng/ci/check-docs.py\n"
    "-->\n\n"
)

# 指向仓库根目录（而非 docs/）的文件，需要补 ../ 前缀
# 注意：不要加入 "README.md"——根 README 里的 `](README.md)` 指的是 docs 自己的导航 hub，
# 加进来会被错误改写成 ../README.md。
ROOT_LEVEL_TARGETS = (
    "CONTRIBUTING.md",
    "global.json",
    "LICENSE",
    "AGENTS.md",
    "README.en.md",
)


def derive(readme_text: str) -> str:
    """把根 README 文本派生为 docs/index.md 文本。"""
    out = readme_text

    # 规则 1：docs/xxx -> xxx
    out = out.replace("](docs/", "](")

    # 规则 2：根目录文件 -> ../xxx
    for name in ROOT_LEVEL_TARGETS:
        out = out.replace("](%s)" % name, "](../%s)" % name)

    # 规则 3：横幅
    return BANNER + out


def read(path: str) -> str:
    with io.open(path, encoding="utf-8") as f:
        return f.read()


def write(path: str, text: str) -> None:
    # 统一 LF，避免 Windows 检出把 CRLF 带进仓库导致 --check 误报
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="从根 README.md 派生 docs/index.md")
    parser.add_argument(
        "--check",
        action="store_true",
        help="只校验 docs/index.md 是否与派生结果一致，不写文件",
    )
    args = parser.parse_args(argv)

    if not os.path.exists(README):
        print("ERROR: 找不到 %s" % README)
        return 2

    expected = derive(read(README))

    if args.check:
        if not os.path.exists(INDEX):
            print("FAIL docs/index.md 不存在，请运行 sync-docs-index.py")
            return 1
        actual = read(INDEX).replace("\r\n", "\n")
        if actual == expected:
            print("OK   docs/index.md 与根 README.md 一致")
            return 0
        # 给出粗略差异定位，方便排查
        a, b = actual.split("\n"), expected.split("\n")
        diff_at = next((i for i in range(max(len(a), len(b)))
                        if (a[i] if i < len(a) else None) != (b[i] if i < len(b) else None)), 0)
        print("FAIL docs/index.md 与根 README.md 不一致（首个差异在第 %d 行）" % (diff_at + 1))
        print("     请运行：python eng/ci/sync-docs-index.py")
        return 1

    write(INDEX, expected)
    print("OK   已从 README.md 生成 docs/index.md（%d 字符）" % len(expected))

    # 派生后立刻做一次链接可达性自检
    bad = []
    base = os.path.dirname(INDEX)
    for m in re.findall(r"\]\((\.{0,2}[^)\s]+)\)", expected):
        if m.startswith("http") or m.startswith("#"):
            continue
        if not os.path.exists(os.path.normpath(os.path.join(base, m))):
            bad.append(m)
    if bad:
        print("WARN 派生结果存在不可达链接：%s" % ", ".join(sorted(set(bad))))
    else:
        print("OK   派生结果链接全部可达")
    return 0


if __name__ == "__main__":
    sys.exit(main())
