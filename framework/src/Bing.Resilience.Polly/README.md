# Bing.Resilience.Polly

独立容错库的可选组成，目标框架 net6.0/net8.0。

参见[独立容错指南](../../../docs/guides/resilience.md)、[HTTP 接入](../../../docs/guides/http-resilience.md)和[执行证据](../../../ai_docs/tasks/20261027-resilience-abstractions/execution.md)。

此包安装 Polly 8.4.2，仅在选择此 Provider 的宿主中引用。使用 AddBingResilience().AddPollyProvider("primary").UseDefaultProvider("primary")。免费消费组合不引用此包。
