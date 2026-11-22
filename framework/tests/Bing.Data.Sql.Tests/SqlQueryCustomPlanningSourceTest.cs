using System.Data;
using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>验证冻结查询使用自定义参数管理器派生分页时保留转换后的参数值。</summary>
public sealed class SqlQueryCustomPlanningSourceTest
{
    /// <summary>自定义管理器的冻结值在默认终结和连续分页中仍由原来的参数契约处理。</summary>
    [Theory]
    [MemberData(nameof(CustomManagerCases))]
    public void CustomManager_ShouldPreserveFrozenValuesAcrossDerivedPages(string entry, bool grouped, bool hasPage, bool advanced)
    {
        var input = new byte[] { 1, 2 };
        var manager = new TransformingParameterManager(TestDialect.Instance);
        var builder = new TestSqlBuilder(parameterManager: advanced ? manager : new BasicParameterManager(manager)).Select("Name").From("samples")
            .Where("Payload", input).OrderBy("Id");
        if (grouped)
            builder.GroupBy("Name");
        if (hasPage)
            builder.Skip(5).Take(2);
        var source = entry == "freeze" ? SqlQueryPlan.Create(builder).Freeze()
            : new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder).GetPlan(entry == "paging");
        var body = "Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0" +
            (grouped ? " \r\nGroup By [Name]" : string.Empty);
        var sourceSql = body + " \r\nOrder By [Id]" +
            (hasPage ? " \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only" : string.Empty);
        var countSql = grouped ? "Select Count(*) \r\nFrom (" + body + ") As [t]"
            : "Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@_p_0";
        var conversions = manager.ConversionCount;
        input[0] = 9;
        var current = source;

        for (var index = 0; index < 3; index++)
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(current, new Pager(index + 2, 2, "Id"));
            Assert.Equal(body + " \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
            var parameters = Assert.IsType<SqlParam[]>(page.Parameters);
            Assert.Equal(new[] { "@_p_0", "@_p_1", "@_p_2" }, parameters.Select(item => item.Name));
            Assert.Equal(new byte[] { 2, 3 }, Assert.IsType<byte[]>(parameters[0].Value));
            Assert.Equal((index + 1) * 2, parameters[1].Value);
            Assert.Equal(2, parameters[2].Value);
            var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
            Assert.Equal(countSql, count.CommandText);
            Assert.Equal(new byte[] { 2, 3 }, Assert.IsType<byte[]>(Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value));
            Assert.IsType<byte[]>(parameters[0].Value)[0] = 77;
            current = page;
        }
        Assert.Equal(conversions, manager.ConversionCount);
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(new byte[] { 2, 3 }, Assert.IsType<byte[]>(Assert.IsType<SqlParam[]>(source.Parameters)[0].Value));
    }

    /// <summary>覆盖普通/增强自定义管理器、三种终结入口和已有分页/分组组合。</summary>
    public static IEnumerable<object[]> CustomManagerCases =>
        from entry in new[] { "freeze", "query", "paging" }
        from grouped in new[] { false, true }
        from hasPage in new[] { false, true }
        from advanced in new[] { false, true }
        select new object[] { entry, grouped, hasPage, advanced };

    /// <summary>仅开放基础参数契约，验证回退派生不依赖增强接口。</summary>
    private sealed class BasicParameterManager : IParameterManager
    {
        private readonly IParameterManager _inner;
        internal BasicParameterManager(IParameterManager inner) => _inner = inner;
        public int Count => _inner.Count;
        public string GenerateName() => _inner.GenerateName();
        public string NormalizeName(string name) => _inner.NormalizeName(name);
        public void Add(string name, object value, Operator? @operator = null) => _inner.Add(name, value, @operator);
        public void Restore(SqlParam parameter) => _inner.Restore(parameter);
        public bool Remove(string name) => _inner.Remove(name);
        public IReadOnlyDictionary<string, object> GetParams() => _inner.GetParams();
        public bool Contains(string name) => _inner.Contains(name);
        public object GetValue(string name) => _inner.GetValue(name);
        public IParameterManager Clone() => new BasicParameterManager(_inner.Clone());
        public IParameterManager CreateEmpty() => new BasicParameterManager(_inner.CreateEmpty());
        public void Clear() => _inner.Clear();
    }

    /// <summary>自定义参数管理器再次转换冻结值时，Count 和 Page 仍应使用冻结的字节数组。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Freeze_ShouldPreserveCustomParameterValuesAndMetadataWhenDerivingCountAndPage(bool hasExistingPage)
    {
        var input = new byte[] { 1, 2 };
        var manager = new TransformingParameterManager(TestDialect.Instance);
        var builder = new TestSqlBuilder(TestDialect.Instance, parameterManager: manager)
            .Select("Name").From("samples").Where("Payload", input).OrderBy("Id");
        if (hasExistingPage)
            builder.Skip(5).Take(2);
        ((IAdvancedParameterManager)((ISqlCommonPartAccessor)builder).ParameterManager).Add(
            new SqlParam("@_p_0", input, DbType.Binary)
            {
                OriginalValue = input,
                IsProviderValue = true
            });
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var conversionsAtFreeze = manager.ConversionCount;
        input[0] = 9;
        var pager = new Pager(2, 1, "Id");

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(frozen, pager, new TestSqlBuilder());
        var frozenParameter = Assert.Single(Assert.IsType<SqlParam[]>(frozen.Parameters),
            parameter => parameter.Name == "@_p_0");
        var countParameter = Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters));
        var pageParameter = Assert.IsType<SqlParam[]>(page.Parameters)[0];

        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@_p_0", count.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new byte[] { 2, 3 }, Assert.IsType<byte[]>(frozenParameter.Value));
        Assert.Equal(new byte[] { 2, 3 }, Assert.IsType<byte[]>(countParameter.Value));
        Assert.Equal(new byte[] { 2, 3 }, Assert.IsType<byte[]>(pageParameter.Value));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(countParameter.OriginalValue));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(pageParameter.OriginalValue));
        Assert.Equal(DbType.Binary, countParameter.DbType);
        Assert.Equal(DbType.Binary, pageParameter.DbType);
        Assert.True(countParameter.IsProviderValue);
        Assert.True(pageParameter.IsProviderValue);
        Assert.Equal(conversionsAtFreeze, manager.ConversionCount);
    }

    /// <summary>带分组的计数包装也不应重新转换自定义管理器中的参数。</summary>
    [Fact]
    public void Freeze_GroupedCount_ShouldReuseCustomManagerParameters()
    {
        var manager = new TransformingParameterManager(TestDialect.Instance);
        var input = new byte[] { 1, 2 };
        var builder = new TestSqlBuilder(TestDialect.Instance, parameterManager: manager)
            .Select("Name").From("samples").Where("Payload", input)
            .GroupBy("Name").OrderBy("Name").Skip(1).Take(2);
        manager.Add(new SqlParam("@_p_0", input, DbType.Binary) { OriginalValue = input, IsProviderValue = true });
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        input[0] = 9;
        var conversionsAtFreeze = manager.ConversionCount;

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);

        Assert.Equal("Select Count(*) \r\nFrom (Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 \r\nGroup By [Name]) As [t]", count.CommandText);
        var parameter = Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters));
        Assert.Equal(new byte[] { 2, 3 }, Assert.IsType<byte[]>(parameter.Value));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameter.OriginalValue));
        Assert.Equal(DbType.Binary, parameter.DbType);
        Assert.True(parameter.IsProviderValue);
        Assert.Equal(conversionsAtFreeze, manager.ConversionCount);
    }

    /// <summary>仅用于覆盖 Add 参数时具有非幂等值转换的自定义管理器。</summary>
    private sealed class TransformingParameterManager : IAdvancedParameterManager
    {
        private readonly ParameterManager _inner;
        private readonly ConversionCounter _counter;

        public TransformingParameterManager(IDialect dialect) : this(new ParameterManager(dialect), new ConversionCounter()) { }

        private TransformingParameterManager(ParameterManager inner, ConversionCounter counter)
        {
            _inner = inner;
            _counter = counter;
        }

        public int ConversionCount => _counter.Count;

        public int Count => _inner.Count;

        public string GenerateName() => _inner.GenerateName();

        public string NormalizeName(string name) => _inner.NormalizeName(name);

        public void Add(string name, object value, Operator? @operator = null) =>
            _inner.Add(name, Transform(value), @operator);

        public void Add(SqlParam parameter)
        {
            if (parameter == null)
                return;
            var transformed = new SqlParam(parameter.Name, Transform(parameter.Value), parameter.DbType,
                parameter.Direction, parameter.Size, parameter.Precision, parameter.Scale)
            {
                OriginalValue = parameter.OriginalValue,
                IsProviderValue = parameter.IsProviderValue,
                EntityType = parameter.EntityType,
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
            ((IAdvancedParameterManager)_inner).Add(transformed);
        }

        public void Restore(SqlParam parameter) => _inner.Restore(parameter);

        public bool Remove(string name) => _inner.Remove(name);

        public IReadOnlyDictionary<string, object> GetParams() => _inner.GetParams();

        public IReadOnlyDictionary<string, SqlParam> GetSqlParams() => ((IAdvancedParameterManager)_inner).GetSqlParams();

        public IReadOnlyDictionary<string, object> ExportValues() => ((IAdvancedParameterManager)_inner).ExportValues();

        public bool Contains(string name) => _inner.Contains(name);

        public object GetValue(string name) => _inner.GetValue(name);

        public IParameterManager Clone() => new TransformingParameterManager((ParameterManager)_inner.Clone(), _counter);

        public IParameterManager CreateEmpty() =>
            new TransformingParameterManager((ParameterManager)_inner.CreateEmpty(), _counter);

        public void Clear() => _inner.Clear();

        private object Transform(object value)
        {
            if (value is not byte[] bytes)
                return value;
            _counter.Count++;
            var result = (byte[])bytes.Clone();
            for (var index = 0; index < result.Length; index++)
                result[index]++;
            return result;
        }

        private sealed class ConversionCounter
        {
            public int Count { get; set; }
        }
    }
}
