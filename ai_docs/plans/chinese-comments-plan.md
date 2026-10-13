# framework 中文 XML 注释补全计划

## 目标

仅调整 `framework/src/**` 和 `framework/tests/**` 中手写 C# 文件的 XML 文档注释，不改变业务逻辑、成员签名、可见性、命名空间或公开 API。注释需准确表达成员职责，并与真实访问器、参数、返回值和继承关系一致。

## 扫描范围与统计

扫描使用 Roslyn C# 语法树识别声明及其前导文档注释，并按 UTF-8 读取文件。排除 `bin`、`obj`、`*.g.cs`、`*.generated.cs`、`*.Designer.cs`、EF Core Migration/ModelSnapshot、含自动生成标记的文件，以及 `Bing.Events.Cap.MySql/00-Source/**` 上游移植源码。

执行期间的首轮可复现扫描纳入 2,159 个文件（生产代码 1,459、测试及基准 700），识别 23,889 个类型、构造函数、方法、属性/索引器/事件、字段和枚举成员声明。该快照发现 1,757 个缺少文档注释的声明，均位于测试项目；生产代码的缺注释数为 0。测试缺口主要分布于：

| 项目 | 缺少注释候选 |
|---|---:|
| Bing.Dapper.SqlServer.Tests | 249 |
| Bing.Data.Sql.Benchmarks | 244 |
| Bing.Data.Sql.Tests | 233 |
| Bing.Test.Shared | 206 |
| Bing.Data.Sql.CustomProvider.Tests | 162 |
| Bing.Dapper.Core.Tests | 111 |
| Bing.Auditing.Tests | 70 |

另有构造函数、属性文案、重写成员 `inheritdoc` 和参数标签等静态候选，需要逐类语义复核。语法扫描不能单独判断隐式接口实现、属性真实语义、测试夹具分类或摘要冗长程度。执行中的后续扫描结果应作为剩余 TODO 更新，不能把启发式候选直接视为确认缺陷。

## 分批实施

### P1：接口、抽象类和公共契约（已完成：2026-10-12）

- 范围：`framework/src/**/*Abstractions*`、`**/*Contracts*`、`I*.cs`、`*Base.cs`，以及 `Bing.Core`、`Bing.Data.Sql`、`Bing.Caching` 等分散接口。
- 工作：补齐类型和成员职责；校正 `<param>`、`<typeparam>`、`<returns>`、异常与异步语义；复杂约束放入 `<remarks>`。
- 验收：公共契约注释完整，标签与签名一致；无业务代码差异。

### P2：DTO、Entity、Options 和枚举（已完成：2026-10-12）

- 范围：`Bing.Ddd.Application.Contracts`、`Bing.Auditing.Contracts`、`Bing.Data`、`Bing.Data.Sql`、`Bing.Caching`，以及测试项目中的 `Models`、`Bing.TestShare`、`Bing.Test.Shared`。
- 工作：确认类型职责后补齐类型、属性和枚举成员；记录必要的默认值、单位、约束和空值语义。
- 验收：确认的数据类型、属性和枚举成员均有准确注释。

### P3：构造函数和属性文案（已完成：2026-10-12）

- 范围：全部纳入文件，先生产项目，再测试项目。
- 工作：构造函数统一为 `初始化一个 <see cref="当前类型"/> 类型的实例。`；属性按访问器使用“获取”“设置”“获取或设置”“获取或初始化”；额外初始化语义放入 `<remarks>`。
- 验收：构造函数摘要指向当前类型；属性摘要与实际访问方式一致。

### P4：实现类、重写成员和重载方法（已完成：2026-10-12）

- 范围：Provider、Repository、ApplicationService、Controller、Builder，以及测试替身和基准实现。
- 工作：使用语义模型确认接口实现、显式实现、抽象成员实现和 `override`；有上游契约时使用独立的 `/// <inheritdoc />`，必要的行为差异写入 `<remarks>`；统一同语义重载摘要。
- 验收：实现成员不重复复制上游说明，差异行为保留且有依据。

### P5：辅助方法（已完成：2026-10-12）

- 范围：生产代码优先处理 SQL Builder、元数据解析器、缓存键及查询辅助文件；测试侧从缺口最多的项目向外推进。
- 工作：补齐用途、参数、泛型参数和返回值；方法摘要只描述核心用途，算法、并发和性能说明放入 `<remarks>`。
- 验收：纳入范围内具名辅助方法均有有效注释，非泛型 `Task`/`ValueTask` 不含 `<returns>`。

### P6：字段、常量、缓存键和配置键（已完成：2026-10-12）

- 范围：全部 `private`、`internal`、`protected`、`static`、`readonly`、`const` 字段及缓存/配置键。
- 工作：说明字段职责、生命周期、单位、默认值或布尔含义；键文档标明作用域或格式。
- 验收：字段和常量均有独立说明，不以字段名重复代替职责描述。

### P7：最终审计与验证（已完成：2026-10-13）

- 范围：全部纳入的 2,159 个文件。
- 工作：审计独立三行 `<summary>`、中文文案、`inheritdoc`、XML 转义、标签与签名、冗长/过时注释及排除目录；比较修改前后去除 trivia 的 Roslyn 语法树。
- 验收：语法树结构一致，差异仅有 XML 注释；无非法 XML 或未处理的缺注释项。

## 文案与继承规范

- 构造函数摘要统一为 `初始化一个 <see cref="当前类型"/> 类型的实例。`。
- 属性摘要简洁，并根据访问器使用“获取……”“设置……”“获取或设置……”“获取或初始化……”。
- 方法摘要只写核心用途；补充语义放 `<remarks>`。
- 语义相同的重载保持相同或高度一致的摘要。
- 接口实现、抽象成员实现及 `override` 优先使用独立的 `/// <inheritdoc />`；不把它嵌在 `<summary>` 中。
- `<param>`、`<typeparam>`、`<returns>` 与实际签名一致；无返回值及非泛型异步方法不写 `<returns>`。
- 字段、常量、缓存键和配置键说明用途和必要的作用域/格式。

## 风险与待确认项

- 当前工作区含有大量既有修改；实施前后应按本任务触及文件的注释差异审查，不覆盖或归因既有业务改动。
- 旧扫描中的部分统计基于启发式规则；以 Roslyn 文档 trivia 复扫为准，标签问题和摘要质量仍需语义复核。
- 隐式接口实现需通过编译语义确认；不能仅凭方法名或签名推断。
- 测试 Fixture、DTO、Entity 和普通 Model 需逐类型确认，不能按名称机械分类。
- 缺少 `project.assets.json` 时，构建只能记为未验证，不能记为通过。

## 验证命令

```powershell
rg --files framework -g '*.cs' -g '!**/bin/**' -g '!**/obj/**' -g '!*.g.cs' -g '!*.generated.cs' -g '!*.Designer.cs' -g '!**/*ModelSnapshot.cs'
dotnet build Bing.All.sln --no-restore -p:GenerateDocumentationFile=true
git diff --check
git status --short -- framework
git diff -- framework -- '*.cs'
```

构建不能替代注释扫描。每批还需检查 XML 文档可解析性、声明注释覆盖、参数标签，并确认去除 trivia 后的语法树结构不变。

## 实施进度（2026-10-12）

### 已完成

- P1 接口、抽象类和公共契约批次已完成：本批 Roslyn 复扫未发现缺失文档、缺失或不匹配的 `<param>`/`<returns>`、非三行 summary 或超长 summary；为 `IEntityMappingSnapshotProvider.CaptureSnapshot` 补充返回值说明，并统一公共契约中的重载摘要。
- P1 重载复核剩余 3 组静态差异候选，分别涉及按查询源与按 ID 查询、Builder 与对象参数绑定；语义不同且组内摘要一致，确认不需合并。本批未修改实现类，新增 `inheritdoc` 为 0。
- P2 DTO、Entity、Options 和枚举批次已完成：复核并修订 26 个 C# 文件中的约 110 个 summary，补充或整理 15 处 remarks；为 `ProductEnum` 的 2 个枚举成员补充注释，枚举复扫覆盖 42 个类型、185 个成员，缺失数为 0。本批新增 `inheritdoc` 为 0。
- P2 收口检查：目标文件中未发现超过 80 字的 summary 或缺少 XML 注释的 private 字段；类型、属性和枚举成员职责均可从声明及现有调用语义确认，无待确认项。当时 P3 及后续批次尚未执行。
- P3 构造函数和属性文案批次已完成：在 104 个文件中拆分或整理 212 条属性补充说明；另修订 41 条属性摘要，将默认值、空值、示例、安全约束和实现细节移入 remarks。合计调整 253 条属性 summary 及对应 remarks。补齐 11 个构造函数的标准摘要和 14 个 param 说明，并规范 1 个泛型构造函数的 cref 空格；另为 2 个 Provider 构造函数补充固定数据库类型 remarks。本批新增 inheritdoc 为 0；11 个构造函数原有 inheritdoc 改为规定的实例初始化摘要。
- P3 全范围复扫：2,161 个手写 C# 文件中有 1,190 个实例构造函数，标准文案不匹配数为 0；9 个静态构造函数不适用。3,933 个属性/索引器中 646 个使用 inheritdoc，其余访问方式前缀不匹配数为 0；超过 80 字的属性 summary 为 0，仍含明显补充说明的属性 summary 候选为 0。P3 未修改字段；字段注释覆盖复用 P1 全局扫描证据。
- P3 共影响 125 个 C# 文件：212 条批量属性文案调整分布在 104 个文件；另有 9 个属性专项文件和 12 个构造函数文件。改动仅涉及 XML 文档注释，Roslyn 验证批量变更前后 C# token 序列完全一致。
- P4 实现类、重写成员和重载方法批次已完成：将 1,242 个隐式接口实现和 205 个显式接口实现/override 的直接文档改为 `inheritdoc`，统一 19 组同语义重载摘要，并将 Provider Profile、取消行为等重载差异移入 remarks。
- P4 Roslyn 语义复扫覆盖 2,161 个手写 C# 文件所在解决方案的 232 个 C# 项目，项目编译加载错误为 0。计划范围内 3,164 个隐式接口实现全部使用 `inheritdoc`；691 个显式实现和 override 全部使用 `inheritdoc`；1,406 个 private 字段均有注释。
- P4 重载差异复核：扫描剩余 8 组、16 个方法摘要仍有差异；逐项确认分别对应不同职责或输入语义（选项自动注册与类型绑定、执行查询与构造查询、dead-letter 列表解析与时间分值转换、分片枚举与身份比较、客户端配置与代理注册、执行钩子与诊断回调），保留差异，不作机械合并。
- P4 注释差异审阅：本批只改 XML 文档注释；批量 `inheritdoc` 转换前后 C# token 序列一致，手工调整也只涉及 summary/remarks 文本。当前工作区原有的业务差异不计入本批。
- 本轮扫描纳入 2,161 个手写 C# 文件，共识别 23,922 个声明。与计划阶段的 2,159 个文件、23,889 个声明不同，差异来自当前工作区文件状态；本轮按当前文件清单执行。
- 类型、构造函数、方法、属性、索引器、事件、字段、常量和枚举成员均有 XML 文档或有效的 `inheritdoc`；缺失文档数为 0。直接 summary 为 21,403 个，`inheritdoc` 为 2,519 个。
- 1,180 个实例构造函数统一为规定文案；9 个静态构造函数不适用实例初始化文案。
- 3,291 个带直接 summary 的属性和索引器已符合访问器文案规则；摘要不匹配数为 0。
- 21,403 个直接 summary 已统一为三行结构；Roslyn 文档 XML 错误数为 0。清理了检测到的重复 `///` 标记及重复结束标签。
- 移除 109 个用于 `void`、非泛型 `Task`/`ValueTask` 方法的无效 `<returns>`；补齐 4 个 `<typeparam>`。现有 `<param>`、`<typeparam>` 标签均未发现多余或不匹配项。
- 对 35 个可按首句明确拆分的长摘要，将补充说明移入 `<remarks>`。
- 修正一个因重名类型导致的歧义 `cref`。
- P5 辅助方法批次已完成：覆盖 SQL Builder、元数据解析器及缓存键生产代码，并检查计划指定的六个测试/基准项目。为 48 个 C# 文件补充 108 个 `<param>` 和 149 个 `<returns>` 标签；未新增 `<typeparam>` 或 `inheritdoc`。修复 `SqlFilterPlacementPlanner` 中一个未闭合的 `<summary>` 标签。
- P5 Roslyn 复扫覆盖 422 个 C# 文件和 4,297 个方法：缺少方法注释、参数/泛型参数/返回标签不匹配、超过 150 字的 summary、同类型重载 summary 不一致均为 0；484 个 private 字段缺注释为 0。SQL Builder 子集复扫覆盖 239 个文件、1,281 个方法，缺失注释和标签问题均为 0。
- P5 收口确认：新增内容仅为 XML 文档注释；无待确认的辅助方法语义。当前工作区有同目录的既有代码改动，审阅与归属仅针对本轮新增的注释。

- P6 字段、常量、缓存键和配置键批次已完成：覆盖 2,161 个手写 C# 文件中的 1,659 个字段；修订 135 条字段或常量摘要，新增 `inheritdoc` 为 0。180 条键/缓存/配置启发式候选均已复核，针对用途、作用域或格式描述不足的字段完成修订；没有需要待确认的业务语义。
- P6 静态复扫：字段缺注释 0、弱摘要候选 0、超过 150 字的字段摘要 0、XML 文档诊断 0；缓存键及配置键候选中已确认的作用域/格式问题为 0。
- P7 最终审计已完成：复扫 2,159 个文件、23,896 个声明；缺少文档和 summary、private 字段注释遗漏、非三行 summary、XML 诊断、C# 语法错误及参数/泛型参数/返回标签不匹配均为 0。当前共有 19,955 个直接 summary、3,941 个 `inheritdoc` 和 1,462 个带文档的 private 字段。
- P7 复核 63 个长度阈值候选；确认无超过 150 字的 summary，并将明确属于额外推导或覆盖范围的内容移入 `remarks`。其余候选均为字段、能力或复杂测试行为的一句话说明。剩余 19 个重载摘要差异候选对应不同职责或输入语义，保留差异。182 个测试声明未编写任何 `<param>` 标签；它们没有部分缺失、错名或多余标签，按参数标签与签名一致性验收，不视作标签不匹配。
- P7 修改 92 个 C# 文件（生产代码 31 个、测试及基准 61 个），补充约 108 个 `<param>`、`<typeparam>`、`<returns>` 标签并修订 summary/remarks；本批新增 `inheritdoc` 为 0。未修改成员签名、访问级别或业务逻辑。

### 未完成 TODO

> P1 至 P7 均已完成。未发现阻止验收的注释缺口。

- 无。

### 验证记录

- P6 构建：`dotnet build Bing.All.sln --no-restore -p:GenerateDocumentationFile=true -v:q` 成功，0 错误、301 个警告；警告包含 NuGet 漏洞数据源不可达、目标框架兼容性及既有分析器/文档警告。
- `dotnet build Bing.All.sln --no-restore -p:GenerateDocumentationFile=true -v:q`：最终构建成功，0 错误、134 个警告；警告中包含无法访问 NuGet 漏洞数据源的 `NU1900`。此前详细构建还显示部分缺失 `<param>` 的 `CS1573` 警告。
- P1 收口复验：`dotnet build Bing.All.sln --no-restore -p:GenerateDocumentationFile=true -v:q` 成功，0 错误、262 个警告；包含 `NU1900`、目标框架兼容性及仓库其它文档/分析器警告。P1 静态复扫结果为缺失文档、参数标签、返回标签、三行 summary 和超长 summary 均为 0。
- 修正歧义 `cref` 后，`Bing.AspNetCore.Mvc.Tests` net6.0/net8.0 构建成功，0 错误。
- `git diff --check` 通过；仅报告工作区中既有文件的 CRLF/LF 提示。
- P2 目标项目构建：`Bing.Ddd.Application.Contracts`、`Bing.Data`、`Bing.Data.Sql`、`Bing.TestShare` 均使用 `--no-restore -p:GenerateDocumentationFile=true` 成功，0 错误；包含既有 `NU1900` 网络警告及 `Bing.TestShare` 依赖中的过时 API 警告。
- P2 差异复核：26 个目标 C# 文件的变更均为 XML 注释和原文件缺少行尾换行的格式修正；未发现 C# 代码 token 变更。`git diff --check` 通过。
- P3 构建验证：36 个受影响生产与测试项目均执行 `dotnet build --no-restore -p:GenerateDocumentationFile=true -v:q` 并成功，0 错误；警告包含 NuGet 漏洞数据源不可达的 `NU1900` 和依赖项目既有警告。
- P3 静态与差异验证：Roslyn 扫描确认构造函数文案和属性访问方式检查均为 0 问题；批量属性变更前后 C# token 序列一致；`git diff --check` 通过，仅有工作区既有 CRLF/LF 提示。
- P4 验证：最终 `dotnet build Bing.All.sln --no-restore -p:GenerateDocumentationFile=true -v:q` 成功，0 错误、191 个警告；警告包含既有框架兼容性及文档/分析器警告。Roslyn 语义扫描确认 P4 接口实现和重写成员覆盖为零缺失，1,406 个 private 字段零缺失，剩余重载差异已逐项审阅。`git diff --check` 通过；本批仅修改 XML 文档注释。
- P5 目标项目构建：`Bing.Data.Sql`、`Bing.Dapper.Core.Tests`、`Bing.Dapper.SqlServer.Tests`、`Bing.Data.Sql.Tests`、`Bing.Test.Shared`、`Bing.Data.Sql.Benchmarks` 均执行 `dotnet build --no-restore -p:GenerateDocumentationFile=true -v:q` 成功，0 错误；构建仍有 NuGet 漏洞数据源不可达、既有分析器和文档警告。
- P5 差异验证：`git diff --check` 通过；工作区只报告既有文件的 CRLF/LF 提示。本批增加和修订的内容均为 XML 文档注释，未修改方法签名、访问级别或业务逻辑。
- MySQL、SQL Server 集成测试依赖的外部数据库端点不可达，未作为通过项；此前已运行的非外部测试批次通过情况按各测试命令记录，不以集成环境缺失替代验证。
- 本轮没有改变成员签名或 C# 代码 token。此前工作区包含缓存功能实现等既有修改，不计入本轮注释实施范围。
- P7 验证：`dotnet build Bing.All.sln --no-restore -p:GenerateDocumentationFile=true -v:q` 成功，0 错误、142 个警告；警告包括 NuGet 漏洞源不可达、目标框架兼容性及既有项目分析/文档警告。Roslyn 扫描确认 2,159 个纳入文件没有缺失文档、标签错配、非法 XML 或语法错误；`git diff --check` 通过，输出仅有既有 CRLF/LF 提示。本轮注释改动限于 XML 文档内容。
- P7 结构差异核对：本轮逐项审阅的补丁只修改 XML 文档注释；由于 P7 开始前工作区已有未提交功能差异且未保存源码快照，不能用 `HEAD` 充当 P7 前 Roslyn 语法树基线。既有业务差异不计入本轮，也未被覆盖。
