## New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CG001 | ChangeGraph | Error | 继承 DomainObjectBase<TObject> 的领域对象必须声明为 partial。 |
CG002 | ChangeGraph | Error | 静态类型不能作为领域对象生成入口。 |
CG006 | ChangeGraph | Warning | 成员类型不受支持，自动 AddChanges 将跳过该属性。 |
CG015 | ChangeGraph | Info | 属性规则不会改写手写 AddChanges 方法。 |
CG017 | ChangeGraph | Error | 显示资源必须是可访问的静态字符串属性。 |
CG018 | ChangeGraph | Error | 派生 Description 特性无法静态求值，需改用标准常量描述。 |
CG019 | ChangeGraph | Error | 自动 AddChanges 的无序 Set 元素必须通过 IEntity.GetKeys() 提供身份。 |
CG020 | ChangeGraph | Error | AddChanges hook 必须使用 override 或重命名；new 同名方法冲突。 |
