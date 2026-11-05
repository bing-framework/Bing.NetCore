# 订单变更跟踪示例

```powershell
dotnet run --project samples/Bing.Samples.ChangeTracking -- --verify
```

示例使用进程内 SQLite 数据库，加载两份订单快照，修改订单状态、收货城市和明细，再调用 `before.GetChanges(after)`。订单与差异记录在同一数据库事务中保存，随后重新读取并输出路径、描述和新旧值。`--verify` 校验数据库中的完整结果，并模拟第二条变更记录写入失败，检查订单、明细和第一条已写入的变更记录是否一并回滚。

生成文件位于 `obj/Debug/net8.0/generated/` 下，也可在 IDE 的源生成器节点中查看。示例不连接真实业务系统；进程退出后数据消失。
