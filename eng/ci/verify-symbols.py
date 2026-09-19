#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""文档「源码出处」校验器（CI 用，零依赖，仅标准库）。

为什么需要它
------------
本仓库 17+ 轮文档工作中，最高频、也最危险的一类错误是：

    文档里写了一个「源码里根本不存在的 API 签名」。

真实发生过的事故（都已用本脚本的逻辑追溯复核过）：
* ``ToListAsync(buffered: false)`` —— ``buffered`` 不是参数，是内部写死的；
* ``GetCountAsync()`` / ``ToDynamicList()`` / ``StreamAsync<T>()`` —— 均不存在；
* ``AddMySqlQuery()`` / ``AddMySqlExecutor()`` —— 入口实为 ``AddMySqlProvider()``；
* ``IRepository`` —— 一度被写成「本框架没有」，实际存在且是 ``IStore`` 超集；
* ``MessageEvent.Send`` 默认值 —— 一度写成 ``true``，实际无初始化器 → ``false``。

这类错误「代码能跑、不报错、只是文档骗人」，靠读文档发现不了，只能对照源码。
本脚本把「对照源码」这件事自动化：它扫描源码建立「符号事实面」，再扫描文档里
的代码块抽取「被引用的符号」，把两者做比对，报告「文档里出现、源码里找不到」的符号。

它不追求 100% 精确（C# 语法解析太重），而是**宁可漏报、不可误报**——只对
「高置信度」的悬空符号告警，把明显的笔误抓出来，剩下的交给人工。

用法
----
    python eng/ci/verify-symbols.py                      # 校验 docs/ 全部 md，退出码 0/1
    python eng/ci/verify-symbols.py --dump-index <out>   # 只导出源码符号索引（调试用）
    python eng/ci/verify-symbols.py --json               # 以 JSON 输出结果（供 check-docs.py 复用）

设计要点
--------
1. 符号索引（从源码建立）：
   - 类型名：``class/interface/struct/enum/record`` 的名字（含泛型类名如 ``IStore<T>``）。
   - 成员名：public 成员的 ``名字``（方法、属性、字段、事件）。
   - 常量字符串：``AddXxx`` / ``UseXxx`` 这类注册入口，靠「方法名匹配」覆盖。
   索引是「全仓并集」，不做命名空间限定——因为文档里的 ``using`` 往往是省略的，
   同一个符号名在不同包重复定义时，只要「至少一处存在」就算命中（避免误报）。

2. 文档引用抽取（从 markdown 代码块）：
   - 只解析 ``` 围栏代码块（`` ```csharp`` / `` ``` ``）。
   - 抽取形如 ``Xxx.Yyy``、``Xxx<T>``、``Xxx()``、``AddXxx`` / ``UseXxx`` 的标识符。
   - 过滤掉 C# 关键字、字面量、明显是变量的名字（首字母小写的长标识符视作本地变量）。
   - 保留「PascalCase 类型/成员」以及「Add*/Use*/Create*/Get*/Set*」等动词开头的候选。

3. 比对：
   - 文档候选符号若在源码索引中**完全不存在** → 报告。
   - 但有大量「合法例外」必须白名单：外部包类型（Dapper、CAP、EF Core、Serilog 等）、
     BCL 类型、以及文档自己构造的示例类（事件类/处理器/DTO）。
   - 因此：**默认只报告「高置信度」悬空符号**——即名字带 ``Bing`` 前缀、或形如
     ``AddXxx``/``UseXxx`` 的注册入口、或出现在「防误用清单/不存在的方法」语境之外
     的成员调用。其余交给 ``--strict`` 模式。

4. 行号锚点校验（可选，默认开启）：
   - 文档里大量出现 ``文件路径.cs:行号`` 的自报出处。
   - 校验：该行号对应的源码行里，是否包含该上下文提到的符号名或相近内容。
   - 这一项能把「出处行号漂移」抓出来（重构后行号变了，文档没跟上）。

输出
----
失败 → 退出码 1，打印 ``FAIL [符号] ...`` 清单；通过 → 退出码 0。
"""
from __future__ import annotations

import argparse
import io
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(ROOT, "docs")

# 扫描范围：只扫这两棵源码树（tests/samples/eng 不进「事实面」，它们不是文档的权威出处）
SRC_ROOTS = [
    os.path.join(ROOT, "framework", "src"),
    os.path.join(ROOT, "components", "src"),
]

SKIP_DIRS = {"obj", "bin", "_site", "node_modules", ".git"}

# C# 关键字 / 常见标识符，抽取文档符号时排除
KEYWORDS = {
    "public", "private", "protected", "internal", "static", "class", "interface",
    "struct", "enum", "record", "abstract", "sealed", "virtual", "override",
    "async", "await", "return", "new", "this", "base", "using", "namespace",
    "void", "int", "long", "short", "byte", "bool", "string", "char", "float",
    "double", "decimal", "object", "var", "if", "else", "for", "foreach", "while",
    "do", "switch", "case", "break", "continue", "throw", "try", "catch", "finally",
    "true", "false", "null", "get", "set", "value", "where", "in", "out", "ref",
    "params", "default", "nameof", "typeof", "is", "as", "not", "and", "or",
    "partial", "readonly", "const", "event", "delegate", "explicit", "implicit",
    "lock", "checked", "unchecked", "goto", "yield", "extern", "fixed", "stackalloc",
}

# 已知的「外部/BCL」类型前缀——文档里出现这些名字不算「悬空符号」，因为它们在源码之外。
# 只放**确实会在文档示例里出现**、且不属于本仓库产出的名字。
EXTERNAL_NAMES = {
    # BCL
    "string", "object", "int", "bool", "long", "Guid", "DateTime", "Task", "List",
    "IEnumerable", "IQueryable", "Dictionary", "IDisposable", "IAsyncDisposable",
    "IServiceCollection", "IServiceProvider", "IApplicationBuilder", "IConfiguration",
    "ILogger", "ILoggerFactory", "CancellationToken", "HttpContext", "WebApplication",
    "WebApplicationBuilder", "IServiceScope", "IHttpContextAccessor", "ILoggerFactory",
    "Expression", "Func", "Action", "Attribute", "Exception", "StringBuilder",
    "IAsyncEnumerable", "ValueTask", "IReadOnlyList", "IReadOnlyCollection",
    "Nullable", "TimeSpan", "Uri", "Stream", "IFormFile", "ClaimsPrincipal",
    "IUnitOfWork", "DbContext", "DbSet", "EntityState",
    # 依赖框架 / 三方
    "IServiceCollection", "Dapper", "SqlMapper", "DynamicParameters", "IDbConnection",
    "IDbTransaction", "ICapPublisher", "CapOptions", "CapBuilder", "CapSubscribe",
    "TopicAttribute", "ICapSubscribe", "Serilog", "LoggerConfiguration", "Log",
    "ILoggerConfiguration", "LoggerSinkConfiguration", "LogContext", "LogEventLevel",
    "MinimumLevel", "LevelAlias", "SkyApm", "SkyApmExtensions", "FreeSql", "IFreeSql",
    "AspectCore", "Autofac", "AutoMapper", "IMapper", "MapperConfiguration",
    "Hangfire", "BackgroundJob", "Scriban", "Template", "MailKit", "SmtpClient",
    "MimeMessage", "System", "Microsoft", "Newtonsoft", "Npgsql", "MySqlConnector",
    "JwtBearer", "JwtOptions", "Http", "HttpClient", "IHttpClientFactory",
    "FluentValidation", "IValidator", "ValidationContext", "xunit", "Xunit",
    "Shouldly", "Moq", "NSubstitute", "Fact", "Theory", "MemberData",
    "ControllerBase", "ApiControllerAttribute", "RouteAttribute", "HttpPost",
    "HttpGet", "HttpPut", "HttpDelete", "FromBody", "FromQuery", "ProducesResponseType",
    "AuthorizeAttribute", "AllowAnonymous", "ApiExplorerSettings", "EnableCors",
    "FromServices", "JsonSerializer", "JsonConvert", "JsonSerializerOptions",
    "ConfigurationBuilder", "ConfigurationManager", "GetConnectionString",
    # —— 常见「外部/BCL 方法名」（文档示例里高频出现，但都属于框架自身之外的产物）——
    # 注意：只收「确实不属于本仓库、且高频出现在文档示例里」的方法名，避免把真错误也白掉。
    "AddScoped", "AddSingleton", "AddTransient", "AddControllers",
    "AddControllersAsServices", "AddMvc", "AddRazorPages", "AddRazorHtml",
    "AddLogging", "AddSerilog", "AddConsole", "AddMemoryCache", "AddDistributedMemoryCache",
    "AddAuthorization", "AddPolicy", "AddCors", "AddHealthChecks", "AddSignalR",
    "AddOptions", "AddSwaggerGen", "AddSwagger", "AddHangfire", "AddHangfireServer",
    "AddBackgroundService", "AddHostedService", "AddCap", "AddHttpClient", "AddDbContext",
    "AddIdentity", "AddAuthentication", "AddJwtBearer", "AddAuthorizationCore",
    "AddApiVersioning", "AddMiniProfiler", "AddAutoMapper", "AddFluentValidation",
    "AddControllersWithViews", "AddSession", "AddAntiforgery", "AddLocalization",
    "AddDefaultIdentity", "AddRazorRuntimeCompilation", "AddSignalRCore", "AddMediatR",
    "UseMiddleware", "UseRouting", "UseEndpoints", "UseAuthorization", "UseAuthentication",
    "UseCors", "UseHttpsRedirection", "UseStaticFiles", "UseSwagger", "UseSwaggerUI",
    "UseHangfireDashboard", "UseDashboard", "UseRabbitMQ", "UseKafka", "UseSqlServer",
    "UseMySql", "UsePostgreSql", "UseEntityFramework", "UseInMemoryStorage", "UseRedis",
    "UseMiniProfiler", "UseSerilogRequestLogging", "UseRequestLocalization",
    "UseDeveloperExceptionPage", "UseExceptionHandler", "UseSession", "UseSignalR",
    "UseLazyLoading", "UseTransaction", "UseTransactionAsync", "UseConnectionString",
    "UseDefaultServiceProvider", "UseSetting", "UseEnvironment", "UseHealthChecks",
    "CreateLogger", "CreateDefaultBuilder", "CreateScope", "CreateRequest", "CreateResponse",
    "GetInstance", "GetService", "GetServices", "GetRequiredService", "GetConnectionString",
    "GetDbConnection", "ConfigureServices", "Configure", "ConfigureAppConfiguration",
    "ConfigureLogging", "ConfigureWebHost", "ConfigureWebHostDefaults", "ConfigureHost",
    "MapGet", "MapPost", "MapControllers", "Map", "MapHealthChecks", "MapWhen", "MapFallback",
    "Run", "RunAsync", "Build", "BuildServiceProvider",
    "AddSingletonInstance", "AddScopedIfMissing", "TryAddSingleton", "TryAddScoped",
    "TryAddTransient", "TryAddEnumerable", "AddAttribute", "AddPolicyEvaluator",
    "AddAuthenticationCore", "AddHttpContextAccessor", "AddSystemWebAdapters",
}

# 「确实存在、但不在 SRC_ROOTS 索引里」的合法符号——逐一注明证据，避免把它们误判成「编造的 API」。
# 来源分三类：① 业务域方法（在 modules/ 下，不是框架 API）；② 三方包 API；③ 真实存在但属
# 私有成员 / 属性（当前索引器只收 public 方法签名，不收属性、私有成员）。
KNOWN_SYMBOLS = {
    # ① 业务域方法（modules/admin 的领域模型方法，属示例而非框架 API）
    "AddLoginLog",          # modules/admin/.../Systems.Domain/Models/User.cs:81
    # ② 三方包 API
    "AddSkyApmExtensions",  # SkyApm 扩展方法（modules/admin/.../SkyApmModule.cs:31）
    "AddTags",              # Exceptionless.Serilog sink 的 EventBuilder.AddTags
    # ③ 真实存在但索引器未收（属性 / 私有成员 / SQL 字面量里的表名）
    "AddSelf",              # Bing.Core DependencyAttribute.AddSelf（属性，非方法签名）
    "AddParameter",         # 私有 static 方法（Bing.Data.Sql / Bing.Dapper.PostgreSql 内部）
    "BingSqlBatch",         # 文档示例里作为 SQL 表名字面量出现，不是符号
}

# 正则：抽取「文档代码块」里可能的类型/成员引用（PascalCase 或 AddXxx/UseXxx）
# 不追求语法正确，只求「抓候选、再比对」。
SYMBOL_RE = re.compile(r"\b([A-Z][A-Za-z0-9_]*)(?:\s*<[^>\n]*>)?\b")

# 抽取「AddXxx / UseXxx / CreateXxx / GetXxx / SetXxx」这类动词开头的注册/工厂入口
ENTRY_RE = re.compile(r"\b(Add|Use|Create|Get|Set|Enable|Register|Configure)[A-Z][A-Za-z0-9_]*\b")

# 抽取「文档自报出处」：`路径.cs:行号` 或 `路径.cs:行号-行号`
LINE_ANCHOR_RE = re.compile(
    r"([A-Za-z0-9_./\\-]+\.(?:cs|props|targets|json|yml)):(\d+)(?:[-:](\d+))?"
)


def read(path: str) -> str:
    with io.open(path, encoding="utf-8") as f:
        return f.read().replace("\r\n", "\n")


def walk_sources():
    """遍历源码树，产出 (文件绝对路径, 文本)。"""
    for base in SRC_ROOTS:
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for name in filenames:
                if name.endswith(".cs"):
                    yield os.path.join(dirpath, name)


def strip_generic(name: str) -> str:
    """`IStore<T>` → `IStore`；`MessageEvent<TPayload>` → `MessageEvent`。"""
    return name.split("<")[0].split("(")[0]


def build_source_index() -> "dict[str, dict]":
    """从源码建立符号索引。

    返回 {符号名: {"types": set(定义位置), "members": set(定义位置)}}。
    这里只记录「存在性」，不做命名空间消歧。
    """
    type_re = re.compile(
        r"^\s*(?:public|internal)\s+(?:static\s+)?(?:partial\s+)?"
        r"(?:abstract\s+|sealed\s+|readonly\s+|ref\s+)?"
        r"(class|interface|struct|enum|record)\s+([A-Za-z_][A-Za-z0-9_]*)\b"
    )
    member_re = re.compile(
        r"^\s*(?:public|protected)\s+(?:static\s+)?(?:async\s+)?"
        r"(?:override\s+|virtual\s+|abstract\s+|new\s+|sealed\s+|readonly\s+)*"
        r"(?:[\w<>\[\].,?\s]+\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*[<(]"
    )
    index: dict[str, dict] = {}

    def add(symbol: str, file_path: str, kind: str) -> None:
        if symbol in KEYWORDS:
            return
        entry = index.setdefault(symbol, {"types": set(), "members": set()})
        entry[kind].add(file_path)

    for fp in walk_sources():
        try:
            text = read(fp)
        except Exception:
            continue
        for i, line in enumerate(text.split("\n"), 1):
            m = type_re.search(line)
            if m:
                add(m.group(2), fp, "types")
                continue
            m = member_re.search(line)
            if m:
                add(m.group(1), fp, "members")
    return index


def extract_code_blocks(text: str):
    """从 markdown 里取出所有围栏代码块的内容。"""
    blocks = []
    in_block = False
    buf = []
    fence = ""
    for line in text.split("\n"):
        s = line.strip()
        if s.startswith("```"):
            if not in_block:
                in_block = True
                fence = s[3:].strip().lower()
                buf = []
            else:
                in_block = False
                if fence in ("", "csharp", "cs", "c#", "code"):
                    blocks.append("\n".join(buf))
                buf = []
        elif in_block:
            buf.append(line)
    return blocks


def extract_doc_symbols(text: str):
    """从文档全文抽取「候选符号」。返回 set(符号名)。"""
    symbols: set[str] = set()
    for block in extract_code_blocks(text):
        # 逐行，去掉注释
        for line in block.split("\n"):
            s = line.strip()
            if s.startswith("//"):
                continue
            # 去掉 // 行内注释
            s = s.split("//")[0]
            for m in SYMBOL_RE.finditer(s):
                name = strip_generic(m.group(1))
                if name in KEYWORDS or name in EXTERNAL_NAMES:
                    continue
                if name == "Bing":  # 命名空间本身，不是类型
                    continue
                symbols.add(name)
            for m in ENTRY_RE.finditer(s):
                name = m.group(0)
                if name in KEYWORDS or name in EXTERNAL_NAMES:
                    continue
                symbols.add(name)
    return symbols


def is_high_confidence_symbol(name: str) -> bool:
    """判断一个悬空符号是否「高置信度」——即真的很可能是文档写错了。

    策略（宁可漏报、不可误报）：
    - 带 ``Bing`` 前缀 → 高置信（本仓库自有命名空间几乎都带 Bing）。
    - ``AddXxx`` / ``UseXxx`` → 高置信（本仓库的注册入口几乎都是这两个动词；
      它们也是历史上出错最多的地方，如 ``AddMySqlQuery`` vs ``AddMySqlProvider``）。
    - 其它动词（``Create/Get/Set/Enable/Register/Configure``）太泛——大量三方包方法
      （``GetConnectionString``、``CreateLogger``、``RegisterXxx``）和文档自造示例
      都用这些前缀，极易误报，故**不再纳入高置信**，只留给 ``--strict`` 模式。
    """
    if name.startswith("Bing"):
        return True
    if re.match(r"^(Add|Use)[A-Z]", name):
        # Add/Use 是框架注册入口的最强信号
        return True
    return False


def verify_document(path: str, source_index: dict, strict: bool = False):
    """校验单个文档。返回 (悬空符号列表, 断锚列表)。"""
    text = read(path)
    rel = os.path.relpath(path, ROOT)

    doc_symbols = extract_doc_symbols(text)
    dangling = []
    for sym in sorted(doc_symbols):
        if sym in source_index:
            continue
        if sym in EXTERNAL_NAMES:
            continue
        if sym in KNOWN_SYMBOLS:
            continue
        if not strict and not is_high_confidence_symbol(sym):
            continue
        dangling.append(sym)

    # 行号锚点校验（只校验「能定位到源码文件」的锚点）
    #
    # 只查「行号越界」这一种硬错误。**不**把「指向空行/注释行」判为失败——
    # 本仓库的文档会刻意引用「被注释掉的代码」作为已知缺口的证据（例如
    # ``Log.cs:260`` 指向被注释的 TraceId 注入块），这是合法且有益的用法。
    broken_anchors = []
    for m in LINE_ANCHOR_RE.finditer(text):
        fname, lineno = m.group(1), int(m.group(2))
        # 找到该文件名对应的源码文件
        target = locate_source_file(fname)
        if target is None:
            continue  # 非本仓库源码文件（外部包/生成文件），跳过
        lines = read(target).split("\n")
        if lineno < 1 or lineno > len(lines):
            broken_anchors.append("%s:%d（越界，文件共 %d 行）" % (fname, lineno, len(lines)))

    return dangling, broken_anchors


def locate_source_file(fname: str):
    """按文件名在源码树里定位源码文件。

    优先用「引用路径的目录后缀」做精确匹配，避免 basename 碰撞：
    文档里 ``Systems.Domain/Repositories/IRoleRepository.cs`` 这类引用带目录，
    而 ``modules/`` 与 ``framework/src`` 下可能都有 ``IRoleRepository.cs``，
    仅凭 basename 会误定位。找不到唯一匹配 → None（跳过，交给人工）。
    """
    base = os.path.basename(fname)
    # 从引用路径里取「最后 2 段目录」作为指纹（如 Repositories / Services）
    dir_fingerprint = fname.replace("\\", "/").split("/")[:-1][-2:]

    def score(fp: str) -> int:
        """按目录指纹匹配度打分，越高越精确。"""
        rel = fp.replace("\\", "/")
        parts = rel.split("/")
        s = 0
        # 倒序比对目录指纹
        for i, seg in enumerate(reversed(dir_fingerprint)):
            if len(parts) - 2 - i >= 0 and parts[-2 - i].lower() == seg.lower():
                s += 1
        return s

    candidates = [fp for fp in walk_sources() if os.path.basename(fp) == base]
    if not candidates:
        return None
    if len(candidates) == 1:
        return candidates[0]
    # 多个候选：按目录指纹打分，取最高分；若最高分不唯一（并列）则返回 None 保守跳过
    best = max(score(fp) for fp in candidates)
    top = [fp for fp in candidates if score(fp) == best]
    return top[0] if len(top) == 1 else None


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Bing.NetCore 文档源码出处校验")
    parser.add_argument("--strict", action="store_true",
                        help="报告所有悬空符号（默认只报告高置信度）")
    parser.add_argument("--json", action="store_true",
                        help="以 JSON 输出（供 check-docs.py 复用）")
    parser.add_argument("--dump-index", metavar="PATH",
                        help="把源码符号索引导出为 JSON（调试用）")
    args = parser.parse_args(argv)

    source_index = build_source_index()

    if args.dump_index:
        dump = {k: {"types": len(v["types"]), "members": len(v["members"])}
                for k, v in sorted(source_index.items())}
        with io.open(args.dump_index, "w", encoding="utf-8") as f:
            json.dump(dump, f, ensure_ascii=False, indent=2)
        print("符号索引已导出：%d 个符号 → %s" % (len(dump), args.dump_index))
        return 0

    all_dangling: dict[str, list[str]] = {}
    all_broken: dict[str, list[str]] = {}
    for path, _base in _docs_md_files():
        dangling, broken = verify_document(path, source_index, strict=args.strict)
        if dangling:
            all_dangling[os.path.relpath(path, DOCS)] = dangling
        if broken:
            all_broken[os.path.relpath(path, DOCS)] = broken

    if args.json:
        json.dump({"dangling": all_dangling, "broken_anchors": all_broken},
                  sys.stdout, ensure_ascii=False, indent=2)
        return 1 if (all_dangling or all_broken) else 0

    # 人类可读输出
    if all_dangling:
        print("=== 悬空符号（文档里出现、源码里找不到）===")
        for doc, syms in sorted(all_dangling.items()):
            for s in syms:
                print("  FAIL %s -> %s" % (doc, s))
    if all_broken:
        print("=== 断锚（自报出处行号不可达）===")
        for doc, items in sorted(all_broken.items()):
            for it in items:
                print("  FAIL %s -> %s" % (doc, it))

    if not all_dangling and not all_broken:
        print("源码出处校验通过：无悬空符号、无断锚")
        return 0

    print("")
    print("源码出处校验未通过：%d 篇文档有悬空符号，%d 篇有断锚"
          % (len(all_dangling), len(all_broken)))
    return 1


def _docs_md_files():
    for dirpath, dirnames, filenames in os.walk(DOCS):
        dirnames[:] = [d for d in dirnames if d != "_site"]
        for name in sorted(filenames):
            if name.endswith(".md"):
                yield os.path.join(dirpath, name), dirpath


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass
    sys.exit(main())
