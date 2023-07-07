using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 测试Sql生成器
/// </summary>
public class TestSqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// Sql方言
    /// </summary>
    private readonly IDialect _dialect;

    /// <summary>
    /// 初始化Sql生成器
    /// </summary>
    public TestSqlBuilder(IDialect dialect = null, IEntityMetadata metadata = null, ITableDatabase tableDatabase = null, IParameterManager parameterManager = null)
        : base(metadata, tableDatabase, parameterManager)
    {
        _dialect = dialect;
    }

    /// <inheritdoc />
    protected override IDialect GetDialect()
    {
        if (_dialect != null)
            return _dialect;
        return TestDialect.Instance;
    }

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var result = new TestSqlBuilder();
        result.Clone(this);
        return result;
    }

    /// <inheritdoc />
    public override ISqlBuilder New()
    {
        return new TestSqlBuilder(Dialect, EntityMetadata, TableDatabase, ParameterManager);
    }

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Offset {GetOffsetParam()} Rows Fetch Next {GetLimitParam()} Rows Only";
}
