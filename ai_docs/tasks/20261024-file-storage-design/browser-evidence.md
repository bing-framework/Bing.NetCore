# 浏览器及工具链验证

Browser plugin 未提供，此次使用安装环境中的 Playwright 和本机 Chrome，无新增浏览器依赖。示例仅监听 127.0.0.1，使用隔离 SQLite、存储目录与测试凭据。

| 场景 | 实际结果 |
|---|---|
| EF Core + Local，64 MiB | 上传后暂停、刷新页面、停止并重启服务，重选同一文件后恢复并进入 Ready |
| EF Core + MinIO，64 MiB | 浏览器经签名 UploadPart 直接传到 MinIO，后台完成并列出文件 |
| MinIO 签名刷新 | 注入首个 PUT 403 ExpiredToken，观察到重新获取分片签名 |
| 网络中断 | 注入连接断开，页面保留会话；刷新重选文件后恢复完成 |
| FreeSql + Local，64 MiB | 实际浏览器上传完成，文件列表出现 Ready 文件 |
| 页面与移动宽度 | 桌面截图检查通过；窄屏无横向溢出，操作可见 |
| 控制台 | 正常流程无 pageerror；故障注入的预期网络错误不计为正常页面错误 |

截图：[Local 桌面](evidence/local-desktop.png)、[Local 移动](evidence/local-mobile.png)、[MinIO](evidence/minio-desktop.png)、[FreeSql](evidence/freesql-desktop.png)。

浏览器阶段发现 AWS SDK v4 对无分片返回 null 集合，已修复并加入真实 MinIO 空集合断言；发现暂停中未结束的并行任务可能干扰重启，示例改为取消后等待所有分片任务结束。后续后端元数据与空文件修复由真实 MinIO + 两套 ORM 测试 PersistedSession_EmptyOrDirectUpload_CompletesWithoutRetry 验证。未修改浏览器脚本后的既有截图按 UI 范围复用。

## 导入与迁移 CLI

两个独立 SQLite 库分别用 schema 输出的 EF Core / FreeSql DDL 创建。清单含 43 字节 UTF-8 实际旧文件与无效 Test 记录：

1. import 默认预览不创建附件；缺租户/地址无效报告 NeedsAttention。
2. import --apply 创建旧 GUID 的 Ready 私有文件。
3. migrate --apply 排队，run-tasks --apply 完成 Local → 隔离 MinIO。
4. verify 完整回读，长度为 43 字节，摘要与持久记录相等。

原始结果：[EF Core](evidence/tool-EFCore-minio.json)、[FreeSql](evidence/tool-FreeSql-minio.json)。单元/集成测试另覆盖重复导入、删除竞争、复制后数据库提交失败和保留正式目标。

## 范围

Admin 测试加载两套宿主的真实 FileController 和相应 ORM，覆盖鉴权至物理删除；不启动依赖其他业务服务的完整 Admin Startup。SQLite/MySQL 实际 HTTP 证据见 admin-evidence.md 与 mysql-evidence.md。没有操作真实业务库或执行真实旧数据迁移。
