# 缓存单节点 Redis 集成证据补齐计划

## 目标

在已存在的非生产 Redis 5.0.12 单节点容器上，执行当前缓存实现的门控集成测试，关闭只依赖单节点环境的证据缺口，并把 Cluster、客户端分片、双节点故障及性能对照仍需外部环境的项目明确分类。

## 范围

- FreeRedis 单节点集成测试。
- CSRedis 单节点集成测试。
- EasyCaching Redis/Hybrid 单节点集成测试。
- 不启动、不停止、不清空现有容器；测试只使用随机命名空间和现有测试清理路径。
- 不把单节点结果外推为 Cluster、客户端分片或跨进程故障转移结论。
- 不修改生产代码；只在任务报告中记录证据和剩余 TODO。

## 预检查

- `docker ps` 确认 Redis 容器存在并映射 `6379`。
- 检查集成测试门控、连接字符串和清理约束。
- 执行前确认工作区已有修改不被覆盖。

## 验收标准

1. FreeRedis、CSRedis 和 EasyCaching 单节点门控测试均实际执行，记录通过、跳过和失败数量。
2. 若失败，记录首个真实根因并修复可在工作区内解决的问题；环境或测试前置条件问题分类为 `BLOCKED_EXTERNAL`。
3. 证据报告明确列出仍未覆盖的 Cluster、分片、故障恢复、跨进程和性能基线项目。
4. 运行 `git diff --check`，并记录未执行的多节点/性能项目及原因。

## 验证命令

```powershell
$env:RUN_INTEGRATION_TESTS = 'true'
$env:ConnectionStrings__RedisConnection = 'localhost:6379'
$env:BING_TEST_EASYCACHING_REDIS = 'localhost:6379'
dotnet test framework/tests/Bing.Caching.FreeRedis.Tests.Integration/Bing.Caching.FreeRedis.Tests.Integration.csproj --no-restore --configuration Release
dotnet test framework/tests/Bing.Caching.CSRedis.Tests.Integration/Bing.Caching.CSRedis.Tests.Integration.csproj --no-restore --configuration Release
dotnet test framework/tests/Bing.EasyCaching.Tests/Bing.EasyCaching.Tests.csproj --no-restore --configuration Release
```

Cluster、客户端分片和专用 `52800` 测试维持门控跳过，不用单节点替代。
