# S3 / MinIO Provider 验证证据

## 实现范围

- `framework/src/Bing.FileStorage.S3`：net6.0 S3 Provider，固定依赖 `AWSSDK.S3` 4.0.104.6。
- `framework/src/Bing.FileStorage.Minio`：MinIO 配置和 `AddBingMinioStorage` 注册入口，复用 S3 Provider。
- `framework/tests/Bing.FileStorage.S3.Tests`：net6.0、net8.0 测试。

S3 Provider 支持已知长度流式写入、读取、属性查询、幂等删除、Range、分片创建/上传/分页查询/完成/中止、签名下载和签名 UploadPart。非空普通写入使用多段上传；每个分片流入限长临时文件并计算 SHA-256，不把整个对象缓冲到内存。上传会话 metadata 保留 `bing-session`，并附加 Provider 对账标识。Provider 不宣称 `CreateOnly`，调用时在读取输入流前以 `UnsupportedCapability` 拒绝。客户端由 Provider 注册后端共享，并随其生命周期释放；调用方直接创建的后端不接管客户端生命周期。

## 真实 MinIO 环境

| 项目 | 值 |
|---|---|
| 容器 | `bing-file-storage-s3-provider-minio` |
| 容器 ID | `82e8c626d7eb486964bfd432c2038a89013559bfb1e9bd4e7a73dad8d8303a89` |
| 镜像 | `minio/minio@sha256:14cea493d9a34af32f524e538b8346cf79f3321eff8e708c1e2960462bd8936e` |
| 数据卷 | `H:\temp\Bing.FileStorage.S3.Minio` → `/data` |
| S3 API | `http://127.0.0.1:19000` |
| Console | `http://127.0.0.1:19001` |
| 状态 | 验证结束后保持运行，供主流程集成测试使用 |

测试凭据仅用于此隔离容器。测试通过以下环境变量读取配置：

- `BING_FILE_STORAGE_MINIO_ENDPOINT`
- `BING_FILE_STORAGE_MINIO_ACCESS_KEY`
- `BING_FILE_STORAGE_MINIO_SECRET_KEY`

测试在 `attachments` Bucket 下使用随机前缀，并在结束时删除本轮对象。ListParts 分页大小设为 1，以真实 MinIO 验证分页路径和服务端 SHA-256 返回值。

## 测试证据

在 PowerShell 中设置上述环境变量后，分别运行：

```powershell
dotnet test framework/tests/Bing.FileStorage.S3.Tests/Bing.FileStorage.S3.Tests.csproj -c Release -f net6.0 --nologo -v minimal
dotnet test framework/tests/Bing.FileStorage.S3.Tests/Bing.FileStorage.S3.Tests.csproj -c Release -f net8.0 --nologo -v minimal
```

验证结果：

| 目标框架 | 结果 | 内容 |
|---|---|---|
| net6.0 | 3 通过，0 失败 | 2 个 Provider 契约测试，1 个真实 MinIO 往返测试 |
| net8.0 | 3 通过，0 失败 | 2 个 Provider 契约测试，1 个真实 MinIO 往返测试 |
| net6.0，未配置端点 | 2 通过，1 跳过 | 确认真实 MinIO 测试在未配置环境时由 xUnit 正确跳过 |

真实 MinIO 测试覆盖非 Seek 输入流、128 MiB + 37 bytes 跨 Provider 分片边界写入及尾部 Range、声明长度超长、空对象、metadata 保留、调用方流所有权、读取与 Range、下载签名 HTTP 请求、消费回调异常透传、签名 UploadPart、分片 SHA-256、ListParts 两页查询、完成后读回、缺失对象、重复中止和删除。测试分别在 net6.0、net8.0 连接同一隔离 MinIO 容器执行。

## 生产符号到测试方法

| 生产符号 / 行为 | 测试项目与方法 |
|---|---|
| `S3FileStorage.Capabilities` 不含 CreateOnly；`SaveAsync` 拒绝 CreateOnly 且不读输入流 | `Bing.FileStorage.S3.Tests`：`CreateOnly_IsRejectedBeforeReadingTheCallerStream` |
| `AddBingMinioStorage` 要求显式凭据 | `Bing.FileStorage.S3.Tests`：`MinioRegistration_RequiresExplicitCredentials` |
| `SaveAsync` 分片边界和准确总长度、`ReadAsync`、`ReadRangeAsync`、签名下载 | 真实 MinIO：`Minio_BasicMultipartRangeAndSignedRequests_RoundTrip` |
| `CreateMultipartAsync`、`SignPartAsync`、`UploadPartAsync`、`ListPartsAsync` 分页和 SHA-256、`CompleteMultipartAsync`、`AbortMultipartAsync` | 真实 MinIO：`Minio_BasicMultipartRangeAndSignedRequests_RoundTrip` |

## 未覆盖项

- 未运行 1 GiB / 100 GiB 容量测试。
- 未对 AWS 或其他 S3 兼容云服务做实际互操作验证；当前真实后端验证范围是固定版本 MinIO。
- 未注入“Complete 请求服务端已完成但响应丢失”的网络故障；Provider 实现会通过对象长度及会话 metadata 对账。
