# Domain ChangeGraph Smoke

该独立控制台项目展示 `ChangeGraph` 源生成器在真实 Consumer 中生成的代码，并运行嵌套对象、自引用、集合、快照和旧版 `DomainObjectBase` 行为检查。

## 运行并查看生成代码

在仓库根目录执行：

```powershell
dotnet restore framework/tests/Bing.Ddd.Domain.Aot.Smoke/Bing.Ddd.Domain.Aot.Smoke.csproj -p:Configuration=GraphSmoke
dotnet run --project framework/tests/Bing.Ddd.Domain.Aot.Smoke/Bing.Ddd.Domain.Aot.Smoke.csproj -c GraphSmoke --no-restore
```

程序逐项打印 `PASS` 和生成文件路径。生成文件也会保存在：

```text
framework/tests/Bing.Ddd.Domain.Aot.Smoke/Generated/GraphSmoke/net10.0/
```

每个参与类型都继承 `DomainObjectBase<TObject>` 并声明 `partial`，无需类级标记。`Order` 生成逐成员 `AddChanges`：标量调用 `AddChange`，关系通过 `AddObjectChanges`/`AddSequenceChanges` 调用 `Customer`、`Line` 和 `Product` 自身的 `GetChanges`。`Department` 同时配置父子关系并包含 `Parent` 自引用。`Customer`、`Product`、`Line` 各自在实体上声明忽略规则、描述或集合身份（`IKey<int>`）。`LegacySample` 是纯标量实体，生成的快速方法展示四参数 `AddChange` 调用。

## NativeAOT

```powershell
dotnet publish framework/tests/Bing.Ddd.Domain.Aot.Smoke/Bing.Ddd.Domain.Aot.Smoke.csproj -c GraphSmoke -r win-x64 --self-contained true -p:DomainSmokeAot=true
framework/tests/Bing.Ddd.Domain.Aot.Smoke/bin/GraphSmoke/net10.0/win-x64/publish/Bing.Ddd.Domain.Aot.Smoke.exe --require-aot
```

发布期间出现的 trimming、AOT、依赖或本机工具链警告都应保留并检查。该 smoke 不屏蔽警告，也不把普通 JIT 运行视为 NativeAOT 发布证据。
