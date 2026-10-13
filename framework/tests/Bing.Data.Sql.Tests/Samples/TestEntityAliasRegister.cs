using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 实体别名注册器
/// </summary>
public class TestEntityAliasRegister : IEntityAliasRegister
{
    /// <inheritdoc />
    public Type FromType { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<Type, string> Data { get; } = new Dictionary<Type, string>();

    /// <inheritdoc />
    public void Register(Type entity, string alias)
    {
    }

    /// <inheritdoc />
    public void Replace(Type entity, string alias)
    {
    }

    /// <inheritdoc />
    public void RegisterAlias(string alias)
    {
    }

    /// <inheritdoc />
    public bool Contains(Type entity) => true;

    /// <inheritdoc />
    public string GetAlias(Type entity) => $"as_{entity.Name}";

    /// <inheritdoc />
    public string GetSelfJoinAlias(Type entity, bool right) => GetAlias(entity);

    /// <inheritdoc />
    public IEntityAliasRegister Clone() => new EntityAliasRegister(new Dictionary<Type, string>(Data));
}
