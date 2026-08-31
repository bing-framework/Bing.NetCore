namespace Bing.Data.Sql.Builders;

/// <summary>
/// 实体别名注册器
/// </summary>
public interface IEntityAliasRegister
{
    /// <summary>
    /// From子句设置的实体类型
    /// </summary>
    Type FromType { get; set; }

    /// <summary>
    /// 实体别名
    /// </summary>
    IReadOnlyDictionary<Type, string> Data { get; }

    /// <summary>
    /// 注册实体别名
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <param name="alias">别名</param>
    void Register(Type entity, string alias);

    /// <summary>
    /// 替换 From 实体的表别名。
    /// </summary>
    /// <param name="entity">实体类型。</param>
    /// <param name="alias">表别名。</param>
    void Replace(Type entity, string alias);

    /// <summary>
    /// 注册查询范围内的表别名。
    /// </summary>
    /// <param name="alias">表别名。</param>
    void RegisterAlias(string alias);

    /// <summary>
    /// 是否包含实体
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <returns>包含指定实体类型时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool Contains(Type entity);

    /// <summary>
    /// 获取实体别名
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <returns>指定实体的当前别名；未注册时返回 <see langword="null"/>。</returns>
    string GetAlias(Type entity);

    /// <summary>
    /// 获取同实体自连接 On 条件使用的别名。
    /// </summary>
    /// <param name="entity">实体类型。</param>
    /// <param name="right">是否为连接条件右侧。</param>
    /// <returns>匹配的表别名。</returns>
    string GetSelfJoinAlias(Type entity, bool right);

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns>当前别名注册状态的独立副本。</returns>
    IEntityAliasRegister Clone();
}