using System;
using System.Collections.Generic;
using Bing.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Bing.EntityFrameworkCore.Internal;

/// <summary>
/// 实体类配置类型查找器
/// </summary>
internal sealed class EntityConfigurationTypeFinder : IEntityConfigurationTypeFinder
{
    /// <summary>
    /// 实体类型注册字典
    /// </summary>
    private static readonly IDictionary<Type, Dictionary<Type, EntityTypeConfigurationMetadata>> EntityRegistersDict;

    /// <summary>
    /// 实体上下文映射字典
    /// </summary>
    private static readonly IDictionary<Type, Type> EntityMapDbContextDict;

    /// <summary>
    /// 空元素字典
    /// </summary>
    private static readonly Dictionary<Type, EntityTypeConfigurationMetadata> Empty;

    /// <summary>
    /// 数据上下文类型哈希表
    /// </summary>
    private static readonly HashSet<Type> DbContextTypes;

    /// <summary>
    /// 所有程序集查找器
    /// </summary>
    private readonly IAllAssemblyFinder _allAssemblyFinder;

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static EntityConfigurationTypeFinder()
    {
        EntityRegistersDict= new Dictionary<Type, Dictionary<Type, EntityTypeConfigurationMetadata>>();
        EntityMapDbContextDict = new Dictionary<Type, Type>();
        Empty = new Dictionary<Type, EntityTypeConfigurationMetadata>();
        DbContextTypes = new HashSet<Type>();
    }

    /// <summary>
    /// 初始化一个<see cref="EntityConfigurationTypeFinder"/>类型的实例
    /// </summary>
    /// <param name="allAssemblyFinder">所有程序集查找器</param>
    public EntityConfigurationTypeFinder(IAllAssemblyFinder allAssemblyFinder)
    {
        _allAssemblyFinder = allAssemblyFinder;
    }

    /// <summary>
    /// 获取指定上下文类型的实体配置注册信息
    /// </summary>
    /// <param name="dbContextType">数据上下文类型</param>
    public Dictionary<Type, EntityTypeConfigurationMetadata> GetEntityTypeConfigurations(Type dbContextType)
    {
        return EntityMapDbContextDict.ContainsKey(dbContextType) ? EntityRegistersDict[dbContextType] : Empty;
    }

    /// <summary>
    /// 获取实体类型所属的数据上下文类型
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>数据上下文类型</returns>
    public Type GetDbContextTypeForEntity(Type entityType)
    {
        if (!EntityMapDbContextDict.ContainsKey(entityType))
            throw new BingFrameworkException($"[{entityType}]未发现任何数据上下文实体映射配置");
        return EntityMapDbContextDict[entityType];
    }

    /// <summary>
    /// 获取全部数据上下文的实体类型
    /// </summary>
    public IEnumerable<Type> GetAllDbContextTypes() => DbContextTypes;

    /// <summary>
    /// 判断实体是否配置到 <see cref="DbContext"/> 当中
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public bool HasDbContextForEntity<T>() => HasDbContextForEntity(typeof(T));

    /// <summary>
    /// 判断实体是否配置到 <see cref="DbContext"/> 当中
    /// </summary>
    /// <param name="type">实体类型</param>
    public bool HasDbContextForEntity(Type type) => EntityMapDbContextDict.ContainsKey(type);
}
