# Bing.DynamicApi.Contracts
[![NuGet](https://img.shields.io/nuget/v/Bing.DynamicApi.Contracts.svg)](https://www.nuget.org/packages/Bing.DynamicApi.Contracts/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.DynamicApi.Contracts.svg)](https://www.nuget.org/packages/Bing.DynamicApi.Contracts/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

动态 API 服务端与运行时客户端共享的描述协议，包含版本、服务、方法和参数定义。该包只有数据契约，不提供服务注册或 HTTP 调用实现。

## 安装

```bash
dotnet add package Bing.DynamicApi.Contracts
```

通常无需单独安装：`Bing.DynamicApi.Client` 和服务端动态 API 能力会传递使用它。协议消费者应先检查 `SchemaVersion`，不支持的版本应明确失败，不能按未知字段猜测行为。

完整说明见[动态 API 模块文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/modules/dynamic-api.md)。

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
