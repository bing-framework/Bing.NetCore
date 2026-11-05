# Domain ChangeGraph Smoke

该独立控制台项目展示领域对象源生成器在真实 Consumer 中生成的 `AddChanges`，并运行嵌套对象、自引用和集合检查。独立 `ChangeGraphTracker` 的快照及边界测试使用 `Fixtures/ExplicitLegacyGraphModels.cs` 中显式保留的测试模型，不依赖领域对象生成器生成 Graph 模型。

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

每个参与类型都继承 `DomainObjectBase<TObject>` 并声明 `partial`，无需类级标记。`Order` 生成逐成员 `AddChanges`：标量调用 `AddChange`，关系通过 `AddObjectChanges`/`AddCollectionChanges` 调用 `Customer`、`Line` 和 `Product` 自身的 `GetChanges`。`Department` 同时配置父子关系并包含 `Parent` 自引用。`Customer`、`Product`、`Line` 各自在实体上声明忽略规则、描述或集合身份（`IEntity.GetKeys()`）。`LegacySample` 是纯标量实体，生成方法展示四参数 `AddChange` 调用。`DomainNestedScenarios` 同时验证显式实体身份、额外 `IKey` 不参与身份读取，以及新子对象为 `null` 时直接记录删除。

## NativeAOT

```powershell
dotnet publish framework/tests/Bing.Ddd.Domain.Aot.Smoke/Bing.Ddd.Domain.Aot.Smoke.csproj -c GraphSmoke -r win-x64 --self-contained true -p:DomainSmokeAot=true
framework/tests/Bing.Ddd.Domain.Aot.Smoke/bin/GraphSmoke/net10.0/win-x64/publish/Bing.Ddd.Domain.Aot.Smoke.exe --require-aot
```

发布期间出现的 trimming、AOT、依赖或本机工具链警告都应保留并检查。该 smoke 不屏蔽警告，也不把普通 JIT 运行视为 NativeAOT 发布证据。
