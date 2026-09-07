# PostgreSQL Function

PostgreSQL 的表值 Function 通过原生 SQL 结果集调用，例如：

```sql
Select output_value, doubled_value
From public.bing_sql_contract_function(@input_value)
```

这是真实的 Result Set 语义，不能包装成 SQL Server 或 MySQL 的 OUT/InputOutput 参数。当前 Npgsql 6.0.11 与框架 `CommandType.StoredProcedure` 路径对 PostgreSQL procedure 的命令形态不一致，因此 PostgreSQL Profile 对 `Procedure()` 命令和输出参数执行 Fail Fast；文本 Function 结果集仍可正常使用。

迁移时：

- 需要结果集时使用 `Sql()` / `SqlInterpolated()` 并完整绑定参数。
- 需要数据库原生 `CALL` 时使用 Provider 原生 Npgsql 命令或等待框架提供明确的 PostgreSQL procedure 合同，不要把 `Procedure()` 的异常吞掉后当作成功。
- 不要读取不存在的 OUT 参数快照，也不要把 Function 返回列改名为 OUT 参数以绕过 Capability Gate。
