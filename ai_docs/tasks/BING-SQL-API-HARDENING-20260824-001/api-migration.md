# API 迁移记录

## 删除入口
- 删除 `SqlAdvancedQueryExtensions` 及 Root 泛型 `Query<TResult>`、`Sql<TResult>`、`SqlInterpolated<TResult>`、`Procedure<TResult>`。
- 删除泛型描述 `SqlFluentQuery<TResult>`、`SqlTextQuery<TResult>`、`SqlProcedureQuery<TResult>`、旧 `SqlQuery<TResult>`。

## 替代形式
```csharp
query.Query().ToList<Item>();
query.Query().Scalar<int>();
query.Sql("Select ...").ToList<Item>();
query.Procedure("name").ExecuteList<Item>();
```

## 多映射
非泛型 `SqlFluentQuery` 和 `SqlTextQuery` 提供 Dapper 2～7 对象映射的同步、异步 `ToList` 终结方法，结果类型位于方法泛型参数末尾。

## Breaking Change
删除旧泛型描述和 Root 泛型扩展是计划内 Breaking Change。Public API Shipped/Unshipped 基线、Analyzer 契约和迁移文档已同步；外部 Provider Integration 仍按既有安全 Gate 单独执行。
