# 蓝绿部署模板与验收边界

这套模板面向 `modules/admin/src/Bing.Admin`（EFCore）或 `modules/admin/src/Bing.Admin.FreeSQL` 的业务镜像。两个宿主已接入 `BingTrafficGate`，提供 `/health/ready`、HTTP 新请求拒绝和在途请求排空；并识别 `BING_CAP_CONSUMER_ENABLED=false`，使 Web 角色保留 CAP 出箱发布而不启动原生消费者及接收重试处理器。`BING_HOST_ROLE=consumer` 仍不会自动创建独立工作进程，模板仍是部署契约和待演练样例，**当前仓库镜像不能直接通过完整动态验收或安全切流**。不要把静态检查成功写成生产部署通过。

## 镜像契约

构建不可变镜像时选择一种 ORM 宿主，并验证镜像满足：

- `BING_HOST_ROLE=web` 配合 `BING_CAP_CONSUMER_ENABLED=false` 时，Admin 进程保留 HTTP 和 CAP 出箱发布，并移除 CAP 消费订阅及接收重试处理器。HTTP 进程仍包含应用代码；若需要完全不启动消费者以外的后台任务，必须在宿主入口另行配置。
- `BING_HOST_ROLE=consumer` 会跳过 Admin 的 HTTP 业务管道、Swagger、CAP Dashboard 和 Hangfire 模块，并保持 CAP 消费开关为 true；`BING_HOST_ROLE=task` 跳过 HTTP 业务管道、Swagger 和 CAP Dashboard，启用对应宿主的 Hangfire，CAP 消费默认关闭，需显式设置 `BING_CAP_CONSUMER_ENABLED=true`。两种角色仍复用 Web 宿主程序，不能当作无 HTTP 监听的独立 Worker。`BING_CAP_CONSUMER_GROUP=bing-admin` 是蓝绿部署的稳定逻辑组标识，版本号不能拼入其中；各 `[CapSubscribe]` 的 `Group` 仍由订阅特性固定，且必须在版本间保持稳定，环境变量不会动态改写这些特性。
- 当前 `task-blue/task-green` 槽位针对 EFCore 和 FreeSQL Admin 共同链接的 Hangfire 模块；两套宿主均已按 Task 角色注册任务运行时、创建/执行排空过滤器和停止等待服务。该能力仍只证明应用层准入与排空契约，不能把模板静态检查或注册测试当作真实 Hangfire Server、任务存储迁移及多节点排空已经完成。
- HTTP `GET /health/ready` 在依赖、迁移兼容性和预热完成后返回 200；排空开始后立即返回非 200。仅检查进程存活不构成就绪。
- 收到正常终止信号后停止接收新请求或新消息，等待在途请求/消费结束；超时保留 CAP 与业务幂等记录供恢复，不能自行跳过失败事件。
- 镜像具备 `/bin/sh`，Kubernetes `preStop` 会先等待 15 秒让 Service 端点摘除传播。容器优雅退出上限为 300 秒；长请求和消费者处理时间必须与此一致。
- MySQL、RabbitMQ、Redis、密钥及配置通过现有配置机制注入。模板不包含凭据。发布前保证数据库先扩展后收缩，消息新旧契约可共存。

## Docker Compose + Nginx

在此目录分别设置蓝绿镜像的固定 digest。首次启动蓝版 Web、绿版 Web、Nginx、蓝版消费者和蓝版 Task：

```powershell
$env:BING_BLUE_IMAGE = 'registry.example/bing-admin@sha256:BLUE_DIGEST'
$env:BING_GREEN_IMAGE = 'registry.example/bing-admin@sha256:GREEN_DIGEST'
docker compose -f .\compose.yaml --profile consumer-blue up -d
docker compose -f .\compose.yaml --profile task-blue up -d
.\Test-BlueGreen.ps1 -Platform Compose -Live -RequireConsumer -RequireTask
```

绿版 Web 始终只预热 HTTP，不启动消费。待绿版就绪、业务长请求与数据库兼容性检查通过后执行：

```powershell
.\Invoke-ComposeBlueGreenCutover.ps1 -Target green -SwitchConsumer -SwitchTask -DrainTimeoutSeconds 300
```

该入口会先检查绿版 Web 就绪，再按稳定逻辑组启动绿版消费者和 Task，切换 Nginx，最后以有限排空时间停止蓝版后台角色。任一步失败都会尝试恢复旧 Web 路由、旧后台角色，并保留恢复失败警告。只切换 Web 时仍可直接使用 `Switch-ComposeTraffic.ps1`；首次演练可添加 `-DryRun` 查看顺序，但 DryRun 不验证容器、就绪或排空。

消费者两个版本短时重叠时，固定逻辑组、业务幂等键及事务性消费必须已验证。Task 两个版本也必须使用稳定的任务幂等键和唯一调度策略；若要求零重叠，先停止蓝版消费者/Task 并确认各自排空，再启动绿版，期间会暂停后台处理。不要仅凭 Web 切流推断消费者或 Task 已切换。回滚 HTTP 使用 `Switch-ComposeTraffic.ps1 -Target blue`；消费者和 Task 按相反顺序处理。Nginx 配置会在本目录的 `nginx/active.conf` 留下当前路由状态，应记录发布版本与操作人。切流脚本先探测目标就绪，再验证 Nginx 配置并 reload；失败时尝试恢复原配置。

## Kubernetes

把 `kubernetes.yaml` 中蓝绿占位镜像分别换成对应的固定镜像 digest，并注入业务配置。绿版消费者和 Task `replicas: 0`，蓝绿 Web 均禁用消费，Service 初始选择蓝版。模板不包含 Ingress、Secret、数据库迁移 Job 或定时任务主控，需按目标集群补齐。

```powershell
kubectl -n YOUR_NAMESPACE apply -f .\kubernetes.yaml
.\Test-BlueGreen.ps1 -Platform Kubernetes -Namespace YOUR_NAMESPACE -Live -RequireConsumer -RequireTask
.\Invoke-KubernetesBlueGreenCutover.ps1 -Target green -Namespace YOUR_NAMESPACE -SwitchConsumer -SwitchTask -DrainTimeoutSeconds 300
```

该入口会先等待目标 Web Deployment 就绪，再按稳定逻辑组扩容目标 Consumer/Task，切换 `bing-web` Service，最后等待旧后台 Deployment 缩容到零。失败时会尝试恢复旧 Service 和旧角色副本数。只切换 Web 时仍可直接使用 `Switch-KubernetesTraffic.ps1`；首次演练可添加 `-DryRun` 查看计划，但 DryRun 不访问集群、不证明副本就绪或排空完成。

HTTP 回滚使用 `Switch-KubernetesTraffic.ps1 -Target blue`；消费者单独恢复蓝版副本并排空绿版。Kubernetes Service 切换只改 `color` selector。等待旧版请求排空后再缩容旧 Web；`preStop` 和 300 秒终止时间只是上限与缓冲，不保证超过上限的请求完成。

## 验收记录

先运行 `Test-BlueGreen.ps1` 做无需外部服务的静态断言。`-Live` 默认检查 Compose/Kubernetes 配置与双版 HTTP 就绪，不执行切流；增加 `-RequireConsumer` 或 `-RequireTask` 后，分别要求至少一个消费者或 Task 实例运行/就绪。该参数仍不证明后台角色已经排空、任务完成或切换成功。`-Live` 会拒绝可变镜像标签、缺少 SHA-256 摘要的镜像以及 Kubernetes 清单中的占位镜像。静态模式允许保留文档占位镜像；运行前必须替换为不可变 digest。分别在隔离测试环境记录以下动态场景，全部通过后才能将部署标记为通过：

1. 在蓝版发起持续长请求，绿版预热期间确认流量仍到蓝版、绿版没有 CAP 消费；切流后新请求到绿版，旧请求完成或在 300 秒边界明确报告。
2. 让绿版返回非 200 就绪状态，确认切流脚本拒绝切换；恢复就绪后切流并确认真实业务请求成功。
3. 消费者处理一条持续任务时切换消费者，确认固定组内只有一次业务提交；超时/强制终止时依赖事务、出箱和幂等恢复，保留未完成证据。
4. 分别在 Nginx 与 Kubernetes 反向切回蓝版，验证新请求路由、旧请求排空、消费者单独回滚和消息契约兼容。
5. 验证数据库扩展迁移、定时任务单活、CAP 积压与重试、审计失败和请求/消费者/Task 排空指标。破坏性数据库迁移不承诺一键回滚。

这些动态结果必须附具体镜像 digest、集群/Compose 版本、时间、请求 ID、CAP 消息 ID 和日志或指标证据。当前模板尚无这些外部证据；本地注册测试只证明处理器清单边界，不证明真实 RabbitMQ 切流。
