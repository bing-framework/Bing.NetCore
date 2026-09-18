# Bing.AutoMapper
[![NuGet](https://img.shields.io/nuget/v/Bing.AutoMapper.svg)](https://www.nuget.org/packages/Bing.AutoMapper/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.AutoMapper.svg)](https://www.nuget.org/packages/Bing.AutoMapper/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 基于 AutoMapper 的 `IObjectMapper` 实现，扫描 `IObjectMapperProfile` 自动注册。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：横切 / 扩展 ｜ **当前版本**：7.0.0 ｜ **源文件**：2
>
> **上游 Bing 包**：`Bing.ObjectMapping`
>
> **三方依赖**：`AutoMapper`

---

## 这是什么

把 AutoMapper 接入 Bing 的对象映射抽象。实现 `IObjectMapperProfile` 定义映射配置，框架会自动发现并构建 `MapperConfiguration`。

## 安装

```bash
dotnet add package Bing.AutoMapper
```

## 快速上手

```csharp
services.AddAutoMapper();

// 定义映射（实现 IObjectMapperProfile 即被自动扫描）
public class UserProfile : IObjectMapperProfile
{
    public void CreateMap(IMapperConfigurationExpression cfg)
        => cfg.CreateMap<User, UserDto>();
}

// 使用
var dto = entity.MapTo<UserDto>();
var list = entities.MapToList<UserDto>();
```

## 注册入口

| 入口方法 | 命名空间 | 说明 |
| --- | --- | --- |
| `AddAutoMapper()` | `Bing.AutoMapper` | 扫描 Profile、构建配置、注册单例 `IObjectMapper` |

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `AutoMapperObjectMapper` | `Bing.AutoMapper` | `IObjectMapper` 实现 |
| `IObjectMapperProfile` | `Bing.AutoMapper` | 映射配置契约 |

## 与其它包的关系

**本包依赖**：`Bing.ObjectMapping`

**依赖本包**：`Bing.Biz.OAuthLogin`

**三方 NuGet**：`AutoMapper`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.AutoMapper

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
