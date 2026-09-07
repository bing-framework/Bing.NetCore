# Provider 能力

能力状态必须同时反映 Provider Profile、运行时 Fail Fast、单元测试、真实集成测试、Capability Catalog、Evidence 和本文档。`TestGenerated` 或静态声明不等于发布级 `ReleaseEvidence`。

| Provider | 事务 | Streaming | Batch/Returning | Procedure / Function | OUT 参数 | RC 定位 |
| --- | --- | --- | --- | --- | --- | --- |
| SQLite | 支持 | 支持 | 按 SQLite 语法 | 不支持存储过程 | 不支持 | 核心 Provider |
| MySQL | 支持 | 支持 | Batch 支持；Returning 不适用 | 存储过程 | OUT/INOUT 合同需真实运行 | 核心 Provider |
| PostgreSQL | 支持 | 支持 | Batch、UPDATE FROM、DELETE USING、RETURNING | 文本 SQL 可调用 Function；当前 `Procedure()` 命令路径按数据库语义拒绝 | 不伪装为 SQL Server/MySQL OUT | 核心 Provider |
| SQL Server | 支持（同步回退） | 支持 | Batch、OUTPUT | 存储过程 | OUT/INOUT/ReturnValue | 核心 Provider |
| Oracle | 以当前安全 fixture 为准 | 以当前安全 fixture 为准 | Limited | 未完成安全 fixture 时不得声明支持 | 未完成安全 fixture 时不得声明支持 | Non-blocking |
| Doris | Read-only | 以只读合同为准 | 不要求 Mutation parity | 不要求 Procedure parity | 不要求 | Non-blocking / ReadOnly |

`Unsupported` 表示统一框架合同不适用于该数据库语义；`ImplementationGap` 只表示数据库合同适用但 Bing Provider 尚未实现，不能用来掩盖 Profile 与实际行为冲突。
