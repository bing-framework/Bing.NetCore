# 独立容量验证

预期均为 EXPECTED_SUCCESS；数据流生成、16 MiB 分片、并发 1、120 分钟上限，在 H 盘按后端顺序运行。每次都完整上传并回读逐字节核验，再验证进行中的分片取消、中止和删除。没有预分配整文件数组。

| 后端 | 文件大小 | 上传+完整回读总耗时 | 客户端峰值 WorkingSet | 客户端峰值托管内存 | 往返总字节吞吐 | 结果 |
|---|---|---|---|---|---|---|
| local | 1 GiB | 3.41 s | 49.2 MiB | 3.9 MiB | 601.2 MiB/s | PASS |
| local | 100 GiB | 440.11 s | 76.3 MiB | 26.6 MiB | 465.3 MiB/s | PASS |
| minio | 1 GiB | 10.61 s | 94.2 MiB | 16.7 MiB | 193.0 MiB/s | PASS |
| minio | 100 GiB | 1244.03 s | 94.7 MiB | 26.6 MiB | 164.6 MiB/s | PASS |

100 GiB 是 107,374,182,400 字节，每个后端实际上传及回读各该数量。内存为探针客户端，不含 MinIO 服务端或操作系统缓存。Local 临时磁盘占用约 200 GiB，符合分片与合并内容同时保留的预期；MinIO 内容删除通过 API 确认，后台回收有延迟。稍后执行 docker exec du -sh /data，仅剩 129 MiB 示例数据，H 盘恢复约 1.594 TB 空闲。单次 MinIO 服务端采样为 398.6 MiB，不将其称为峰值。

运行环境：Windows 10.0.19045，.NET 8.0.30；MinIO 隔离 Docker Linux 容器，镜像摘要 14cea493d9a34af32f524e538b8346cf79f3321eff8e708c1e2960462bd8936e，版本 RELEASE.2025-09-07T16-13-09Z。容量运行之间有小文件 UI/CLI 功能验证，吞吐是环境观察值，不是正式性能 SLA 或性能提升证明。

## 原始证据与复用边界

- [Local 1 GiB](evidence/capacity-local-1gib.json)
- [Local 100 GiB](evidence/capacity-local-100gib.json)
- [MinIO 1 GiB](evidence/capacity-minio-1gib.json)
- [MinIO 100 GiB](evidence/capacity-minio-100gib.json)

原始 JSON 的 assemblySha256 记录的是探针引用的 FileSystem 程序集（两后端均如此），不将其误标为 S3 程序集。补充二进制身份见 evidence/capacity-binaries.json。探针后续已将新报告字段修正为 providerAssemblySha256 / harnessAssemblySha256，历史原始 JSON 保持不变。后续修改只涉及缺失上传句柄的幂等完成分支、空分片列表兼容、容量判断溢出边界、离线临时文件维护、业务元数据/空文件及文档，容量成功路径的缓冲大小、读写算法、并发和资源门槛未变；容量证据按相关执行路径复用，不无依据重复写入 100 GiB。

正式吞吐/内存阈值未定义，本次容量正确性门槛均通过；性能 before 对比 NOT_APPLICABLE。工作目录和每轮随机 Bucket 内容由探针清理。
