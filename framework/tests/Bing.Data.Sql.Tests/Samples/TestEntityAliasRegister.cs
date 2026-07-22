using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 实体别名注册器
/// </summary>
public class TestEntityAliasRegister : IEntityAliasRegister
{
    /// <summary>
    /// From子句设置的实体类型
    /// </summary>
    public Type FromType { get; set; }

    /// <summary>
    /// 实体别名
    /// </summary>
    public IReadOnlyDictionary<Type, string> Data { get; } = new Dictionary<Type, string>();

    /// <summary>
    /// 注册实体别名
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <param name="alias">别名</param>
    public void Register(Type entity, string alias)
    {
    }

    /// <summary>
    /// 替换 From 实体的表别名。
    /// </summary>
    /// <param name="entity">实体类型。</param>
    /// <param name="alias">表别名。</param>
    public void Replace(Type entity, string alias)
    {
    }

    /// <summary>
    /// 注册查询范围内的表别名。
    /// </summary>
    /// <param name="alias">表别名。</param>
    public void RegisterAlias(string alias)
    {
    }

    /// <summary>
    /// 是否包含实体
    /// </summary>
    /// <param name="entity">实体类型</param>
    public bool Contains(Type entity) => true;

    /// <summary>
    /// 获取实体别名
    /// </summary>
    /// <param name="entity">实体类型</param>
    public string GetAlias(Type entity) => $"as_{entity.Name}";

    /// <summary>
    /// 复制实体别名注册器
    /// </summary>
    public IEntityAliasRegister Clone() => new EntityAliasRegister(new Dictionary<Type, string>(Data));
}
