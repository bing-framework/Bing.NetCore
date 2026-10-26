# Bing.FileStorage

命名存储实例、容器白名单和范围绑定的基础层，目标框架为 `netstandard2.0`，依赖 `Bing.FileStorage.Abstractions`。

先注册基础选项和至少一个 Provider。默认实例必须与已注册后端名称一致；容器名和对象键会按可移植规则验证。

```csharp
services.AddBingFileStorage(options =>
{
    options.DefaultStore = "local-v1";
    options.Containers.Clear();
    options.Containers.Add("attachments");
});
services.AddBingFileSystemStorage("local-v1", options =>
{
    options.RootPath = Path.GetFullPath("data/files");
});

var storage = provider.GetRequiredService<IFileStorageFactory>()
    .GetStorage("local-v1", StorageScope.Host);
```

`IFileStorageFactory` 只解析已注册的命名后端，不会静默回退。`StorageScope` 绑定主机或租户隔离范围，不代替业务层权限检查。后端由依赖注入容器持有；后端工厂创建的可释放实例会随容器释放。本包不创建目录以外的业务数据，也不连接数据库。附件容器的私有或公开策略由 `Bing.Biz.FileManagement` 配置。
