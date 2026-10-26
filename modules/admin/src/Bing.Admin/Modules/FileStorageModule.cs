using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.Admin.Service.FileStorage;
using Bing.Admin.Apis.Commons;
using Bing.Auditing.Persistence;
using Bing.MultiTenancy;
using Bing.Core.Modularity;
using Bing.FileStorage;
using Bing.FileStorage.FileSystem;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;

namespace Bing.Admin.Modules;

/// <summary>两套宿主共用的文件配置，ORM 接入由各宿主的 partial 实现选择。</summary>
public partial class FileStorageModule : BingModule
{
    public override ModuleLevel Level => ModuleLevel.Framework;
    public override int Order => 2;
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        var configuration = services.GetConfiguration();
        var section = configuration.GetSection("FileManagement");
        if (!section.GetValue<bool>("Enabled"))
        {
            services.AddControllers().ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new DisabledFileControllers()));
            return services;
        }
        services.AddBingFileStorage(options =>
        {
            options.DefaultStore = section["DefaultStore"] ?? "local-v1";
            options.Containers.Clear();
            var containers = section.GetSection("Containers").GetChildren();
            foreach (var container in containers) options.Containers.Add(container.Key);
            if (options.Containers.Count == 0) { options.Containers.Add("attachments"); options.Containers.Add("public"); }
        });
        var stores = section.GetSection("Stores").GetChildren();
        var count = 0;
        foreach (var store in stores)
        {
            count++;
            if (string.Equals(store["Provider"], "Minio", StringComparison.OrdinalIgnoreCase))
                services.AddBingMinioStorage(store.Key, options => store.Bind(options));
            else if (string.Equals(store["Provider"], "Qiniu", StringComparison.OrdinalIgnoreCase))
                services.AddBingQiniuStorage(store.Key, options => store.Bind(options));
            else if (string.Equals(store["Provider"], "FileSystem", StringComparison.OrdinalIgnoreCase))
                services.AddBingFileSystemStorage(store.Key, options => store.Bind(options));
            else throw new InvalidOperationException("未知文件存储 Provider：" + store["Provider"]);
        }
        if (count == 0) throw new InvalidOperationException("启用文件管理必须显式配置 FileManagement:Stores 持久存储。");
        services.AddBingFileManagement(options =>
        {
            section.Bind(options);
        });
        services.AddBingAdminFileStoragePermissions();
        services.TryAddScoped<ICurrentTenant, AdminFileCurrentTenant>();
        services.Replace(ServiceDescriptor.Scoped<IFileAccessAuthorizer, AdminUserInfoFileAuthorizer>());
        ConfigureMetadata(services, configuration);
        if (section.GetValue("LifecycleAudit:Enabled", true))
        {
            var connection = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("生命周期审计需要 Admin DefaultConnection。");
            services.AddSingleton(new AdminFileAuditDatabase(AuditLogDatabase.MySql, () => new MySqlConnection(connection)));
            services.AddScoped<IFileLifecycleEventHandler, AdminFileLifecycleAuditHandler>();
        }
        var operations = new AdminFileOperationsOptions();
        section.GetSection("Operations").Bind(operations);
        services.AddSingleton(operations);
        if (operations.MetricsEnabled)
        {
            services.AddSingleton(_ => new AdminFileMetricsExporter(stores.Select(store => store.Key)));
            services.AddHostedService<AdminFileMetricsStartup>();
        }
        services.AddBingFileManagementWeb(section.GetValue("RunWorker", true), health => section.GetSection("Health").Bind(health),
            console => section.GetSection("Console").Bind(console));
        return services;
    }
    private static partial void ConfigureMetadata(IServiceCollection services, IConfiguration configuration);
    private sealed class DisabledFileControllers : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(System.Collections.Generic.IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            for (var index = feature.Controllers.Count - 1; index >= 0; index--)
                if (typeof(FileManagementControllerBase).IsAssignableFrom(feature.Controllers[index]) || feature.Controllers[index].AsType() == typeof(FileOperationsController)) feature.Controllers.RemoveAt(index);
        }
    }
}
