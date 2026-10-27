# 容错扩展迁移与回滚

## 迁移顺序

1. 安装 `Bing.Resilience.Abstractions`、`Bing.Resilience` 和 BuiltIn/Fences/Polly 中的一个 Provider，注册并验证启动校验。
2. 给两个 HTTP 客户端逐个添加 `AddBingHttpResilience` 或 `AddBingDynamicApiClientResilience`；运行时模式先把旧 `MaxRetries` 设为 0。
3. 需要动态路由时先发布配置快照并配置可信 origin、契约版本、一致性组和撤销策略。
4. 写重放前先部署接收端同键幂等协议和操作目录，再开放少量幂等写；非幂等写、上传、流响应继续只发送一次。
5. Durable/Saga 先执行向后兼容的 Journal 迁移，部署旧处理器和历史密钥，再启动 Worker。
6. SyntheticResponse 和 Definition Hedging 逐操作启用，观察来源、契约版本、端点 ID、熔断和租约指标。

## 兼容变化

已有 HTTP 注册入口、Legacy 运行时行为、SDK 异常包装、错误响应读取边界和文件/流所有权保持。新增 Provider 不会自动替换宿主现有引擎。启用 Runtime 后，完整操作仍固定一个 Generation；启用写重放后，宿主必须保证请求体版本在重放窗口内可重建。

## 回滚

- Provider：静态注册或 Provider 包变更通过重启切回；已注册 Provider 可先调用 `IResilienceRuntimeManager.SwitchAsync` 切换并等待在途代排空，再按需重启。
- 发现：发布最后一个已验证固定快照，安全撤销状态继续保留。
- 写重放：关闭业务许可和 `MaxRetryAttempts`；至少保留幂等记录到重放窗口结束。
- Durable/Saga：停止入队和 Worker，新旧 Journal、历史处理器和密钥继续保留。
- Synthetic：移除操作白名单，恢复原远端错误。
- Definition Hedging：改为 SequentialRetry，并保留完整校验缓存。
- 分布式熔断：明确发布 FailClosed/FailOpenWithLocalCircuit；不能静默绕过状态存储。

数据库表和业务补偿不会因删除包自动回滚，必须按宿主迁移系统增量处理。
