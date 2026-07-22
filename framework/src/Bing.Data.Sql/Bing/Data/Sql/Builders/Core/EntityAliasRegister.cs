namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 实体别名注册器
/// </summary>
public class EntityAliasRegister : IEntityAliasRegister
{
    #region 属性

    /// <summary>
    /// From子句设置的实体类型
    /// </summary>
    public Type FromType { get; set; }

    /// <summary>
    /// 实体别名
    /// </summary>
    public IReadOnlyDictionary<Type, string> Data { get; }

    /// <summary>
    /// 实体别名映射。
    /// </summary>
    private readonly Dictionary<Type, string> _data;

    /// <summary>
    /// 当前查询范围内已注册的别名。
    /// </summary>
    private readonly HashSet<string> _aliases;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="EntityAliasRegister"/>类型的实例
    /// </summary>
    public EntityAliasRegister(IDictionary<Type, string> data = null, Type fromType = null)
    {
        _data = data == null ? new Dictionary<Type, string>() : new Dictionary<Type, string>(data);
        Data = _data;
        _aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in _data.Values)
            RegisterAlias(alias);
        FromType = fromType;
    }

    #endregion

    #region Register(注册实体别名)

    /// <summary>
    /// 注册实体别名
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <param name="alias">别名</param>
    public void Register(Type entity, string alias)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        RegisterAlias(alias);
        _data[entity] = alias;
    }

    /// <summary>
    /// 替换 From 实体的表别名。
    /// </summary>
    /// <param name="entity">实体类型。</param>
    /// <param name="alias">表别名。</param>
    public void Replace(Type entity, string alias)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        if (_data.TryGetValue(entity, out var currentAlias) && string.IsNullOrWhiteSpace(currentAlias) == false)
            _aliases.Remove(currentAlias);
        RegisterAlias(alias);
        _data[entity] = alias;
    }

    /// <summary>
    /// 注册查询范围内的表别名。
    /// </summary>
    /// <param name="alias">表别名。</param>
    public void RegisterAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;
        if (_aliases.Add(alias.Trim()) == false)
            throw new InvalidOperationException($"查询中已存在表别名 \"{alias}\"。");
    }

    #endregion

    #region Contains(是否包含实体)

    /// <summary>
    /// 是否包含实体
    /// </summary>
    /// <param name="entity">实体类型</param>
    public bool Contains(Type entity) => entity != null && Data.ContainsKey(entity);

    #endregion

    #region GetAlias(获取实体别名)

    /// <summary>
    /// 获取实体别名
    /// </summary>
    /// <param name="entity">实体类型</param>
    public string GetAlias(Type entity)
    {
        if (entity == null)
            return null;
        return _data.TryGetValue(entity, out var alias) ? alias : null;
    }

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    public IEntityAliasRegister Clone() => new EntityAliasRegister(_data, FromType);

    #endregion
}