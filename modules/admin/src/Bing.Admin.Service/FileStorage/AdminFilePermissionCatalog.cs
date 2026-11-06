using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Admin.Service.FileStorage;

/// <summary>Admin 文件管理使用的固定资源标识。</summary>
public static class AdminFilePermissionCatalog
{
    /// <summary>资源模块 URI。</summary>
    public const string ModuleUri = "bing-file-management";

    /// <summary>上传文件权限。</summary>
    public const string Upload = "files.upload";

    /// <summary>发布公开文件权限。</summary>
    public const string Publish = "files.publish";

    /// <summary>管理文件权限。</summary>
    public const string Manage = "files.manage";

    /// <summary>访问宿主级文件范围权限。</summary>
    public const string Host = "files.host";

    /// <summary>管理其他用户资料附件；不等同于通用文件管理权限。</summary>
    public const string ManageUserInfoAttachments = "files.user-info.manage";
    /// <summary>管理公共连接或停用个人连接，不授予个人文件读取权。</summary>
    public const string ManageConnections = "files.connections.manage";

    /// <summary>由服务端维护的权限码列表。</summary>
    public static IReadOnlyList<(string Code, string Name)> Operations { get; } = new[]
    {
        (Upload, "上传文件"),
        (Publish, "发布公开文件"),
        (Manage, "管理文件"),
        (Host, "访问宿主文件"),
        (ManageUserInfoAttachments, "管理用户资料附件"),
        (ManageConnections, "管理文件存储连接")
    };

    /// <summary>检查权限码是否属于文件管理权限目录。</summary>
    public static bool Contains(string permission) =>
        Operations.Any(operation => string.Equals(operation.Code, permission, StringComparison.Ordinal));
}
