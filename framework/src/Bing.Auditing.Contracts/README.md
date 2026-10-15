# Bing.Auditing.Contracts
[![NuGet](https://img.shields.io/nuget/v/Bing.Auditing.Contracts.svg)](https://www.nuget.org/packages/Bing.Auditing.Contracts/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Auditing.Contracts.svg)](https://www.nuget.org/packages/Bing.Auditing.Contracts/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 审计契约工程（供领域层引用，不含运行时实现）。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：19
>
> **上游 Bing 包**：无（本包是叶子/基础包）

---

## 这是什么

把审计接口单独成包，领域层只引用契约即可。

`IAuditPropertySetter` 定义实体新增、修改和删除时的审计字段填充契约。EFCore、FreeSQL 等数据适配层可以只依赖本包；运行时实现由 `Bing.Auditing` 提供。旧消费者从 `Bing.Auditing` 加载该接口时仍通过类型转发解析。

## 安装

```bash
dotnet add package Bing.Auditing.Contracts
```

## 与其它包的关系

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Auditing.Contracts

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
