using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>冻结片段与自定义参数管理器组合时的绑定行为。</summary>
public sealed class SqlBuilderFragmentParameterManagerTest
{
    /// <summary>嵌套 Builder 在自定义管理器下也应隔离同名的不同逻辑键。</summary>
    [Fact]
    public void Cte_WhenNestedBuilderUsesCustomManager_ShouldPreserveKeysAcrossRepeatedRenders()
    {
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var first = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", "same") })
            .WithParameterKey("id", firstKey);
        var second = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", "same") })
            .WithParameterKey("id", secondKey);
        var child = new TestSqlBuilder(parameterManager: new UppercaseParameterManager(TestDialect.Instance))
            .Select("*").From(second, "leaf");
        var root = new TestSqlBuilder(parameterManager: new UppercaseParameterManager(TestDialect.Instance))
            .Select("*").From("root").With("first", first).With("second", child);
        const string expected = "With [first] \r\nAs (select @id as Id),\r\n[second] \r\nAs (Select * \r\nFrom (select @_p_0 as Id) As [leaf])\r\nSelect * \r\nFrom [root]";

        Assert.Equal(expected, root.ToSql());
        Assert.Equal(expected, root.ToSql());
        var template = root.ToTemplate();
        Assert.Equal(new[] { firstKey, secondKey }, template.Slots);
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [firstKey] = "left", [secondKey] = "right" });
        Assert.Equal(new object[] { "left", "right" }, rendered.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>自定义管理器的旧式渲染路径也需保留不同片段的逻辑参数身份。</summary>
    [Fact]
    public void CteAndUnion_WhenCustomManagerUsesDifferentKeys_ShouldKeepIndependentBindings()
    {
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var first = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", "same") })
            .WithParameterKey("id", firstKey);
        var second = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", "same") })
            .WithParameterKey("id", secondKey);
        var manager = new UppercaseParameterManager(TestDialect.Instance);
        var builder = new TestSqlBuilder(parameterManager: manager).Select("*").From("items")
            .With("items", first).UnionAll(second);
        const string expected = "With [items] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [items] \r\n) \r\nUnion All \r\n(select @_p_0 as Id \r\n)";

        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        var template = builder.ToTemplate();
        Assert.Equal(expected, template.Sql);
        Assert.Equal(new[] { firstKey, secondKey }, template.Slots);
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [firstKey] = "first", [secondKey] = "second" });
        Assert.Equal(new object[] { "first", "second" }, rendered.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>自定义管理器渲染失败时，候选逻辑键和参数均应回滚。</summary>
    [Fact]
    public void CteAndUnion_WhenCustomManagerRenderFails_ShouldRestoreLogicalKeys()
    {
        var key = new SqlParameterKey("request", "id");
        var cte = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", "same") })
            .WithParameterKey("id", key);
        var invalid = new SqlQueryFragment("select 'it\\'s @ID' as text, @ID as Id",
            new[] { new SqlParam("id", "other") });
        var manager = new UppercaseParameterManager(TestDialect.Instance);
        var builder = new TestSqlBuilder(parameterManager: manager).Select("*").From("items")
            .With("items", cte).UnionAll(invalid);

        Assert.Throws<InvalidOperationException>(() => builder.ToSql());
        Assert.Empty(builder.GetParams());
        builder.ClearUnionBuilders().AddParam("id", "same");
        var template = builder.ToTemplate();

        Assert.Equal("With [items] \r\nAs (select @_p_0 as Id)\r\nSelect * \r\nFrom [items]", template.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, template.Slots);
    }

    /// <summary>同值弱元数据覆盖已缓存名称后，片段应保留强元数据并改用独立名称。</summary>
    [Fact]
    public void CteAndUnion_WhenSameValueOverwritesCachedName_ShouldKeepExternalAndStrongFragmentParameters()
    {
        var cte = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7, DbType.Int32) });
        var union = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 8, DbType.Int64) });
        var builder = new TestSqlBuilder().Select("*").From("active_users")
            .With("active_users", cte).UnionAll(union);
        const string firstSql = "With [active_users] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @_p_0 as Id \r\n)";

        Assert.Equal(firstSql, builder.ToSql());
        Assert.Equal(DbType.Int32, builder.GetSqlParams()["@id"].DbType);
        Assert.Equal(DbType.Int64, builder.GetSqlParams()["@_p_0"].DbType);

        builder.AddParam("_p_0", 8);

        const string expectedSql = "With [active_users] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @_p_1 as Id \r\n)";
        Assert.Equal(expectedSql, builder.ToSql());

        var parameters = builder.GetSqlParams();
        Assert.Equal(8, builder.GetParam("_p_0"));
        Assert.Null(parameters["@_p_0"].DbType);
        Assert.Equal(SqlParameterMetadataLevel.Weak, parameters["@_p_0"].MetadataLevel);
        Assert.Equal(8, parameters["@_p_1"].Value);
        Assert.Equal(DbType.Int64, parameters["@_p_1"].DbType);
    }

    /// <summary>管理器规范化参数值时，名称冲突下重复渲染应复用绑定结果。</summary>
    [Fact]
    public void Cte_WhenCustomManagerNormalizesValuesAndNameCollides_ShouldKeepRepeatedRenderStable()
    {
        var manager = new UppercaseParameterManager(TestDialect.Instance);
        var builder = new TestSqlBuilder(parameterManager: manager).Select("*").From("items");
        builder.AddParam("id", "outer");
        builder.With("items", new SqlQueryFragment("select @id as value",
            new[] { new SqlParam("id", "inner") }));

        const string expectedSql = "With [items] \r\nAs (select @_p_0 as value)\r\nSelect * \r\nFrom [items]";
        var firstSql = builder.ToSql();
        var firstParameters = builder.GetParams();
        var secondSql = builder.ToSql();
        var secondParameters = builder.GetParams();

        Assert.Equal(expectedSql, firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(2, firstParameters.Count);
        Assert.Equal(firstParameters.Count, secondParameters.Count);
        Assert.Equal("OUTER", firstParameters["@id"]);
        Assert.Equal("INNER", firstParameters["@_p_0"]);
        Assert.Equal(firstParameters["@id"], secondParameters["@id"]);
        Assert.Equal(firstParameters["@_p_0"], secondParameters["@_p_0"]);
    }

    /// <summary>第三方管理器遇到片段内部同值异类型参数时保留各自元数据。</summary>
    [Fact]
    public void From_WhenCustomManagerHasEqualPrefixedValuesWithDifferentMetadata_ShouldKeepDistinctParameters()
    {
        var manager = new UppercaseParameterManager(TestDialect.Instance);
        var fragment = new SqlQueryFragment("select @id as first, :id as second", new[]
        {
            new SqlParam("@id", "VALUE", DbType.String),
            new SqlParam(":id", "VALUE", DbType.AnsiString)
        });
        var builder = new TestSqlBuilder(parameterManager: manager).Select("*").From(fragment, "items");

        Assert.Equal("Select * \r\nFrom (select @id as first, @_p_0 as second) As [items]", builder.ToSql());
        Assert.Equal(DbType.String, builder.GetSqlParams()["@id"].DbType);
        Assert.Equal(DbType.AnsiString, builder.GetSqlParams()["@_p_0"].DbType);
    }

    private sealed class UppercaseParameterManager : IAdvancedParameterManager
    {
        private readonly ParameterManager _inner;

        public UppercaseParameterManager(IDialect dialect) : this(new ParameterManager(dialect)) { }

        private UppercaseParameterManager(ParameterManager inner) => _inner = inner;

        public int Count => _inner.Count;

        public string GenerateName() => _inner.GenerateName();

        public string NormalizeName(string name) => _inner.NormalizeName(name);

        public void Add(string name, object value, Operator? @operator = null) =>
            _inner.Add(name, NormalizeValue(value), @operator);

        public void Add(SqlParam parameter)
        {
            if (parameter == null)
                return;
            var copy = new SqlParam(parameter.Name, NormalizeValue(parameter.Value), parameter.DbType,
                parameter.Direction, parameter.Size, parameter.Precision, parameter.Scale)
            {
                EntityType = parameter.EntityType,
                OriginalValue = parameter.OriginalValue,
                IsProviderValue = parameter.IsProviderValue,
                PropertyName = parameter.PropertyName,
                ColumnName = parameter.ColumnName,
                DatabaseType = parameter.DatabaseType,
                ProviderTypeName = parameter.ProviderTypeName,
                Source = parameter.Source,
                MetadataLevel = parameter.MetadataLevel,
                StorageKind = parameter.StorageKind,
                ConverterKind = parameter.ConverterKind,
                CustomConverterName = parameter.CustomConverterName
            };
            _inner.Add(copy);
        }

        public IReadOnlyDictionary<string, object> GetParams() => _inner.GetParams();

        public void Restore(SqlParam parameter) => _inner.Restore(parameter);

        public bool Remove(string name) => _inner.Remove(name);

        public IReadOnlyDictionary<string, SqlParam> GetSqlParams() => _inner.GetSqlParams();

        public IReadOnlyDictionary<string, object> ExportValues() => _inner.ExportValues();

        public bool Contains(string name) => _inner.Contains(name);

        public object GetValue(string name) => _inner.GetValue(name);

        public IParameterManager Clone() => new UppercaseParameterManager((ParameterManager)_inner.Clone());

        public IParameterManager CreateEmpty() => new UppercaseParameterManager((ParameterManager)_inner.CreateEmpty());

        public void Clear() => _inner.Clear();

        private static object NormalizeValue(object value) => value is string text ? text.ToUpperInvariant() : value;
    }
}
