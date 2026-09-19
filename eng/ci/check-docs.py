#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""文档一致性校验（CI 用，零依赖，仅标准库）。

为什么需要它
------------
本仓库的文档规模已经不小（docs/ 下 60+ 篇 Markdown + 67 份包级 README），
而历史上反复出现过同一类问题，全部是「机器能查、人不一定记得查」的：

* 包表漏包 —— 根 README 的 NuGet 包表只有 55 行，实际有 67 个可发包；
* 双份漂移 —— ``docs/index.md``（docfx 首页）与根 ``README.md`` 长期不同步；
* 断链 —— 新增/重命名文档后，别处指向它的链接没人改；
* 事实性笔误 —— ``NSubstitute``（实际是 Shouldly/Moq）、``NPostgreSql``（实际是 Npgsql）、
  徽章 URL 末尾多一个句点；
* 包缺少 README —— 没有 README 时 nuget.org 包页面就没有说明。

本脚本把这些检查固化下来，避免每轮靠临时脚本抽查。

用法
----
    python eng/ci/check-docs.py            # 校验，全部通过退出码 0，否则 1
    python eng/ci/check-docs.py --quiet    # 只打印失败项

检查项
------
1. toc.yml            —— 所有 href 目标存在
2. toc.yml 结构       —— 只由 `- name:` / `href:` / `items:` / `#` 注释组成
3. .yml 注释          —— 不得误用 `<!-- -->`（会让 YAML 解析失败，docfx 直接报错）
4. 根 README.md       —— 本地链接可达
5. 根 README.en.md    —— 本地链接可达（英文入口）
6. docs/README.md     —— 本地链接可达（正确处理 ../ 基准）
7. docs 交叉链接      —— docs/ 下**全部** md（含子目录）的正文链接可达
8. docs 顶层归位      —— 顶层只允许入口类 md，文档必须收进主题子目录
9. 包表覆盖           —— framework/src + components/src 的可发包全部出现在 README 包表
10. index.md 同步     —— docs/index.md 与根 README 派生结果一致
11. 包级 README       —— 每个包目录都有 README.md，且含 NuGet 徽章
12. 已知坏模式        —— 扫描历史上的事实性错误是否回归
13. 源码出处          —— 文档代码块里的符号必须在源码里能找到（抓「编造的 API」，
                        由 verify-symbols.py 提供）

注：本脚本只依赖 Python 标准库，不要求安装 PyYAML——第 2/3 项用纯文本结构校验替代 YAML 解析。
"""
from __future__ import annotations

import argparse
import importlib.util
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(ROOT, "docs")

failures = []
notes = []


def fail(check: str, msg: str) -> None:
    failures.append("[%s] %s" % (check, msg))


def ok(check: str, msg: str) -> None:
    notes.append("[%s] %s" % (check, msg))


def read(path: str) -> str:
    with io.open(path, encoding="utf-8") as f:
        return f.read().replace("\r\n", "\n")


def local_links(text: str):
    """取出 markdown 里的本地链接目标（排除 http / 锚点 / mailto）。"""
    for m in re.findall(r"\]\(([^)\s]+)\)", text):
        if m.startswith(("http://", "https://", "#", "mailto:")):
            continue
        yield m.split("#")[0]


def check_links(check: str, file_path: str, base_dir: str) -> None:
    if not os.path.exists(file_path):
        fail(check, "文件不存在：%s" % os.path.relpath(file_path, ROOT))
        return
    text = read(file_path)
    bad = []
    for link in local_links(text):
        if not link:
            continue
        # 跳过模板占位与纯锚点式写法
        if link.startswith("{{") or link.endswith("/"):
            continue
        target = os.path.normpath(os.path.join(base_dir, link))
        if not os.path.exists(target):
            bad.append(link)
    if bad:
        fail(check, "断链 %d 处：%s" % (len(bad), ", ".join(sorted(set(bad)))))
    else:
        ok(check, "链接全部可达")


def package_dirs():
    """返回 {包名: 包目录绝对路径}。"""
    result = {}
    for rel in ("framework/src", "components/src"):
        base = os.path.join(ROOT, rel)
        if not os.path.isdir(base):
            continue
        for name in sorted(os.listdir(base)):
            if os.path.isdir(os.path.join(base, name)) and name.startswith("Bing."):
                result[name] = os.path.join(base, name)
    return result


def check_toc() -> None:
    toc = os.path.join(DOCS, "toc.yml")
    if not os.path.exists(toc):
        fail("toc", "docs/toc.yml 不存在")
        return
    hrefs = re.findall(r"href:\s*(\S+)", read(toc))
    bad = [h for h in hrefs
           if not h.startswith("http") and not os.path.exists(os.path.join(DOCS, h))]
    if bad:
        fail("toc", "href 不可达：%s" % ", ".join(bad))
    else:
        ok("toc", "%d 个条目全部可达" % len(hrefs))


def check_pkg_table() -> None:
    readme_path = os.path.join(ROOT, "README.md")
    text = read(readme_path)
    listed = set(re.findall(r"nuget\.org/packages/([A-Za-z0-9._]+)/", text))
    pkgs = set(package_dirs())
    if listed != pkgs:
        missing = sorted(pkgs - listed)
        extra = sorted(listed - pkgs)
        parts = []
        if missing:
            parts.append("漏列 %d 个：%s" % (len(missing), ", ".join(missing)))
        if extra:
            parts.append("多余 %d 个（无对应工程）：%s" % (len(extra), ", ".join(extra)))
        fail("包表", "；".join(parts))
    else:
        ok("包表", "覆盖 %d/%d 个可发包" % (len(listed & pkgs), len(pkgs)))


def load_sync_module():
    """加载同目录下的 sync-docs-index.py（文件名含连字符，需按路径加载）。"""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sync-docs-index.py")
    spec = importlib.util.spec_from_file_location("sync_docs_index", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def check_index_sync() -> None:
    try:
        mod = load_sync_module()
    except Exception as exc:  # pragma: no cover - 防御性
        fail("index", "无法加载 sync-docs-index.py：%s" % exc)
        return
    expected = mod.derive(read(os.path.join(ROOT, "README.md")))
    index_path = os.path.join(DOCS, "index.md")
    if not os.path.exists(index_path):
        fail("index", "docs/index.md 不存在")
        return
    if read(index_path) != expected:
        fail("index", "docs/index.md 与根 README.md 不一致，请运行 python eng/ci/sync-docs-index.py")
    else:
        ok("index", "docs/index.md 与根 README 一致")


def load_verify_module():
    """加载同目录下的 verify-symbols.py（文件名含连字符，需按路径加载）。"""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "verify-symbols.py")
    spec = importlib.util.spec_from_file_location("verify_symbols", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def check_symbols() -> None:
    """源码出处校验：文档代码块里的符号必须能在源码里找到（抓「编造的 API」）。

    通过 importlib 加载 verify-symbols.py，复用它的索引与比对逻辑，
    避免在 check-docs.py 里重复实现。若 verify-symbols.py 缺失或抛异常，
    降级为「跳过」而非失败——它是增强项，不是既有 12 项的阻断条件。
    """
    try:
        mod = load_verify_module()
        source_index = mod.build_source_index()
    except Exception as exc:  # pragma: no cover - 防御性
        fail("出处校验", "无法加载 verify-symbols.py：%s" % exc)
        return
    dangling_total = 0
    broken_total = 0
    bad_docs = []
    for path, _base in mod._docs_md_files():
        rel = os.path.relpath(path, DOCS)
        dangling, broken = mod.verify_document(path, source_index, strict=False)
        if dangling:
            dangling_total += len(dangling)
            bad_docs.append("%s：%s" % (rel, ", ".join(dangling)))
        if broken:
            broken_total += len(broken)
            bad_docs.append("%s：%s" % (rel, "；".join(broken)))
    if bad_docs:
        fail("出处校验", "%d 处悬空符号 / %d 处断锚：%s"
             % (dangling_total, broken_total, "；".join(bad_docs[:5])))
    else:
        ok("出处校验", "无悬空符号、无断锚（源码事实面对照通过）")


def check_pkg_readmes() -> None:
    missing = []
    no_badge = []
    for name, path in package_dirs().items():
        rp = os.path.join(path, "README.md")
        if not os.path.exists(rp):
            missing.append(name)
            continue
        if "img.shields.io" not in read(rp):
            no_badge.append(name)
    if missing:
        fail("包README", "缺少 README.md（nuget.org 将无说明）：%s" % ", ".join(missing))
    if no_badge:
        fail("包README", "缺少 NuGet 徽章行：%s" % ", ".join(no_badge))
    if not missing and not no_badge:
        ok("包README", "%d 份齐备且均有徽章" % len(package_dirs()))


BAD_PATTERNS = [
    ("NSubstitute", "测试栈是 xUnit + Shouldly + Moq + Coverlet，不是 NSubstitute"),
    ("NPostgreSql.", "PostgreSQL 驱动应为 Npgsql.EntityFrameworkCore.PostgreSQL"),
    ("packages/Bing.AspNetCore.Mvc.Contracts./", "包 URL 末尾多余句点"),
    ("尚未接入 docfx", "docfx 已接入（docs/docfx.json + .github/workflows/docfx.yml）"),
]


def check_bad_patterns() -> None:
    targets = [os.path.join(ROOT, "README.md"), os.path.join(DOCS, "index.md")]
    hits = []
    for tp in targets:
        if not os.path.exists(tp):
            continue
        text = read(tp)
        for pat, why in BAD_PATTERNS:
            if pat in text:
                hits.append("%s 含 `%s`（%s）" % (os.path.relpath(tp, ROOT), pat, why))
    if hits:
        fail("坏模式", "；".join(hits))
    else:
        ok("坏模式", "%d 类历史错误均未回归" % len(BAD_PATTERNS))


# toc.yml 允许的行形态（stdlib-only 的结构校验，不引入 PyYAML 依赖）
# 支持两级：顶层 `- name:` / `  href:` / `  items:`，以及 items 下缩进 4 空格的一组。
TOC_PATTERNS = [
    re.compile(r"^- name: \S"),
    re.compile(r"^  href: \S"),
    re.compile(r"^- href: \S"),
    re.compile(r"^  items:$"),
    re.compile(r"^    - name: \S"),
    re.compile(r"^      href: \S"),
]


def check_toc_structure() -> None:
    """toc.yml 必须只由 `- name:` / `href:` / `items:` 与 `#` 注释组成。

    这条检查是为了拦住一类真实发生过的错误：用 `<!-- -->` 给 YAML 写注释。
    YAML 里 `<!--` 不是注释语法——它会让整个文件解析失败
    （``ScannerError: could not find expected ':'``），docfx 构建随之报错。
    所以这里做纯文本结构校验，不依赖 PyYAML。
    """
    path = os.path.join(DOCS, "toc.yml")
    if not os.path.exists(path):
        fail("toc结构", "docs/toc.yml 不存在")
        return
    bad = []
    for i, line in enumerate(read(path).split("\n"), 1):
        s = line.rstrip()
        if not s.strip() or s.lstrip().startswith("#"):
            continue
        if any(p.match(s) for p in TOC_PATTERNS):
            continue
        bad.append("第 %d 行 %r" % (i, s[:60]))
    if bad:
        fail("toc结构", "非法行（YAML 里不能用 <!-- --> 写注释）：%s" % "; ".join(bad[:5]))
    else:
        ok("toc结构", "全部为 name/href/items 条目或 # 注释")


def check_no_html_comment_in_yaml() -> None:
    """任何 .yml 文件里都不应出现 HTML 注释，否则 YAML 解析会失败。

    只检查**非注释行**——在 `#` 注释里提到 `<!--`（例如说明这条规则本身）是允许的。
    """
    hits = []
    for base in (DOCS, os.path.join(ROOT, ".github", "workflows")):
        if not os.path.isdir(base):
            continue
        for dirpath, _dirnames, filenames in os.walk(base):
            for name in filenames:
                if not name.endswith((".yml", ".yaml")):
                    continue
                fp = os.path.join(dirpath, name)
                for i, line in enumerate(read(fp).split("\n"), 1):
                    stripped = line.lstrip()
                    if stripped.startswith("#"):
                        continue
                    if "<!--" in line:
                        hits.append("%s:%d" % (os.path.relpath(fp, ROOT), i))
    if hits:
        fail("yaml注释", "非注释行含 HTML 注释，会导致 YAML 解析失败：%s" % ", ".join(hits))
    else:
        ok("yaml注释", "所有 .yml 的非注释行均未误用 HTML 注释")


def docs_md_files():
    """docs/ 下全部 md（递归，排除 _site），返回 [(绝对路径, 所在目录)]。"""
    for dirpath, dirnames, filenames in os.walk(DOCS):
        dirnames[:] = [d for d in dirnames if d != "_site"]
        for name in sorted(filenames):
            if name.endswith(".md"):
                yield os.path.join(dirpath, name), dirpath


def check_docs_links() -> None:
    """docs/ 下全部 md 的正文交叉链接（递归，按各文件自身所在目录解析）。"""
    total = 0
    bad = []
    for path, base in docs_md_files():
        if base == DOCS and os.path.basename(path) == "index.md":
            continue  # index.md 由派生保证，另有专项检查
        for link in local_links(read(path)):
            if not link:
                continue
            if link.startswith("{{") or link.endswith("/"):
                continue
            total += 1
            if not os.path.exists(os.path.normpath(os.path.join(base, link))):
                bad.append("%s -> %s" % (os.path.relpath(path, DOCS), link))
    if bad:
        fail("交叉链接", "断链 %d 处：%s" % (len(bad), "; ".join(bad[:10])))
    else:
        ok("交叉链接", "docs 下 %d 条全部可达" % total)


# docs 顶层只允许放「入口类」文件；其余文档必须收进主题子目录。
# 这条检查是为了防止文档重新退化成「几十个 md 平铺在 docs/ 下」。
TOP_LEVEL_ALLOWED_MD = {
    "index.md",        # docfx 首页（由 sync-docs-index.py 派生）
    "README.md",       # 文档导航 hub
    "README.en.md",    # 英文导航
    "CHANGELOG.md",    # 文档体系自身的变更记录
    "ReleaseNotes.md",  # 框架发行说明
}


def check_top_level_layout() -> None:
    stray = sorted(n for n in os.listdir(DOCS)
                   if n.endswith(".md") and n not in TOP_LEVEL_ALLOWED_MD)
    if stray:
        fail("顶层归位", "docs 顶层不应直接放文档，请收进主题子目录：%s" % ", ".join(stray))
    else:
        ok("顶层归位", "docs 顶层仅 %d 个入口类 md" % len(TOP_LEVEL_ALLOWED_MD))


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Bing.NetCore 文档一致性校验")
    parser.add_argument("--quiet", action="store_true", help="只输出失败项")
    args = parser.parse_args(argv)

    check_toc()
    check_toc_structure()
    check_no_html_comment_in_yaml()
    check_links("README", os.path.join(ROOT, "README.md"), ROOT)
    check_links("README.en", os.path.join(ROOT, "README.en.md"), ROOT)
    check_links("docs/README", os.path.join(DOCS, "README.md"), DOCS)
    check_docs_links()
    check_top_level_layout()
    check_pkg_table()
    check_index_sync()
    check_pkg_readmes()
    check_bad_patterns()
    check_symbols()

    if not args.quiet:
        for n in notes:
            print("OK   " + n)
    if failures:
        print("")
        for f in failures:
            print("FAIL " + f)
        print("")
        print("文档校验未通过：%d 项失败" % len(failures))
        return 1
    print("")
    print("文档校验通过：%d 项检查全部通过" % len(notes))
    return 0

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass
    sys.exit(main())
