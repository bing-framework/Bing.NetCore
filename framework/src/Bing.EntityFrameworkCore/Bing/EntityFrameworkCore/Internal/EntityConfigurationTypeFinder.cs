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
    private static readonly IDictionary<Type, Dictionary<Type, EntityTypeConfigurationMetadata>> _entityRegistersDict;

    /// <summary>
    /// 实体上下文映射字典
    /// </summary>
    private static readonly IDictionary<Type, Type> _entityMapDbContextDict;

    /// <summary>
    /// 空元素字典
    /// </summary>
    private static readonly Dictionary<Type, EntityTypeConfigurationMetadata> _empty;

    /// <summary>
    /// 数据上下文类型哈希表
    /// </summary>
    private static readonly HashSet<Type> _dbContextTypes;

    /// <summary>
    /// 所有程序集查找器
    /// </summary>
    private readonly IAllAssemblyFinder _allAssemblyFinder;

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static EntityConfigurationTypeFinder()
    {
        _entityRegistersDict= new Dictionary<Type, Dictionary<Type, EntityTypeConfigurationMetadata>>();
        _entityMapDbContextDict = new Dictionary<Type, Type>();
        _empty = new Dictionary<Type, EntityTypeConfigurationMetadata>();
        _dbContextTypes = new HashSet<Type>();
    }

    /// <summary>
    /// 初始化一个 <see cref="EntityConfigurationTypeFinder"/> 类型的实例。
    /// </summary>
    /// <param name="allAssemblyFinder">所有程序集查找器</param>
    public EntityConfigurationTypeFinder(IAllAssemblyFinder allAssemblyFinder)
    {
        _allAssemblyFinder = allAssemblyFinder;
    }

    /// <inheritdoc />
    public Dictionary<Type, EntityTypeConfigurationMetadata> GetEntityTypeConfigurations(Type dbContextType)
    {
        return _entityMapDbContextDict.ContainsKey(dbContextType) ? _entityRegistersDict[dbContextType] : _empty;
    }

    /// <inheritdoc />
    public Type GetDbContextTypeForEntity(Type entityType)
    {
        if (!_entityMapDbContextDict.ContainsKey(entityType))
            throw new BingFrameworkException($"[{entityType}]未发现任何数据上下文实体映射配置");
        return _entityMapDbContextDict[entityType];
    }

    /// <inheritdoc />
    public IEnumerable<Type> GetAllDbContextTypes() => _dbContextTypes;

    /// <inheritdoc />
    public bool HasDbContextForEntity<T>() => HasDbContextForEntity(typeof(T));

    /// <inheritdoc />
    public bool HasDbContextForEntity(Type type) => _entityMapDbContextDict.ContainsKey(type);
}
