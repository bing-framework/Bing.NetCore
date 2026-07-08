---
name: chinese-comments
description: 为 C#/.NET 项目自动补全规范的中文 XML 注释。
---

# 中文注释规范

在生成或修改 C#/.NET 项目代码时，请自动补全中文 XML 注释。

## 必须补全的对象

- Controller 与 Action
- class / interface / enum / record
- public / protected 方法
- 构造函数
- DTO / Entity / Options
- public 属性
- 重要字段、常量、缓存键

## 注释要求

1. 使用简体中文。
2. 注释应描述业务含义，不要只翻译名称。
3. public、protected、internal、private 方法必须补全 `<summary>`、`<param>`、`<returns>`。
4. 枚举类型和枚举成员都要补全注释。
5. DTO、实体、控制器、应用服务必须优先补全注释。
6. 重要 private 字段和常量建议补充注释。
7. 保持代码格式化，不要破坏原有逻辑。

## 风格要求

- 简洁、专业、准确
- 符合企业级 .NET 项目风格
- 与 ABP / DDD / CQRS 场景兼容
- 优先体现业务用途，而非字面翻译

## 执行要求

当发现缺失注释时，请顺手补齐；
当发现已有注释质量较差时，请优化为更准确的中文注释。