using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Bing.EntityFrameworkCore;

/// <summary>
/// 实体类配置类型查找器
/// </summary>
public interface IEntityConfigurationTypeFinder
{
    /// <summary>
    /// 获取指定上下文类型的实体配置注册信息
    /// </summary>
    /// <param name="dbContextType">数据上下文类型</param>
    Dictionary<Type, EntityTypeConfigurationMetadata> GetEntityTypeConfigurations(Type dbContextType);

    /// <summary>
    /// 获取实体类型所属的数据上下文类型
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>数据上下文类型</returns>
    Type GetDbContextTypeForEntity(Type entityType);

    /// <summary>
    /// 获取全部数据上下文的实体类型
    /// </summary>
    IEnumerable<Type> GetAllDbContextTypes();

    /// <summary>
    /// 判断实体是否配置到 <see cref="DbContext"/> 当中
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    bool HasDbContextForEntity<T>();

    /// <summary>
    /// 判断实体是否配置到 <see cref="DbContext"/> 当中
    /// </summary>
    /// <param name="type">实体类型</param>
    bool HasDbContextForEntity(Type type);
}


/// <summary>
/// 实体类型配置元数据
/// </summary>
public struct EntityTypeConfigurationMetadata
{
    /// <summary>
    /// 初始化一个<see cref="EntityTypeConfigurationMetadata"/>类型的实例
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <param name="entityTypeConfiguration">实体类型配置</param>
    public EntityTypeConfigurationMetadata(Type entityType, MethodInfo methodInfo, object entityTypeConfiguration)
    {
        EntityType = entityType;
        MethodInfo = methodInfo;
        EntityTypeConfiguration = entityTypeConfiguration;
    }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// 方法信息
    /// </summary>
    public MethodInfo MethodInfo { get; }

    /// <summary>
    /// 实体类型配置
    /// </summary>
    public object EntityTypeConfiguration { get; }
}
