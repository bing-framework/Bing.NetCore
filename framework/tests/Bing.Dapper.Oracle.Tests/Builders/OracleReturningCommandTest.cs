using System.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Dapper.Tests.Builders;

/// <summary>Oracle 写入回传的 SQL 和冻结参数契约。</summary>
public sealed class OracleReturningCommandTest
{
    [Fact]
    public void ReturningInto_WhenBuiltFromUpdate_ShouldFreezeSqlAndTypedOutputs()
    {
        var builder = new SqlUpdateBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .Update(new SqlTableReference { TableName = "samples" }).Where("Id", 42).Set("Name", "updated");
        var command = builder.BuildCommand().ReturningInto(new OracleReturningColumn("Id", DbType.Decimal, name: "id"),
            new OracleReturningColumn("Name", DbType.String, size: 200, name: "returned_name"));
        builder.Clear();

        Assert.Equal("Begin Update \"samples\" Set \"Name\" = :p_1 Where \"Id\"=:p_0 Returning \"Id\", \"Name\" Into :id, :returned_name; " +
            ":bing_returning_rows := SQL%ROWCOUNT; End;", command.Sql);
        Assert.Collection(command.Parameters,
            parameter => { Assert.Equal(":p_0", parameter.Name); Assert.Equal("42", parameter.Value); },
            parameter => { Assert.Equal(":p_1", parameter.Name); Assert.Equal("updated", parameter.Value); },
            parameter => AssertOutput(parameter, "id", DbType.Decimal),
            parameter => AssertOutput(parameter, "returned_name", DbType.String, 200),
            parameter => AssertOutput(parameter, "bing_returning_rows", DbType.Int32));
        command.Parameters.First().Value = -1;
        Assert.Equal("42", command.Parameters.First().Value);
    }

    [Fact]
    public void ReturningInto_WhenWriteShapeIsUnsupported_ShouldRejectBeforeExecution()
    {
        var column = new OracleReturningColumn("Id", DbType.Decimal);
        var unbound = new SqlWriteCommand("Insert Into samples (Id) Values (42)", Array.Empty<SqlParam>());
        var insertSelect = new SqlWriteCommand("Insert Into samples (Id) Select 42 From Dual", Array.Empty<SqlParam>(),
            OracleSqlProvider.Instance, SqlOperationKind.InsertSelect);

        Assert.Throws<NotSupportedException>(() => unbound.ReturningInto(column));
        Assert.Throws<NotSupportedException>(() => insertSelect.ReturningInto(column));
    }

    [Fact]
    public void ReturningInto_WhenOutputNameConflictsWithInput_ShouldRejectBeforeExecution()
    {
        var command = new SqlDeleteBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 42).BuildCommand();

        Assert.Throws<ArgumentException>(() => command.ReturningInto(new OracleReturningColumn("Id", DbType.Decimal, name: "P_0")));
    }

    [Fact]
    public void ReturningInto_WhenOutputNameIsNotAParameterIdentifier_ShouldRejectBeforeBuild()
    {
        var command = new SqlDeleteBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 42).BuildCommand();

        Assert.Throws<ArgumentException>(() => command.ReturningInto(new OracleReturningColumn("Id", DbType.Decimal, name: "id,other")));
    }

    [Fact]
    public void ReturningColumn_WhenSizedOutputHasNoLength_ShouldRejectTheDescriptor()
    {
        Assert.Throws<ArgumentException>(() => new OracleReturningColumn("Name", DbType.String));
    }

    [Fact]
    public void ReturningManyInto_WhenBuiltFromUpdate_ShouldFreezeSqlAndTypedOutputs()
    {
        var builder = new SqlUpdateBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .Update(new SqlTableReference { TableName = "samples" }).Where("Id", 42).Set("Name", "updated");
        var columns = new[]
        {
            new OracleReturningColumn("Id", DbType.Decimal),
            new OracleReturningColumn("Name", DbType.String, size: 200, name: "returned_name")
        };
        var command = builder.BuildCommand().ReturningManyInto(4, columns);
        columns[1] = new OracleReturningColumn("Other", DbType.Int32);
        builder.Clear();

        Assert.Equal("Declare Type bing_return_col_0_t Is Table Of Number Index By Pls_Integer; " +
            "bing_return_col_0 bing_return_col_0_t; Type bing_return_col_1_t Is Table Of NVarchar2(200) Index By Pls_Integer; " +
            "bing_return_col_1 bing_return_col_1_t; Begin Update \"samples\" Set \"Name\" = :p_1 Where \"Id\"=:p_0 " +
            "Returning \"Id\", \"Name\" Bulk Collect Into bing_return_col_0, bing_return_col_1; :bing_returning_rows := SQL%ROWCOUNT; " +
            "If :bing_returning_rows > :bing_returning_max_rows Then Raise_Application_Error(-20060, 'ReturningManyInto row limit exceeded'); End If; " +
            ":Id := bing_return_col_0; :returned_name := bing_return_col_1; End;", command.Sql);
        Assert.Collection(command.Parameters,
            parameter => { Assert.Equal(":p_0", parameter.Name); Assert.Equal("42", parameter.Value); },
            parameter => { Assert.Equal(":p_1", parameter.Name); Assert.Equal("updated", parameter.Value); },
            parameter => AssertOutput(parameter, "Id", DbType.Decimal),
            parameter => AssertOutput(parameter, "returned_name", DbType.String, 200),
            parameter => AssertOutput(parameter, "bing_returning_rows", DbType.Int32),
            parameter =>
            {
                Assert.Equal("bing_returning_max_rows", parameter.Name);
                Assert.Equal(4, parameter.Value);
                Assert.Equal(DbType.Int32, parameter.DbType);
                Assert.Equal(ParameterDirection.Input, parameter.Direction);
            });
    }

    [Fact]
    public void ReturningManyInto_WhenReturningFixedTextAndBinaryFloat_ShouldFreezeSqlAndTypedOutputs()
    {
        var command = new SqlDeleteBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 42).BuildCommand()
            .ReturningManyInto(4,
                new OracleReturningColumn("FixedText", DbType.AnsiStringFixedLength, size: 4),
                new OracleReturningColumn("NationalText", DbType.StringFixedLength, size: 8),
                new OracleReturningColumn("Weight", DbType.Single));

        Assert.Equal("Declare Type bing_return_col_0_t Is Table Of Char(4 Char) Index By Pls_Integer; " +
            "bing_return_col_0 bing_return_col_0_t; Type bing_return_col_1_t Is Table Of NChar(8) Index By Pls_Integer; " +
            "bing_return_col_1 bing_return_col_1_t; Type bing_return_col_2_t Is Table Of Binary_Float Index By Pls_Integer; " +
            "bing_return_col_2 bing_return_col_2_t; Begin Delete From \"samples\" Where \"Id\"=:p_0 " +
            "Returning \"FixedText\", \"NationalText\", \"Weight\" Bulk Collect Into bing_return_col_0, bing_return_col_1, bing_return_col_2; " +
            ":bing_returning_rows := SQL%ROWCOUNT; " +
            "If :bing_returning_rows > :bing_returning_max_rows Then Raise_Application_Error(-20060, 'ReturningManyInto row limit exceeded'); End If; " +
            ":FixedText := bing_return_col_0; :NationalText := bing_return_col_1; :Weight := bing_return_col_2; End;", command.Sql);
        Assert.Collection(command.Parameters,
            parameter => Assert.Equal(":p_0", parameter.Name),
            parameter => AssertOutput(parameter, "FixedText", DbType.AnsiStringFixedLength, 4),
            parameter => AssertOutput(parameter, "NationalText", DbType.StringFixedLength, 8),
            parameter => AssertOutput(parameter, "Weight", DbType.Single),
            parameter => AssertOutput(parameter, "bing_returning_rows", DbType.Int32),
            parameter => Assert.Equal(4, parameter.Value));
    }

    [Fact]
    public void ReturningManyInto_WhenColumnTypeIsUnsupported_ShouldRejectBeforeBuild()
    {
        var command = new SqlDeleteBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 42).BuildCommand();

        Assert.Throws<NotSupportedException>(() => command.ReturningManyInto(4,
            new OracleReturningColumn("Unmapped", DbType.Object)));
    }

    [Fact]
    public void ReturningManyInto_WhenOutputNameIsReservedForCapacity_ShouldRejectBeforeBuild()
    {
        var command = new SqlDeleteBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 42).BuildCommand();

        Assert.Throws<ArgumentException>(() => command.ReturningManyInto(4,
            new OracleReturningColumn("Id", DbType.Decimal, name: "BING_RETURNING_MAX_ROWS")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReturningManyInto_WhenCapacityIsNotPositive_ShouldRejectBeforeBuild(int maxRows)
    {
        var command = new SqlDeleteBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 42).BuildCommand();

        Assert.Throws<ArgumentOutOfRangeException>(() => command.ReturningManyInto(maxRows,
            new OracleReturningColumn("Id", DbType.Decimal)));
    }

    [Fact]
    public void ReturningManyInto_WhenCommandRequiresOneRow_ShouldRejectBeforeBuild()
    {
        var command = new SqlDeleteBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 42).BuildCommand()
            .WithValidateAffectedRows(true);

        Assert.Throws<NotSupportedException>(() => command.ReturningManyInto(4,
            new OracleReturningColumn("Id", DbType.Decimal)));
    }

    [Fact]
    public void ReturningManyInto_WhenWriteIsInsertValues_ShouldRejectBeforeBuild()
    {
        var command = new SqlInsertBuilder(OracleSqlProvider.Instance, SqlBuilderServices.CreateDefault())
            .InsertInto(new SqlTableReference { TableName = "samples" }).Columns("Id").Values(42).BuildCommand();

        Assert.Throws<NotSupportedException>(() => command.ReturningManyInto(4,
            new OracleReturningColumn("Id", DbType.Decimal)));
    }

    private static void AssertOutput(SqlParam parameter, string name, DbType dbType, int? size = null)
    {
        Assert.Equal(name, parameter.Name);
        Assert.Equal(dbType, parameter.DbType);
        Assert.Equal(ParameterDirection.Output, parameter.Direction);
        Assert.Equal(size, parameter.Size);
    }
}
