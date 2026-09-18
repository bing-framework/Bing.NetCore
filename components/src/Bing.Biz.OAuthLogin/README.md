# Bing.Biz.OAuthLogin
[![NuGet](https://img.shields.io/nuget/v/Bing.Biz.OAuthLogin.svg)](https://www.nuget.org/packages/Bing.Biz.OAuthLogin/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Biz.OAuthLogin.svg)](https://www.nuget.org/packages/Bing.Biz.OAuthLogin/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 第三方 OAuth 登录：16 个平台的授权、取令牌与取用户信息。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：业务组件（components/） ｜ **当前版本**：7.0.0 ｜ **源文件**：135
>
> **上游 Bing 包**：`Bing.AutoMapper`、`Bing.Validation`、`Bing.Core`
>
> **三方依赖**：`Bing.Utils.Http`

---

## 这是什么

体量最大的 components 包之一（约 135 个源文件），每个平台一套 `XxxAuthorizationConfig` / `ConfigProvider` / `Provider` / `Request`。

支持：`QQ` `Wechat` `Weibo` `Taobao` `Microsoft` `Github` `Facebook` `Youzan` `Jd` `Alibaba` `MeiliShuo` `Baidu` `Coding` `Gitee` `OsChina` `DingTalk`。

## 安装

```bash
dotnet add package Bing.Biz.OAuthLogin
```

## 快速上手

```csharp
using Bing.Biz.OAuthLogin.QQ;
using Bing.Biz.OAuthLogin.QQ.Configs;

var config = new QQAuthorizationConfig
{
    AppId = configuration["OAuth:QQ:AppId"],
    AppKey = configuration["OAuth:QQ:AppKey"],
    CallbackUrl = "https://your.site/oauth/qq/callback"
};
config.Validate();   // 校验失败抛 Bing 的 Warning 异常

var provider = new QQAuthorizationProvider(new QQAuthorizationConfigProvider(config));

// ① 生成授权跳转地址  ② GetTokenAsync  ③ IGetUserInfoProvider 取用户信息
var result = await provider.GetTokenAsync(new AccessTokenParam { Code = code });
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `AuthorizationConfigBase` | `Bing.Biz.OAuthLogin.Core` | `AppId` / `AppKey` / `CallbackUrl` / `AuthorizationUrl` / `AccessTokenUrl`，全部 `[Required]` |
| `IAuthorizationProvider` | `Bing.Biz.OAuthLogin.Core` | 继承自 `IAuthorizationUrlProvider<AuthorizationParam>` |
| `GetTokenAsync` / `RefreshTokenAsync` | `Bing.Biz.OAuthLogin.Extensions` | 扩展方法；Provider 未实现对应能力接口时抛 `NotImplementedException` |
| `OAuthWay` | `Bing.Biz.OAuthLogin.Core` | 16 个提供方枚举 |

## 与其它包的关系

**本包依赖**：`Bing.AutoMapper`、`Bing.Validation`、`Bing.Core`

**三方 NuGet**：`Bing.Utils.Http`

## 注意事项

⚠ 四个要点：
1. **没有注册入口**，`IOAuthLoginFactory` 目前是**空接口**，需要自己 `new` 并注册进容器；
2. 本包只负责"向第三方换取身份"，拿到用户信息后**仍需你自己签发本站令牌**（另见 `Bing.AspNetCore.Authentication.JwtBearer` 的 `AddJwt`）；
3. `AuthorizationConfigBase.Validate()` 抛的是 Bing 的 **`Warning`** 异常；
4. 部分平台接口年代较早（有赞、美丽说、开源中国等），**对接前请核对目标平台现行 OAuth 文档**。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [组件库文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/guides/组件库文档.md) —— `components/` 下各包的详细说明与状态徽章
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Biz.OAuthLogin

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
