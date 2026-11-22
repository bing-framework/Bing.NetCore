using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Bing.Data.Sql.Builders.Params;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private static readonly Lazy<MethodInfo> GeneratedParameterBinder = new(CreateGeneratedParameterBinder);

    /// <summary>集合占位符只在 SQL 代码区展开，重复引用共享同一组参数。</summary>
    [Fact]
    public void BindParameters_WhenCollectionTokenAppearsInSqlText_ShouldReplaceOnlyCodeTokens()
    {
        const string sql = """select @ids, @IDS, '@ids', "@ids", [@ids], `@ids`, /* @ids */ -- @ids""" +
            "\n$body$ @ids $body$";
        var command = Bind(sql, new SqlParam("@ids", new[] { 7, 9 }, DbType.Int32));

        Assert.Equal("""select (@ids0,@ids1), (@ids0,@ids1), '@ids', "@ids", [@ids], `@ids`, /* @ids */ -- @ids""" +
            "\n$body$ @ids $body$", command.CommandText);
        Assert.Equal(new[] { "ids0", "ids1" }, ParameterNames(command));
        Assert.Collection(command.Parameters.Cast<DbParameter>(),
            parameter => AssertParameter(parameter, "ids0", 7, DbType.Int32),
            parameter => AssertParameter(parameter, "ids1", 9, DbType.Int32));
    }

    /// <summary>集合展开项与显式标量参数冲突时保留标量名并重命名展开项。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindParameters_WhenCollectionExpansionCollidesWithScalar_ShouldUseUniquePhysicalNames(bool scalarFirst)
    {
        var collection = new SqlParam("@ids", new[] { 7, 9 }, DbType.Int32);
        var scalar = new SqlParam("@IDS0", 41, DbType.Int32);
        var parameters = scalarFirst ? new[] { scalar, collection } : new[] { collection, scalar };
        var command = Bind("select @ids, @IDS0", parameters);

        Assert.Equal("select (@ids0_1,@ids1), @IDS0", command.CommandText);
        Assert.Equal(scalarFirst
            ? new[] { "IDS0", "ids0_1", "ids1" }
            : new[] { "ids0_1", "ids1", "IDS0" }, ParameterNames(command));
        Assert.Equal(scalarFirst
            ? new object[] { 41, 7, 9 }
            : new object[] { 7, 9, 41 }, ParameterValues(command));
        AssertUniqueParameterNames(command);
    }

    /// <summary>两个集合的展开项发生大小写不敏感冲突时按绑定顺序分配唯一名称。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindParameters_WhenCollectionsExpandToSameName_ShouldUseUniquePhysicalNames(bool secondCollectionFirst)
    {
        var first = new SqlParam("@ids", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, DbType.Int32);
        var second = new SqlParam("@IDS1", new[] { 41 }, DbType.Int32);
        var parameters = secondCollectionFirst ? new[] { second, first } : new[] { first, second };
        var command = Bind("select @ids, @IDS1", parameters);
        var expectedIds = Enumerable.Range(0, 10).Select(index => index == 1 ? "@ids1_1" : "@ids" + index)
            .Append(secondCollectionFirst ? "@ids10_1" : "@ids10");
        var expectedSql = "select (" + string.Join(",", expectedIds) + "), " +
            (secondCollectionFirst ? "(@IDS10)" : "(@IDS10_1)");

        Assert.Equal(expectedSql, command.CommandText);
        Assert.Equal(secondCollectionFirst
            ? new[] { "IDS10", "ids0", "ids1_1", "ids2", "ids3", "ids4", "ids5", "ids6", "ids7", "ids8", "ids9", "ids10_1" }
            : new[] { "ids0", "ids1_1", "ids2", "ids3", "ids4", "ids5", "ids6", "ids7", "ids8", "ids9", "ids10", "IDS10_1" },
            ParameterNames(command));
        Assert.Equal(secondCollectionFirst
            ? new object[] { 41, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }
            : new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 41 },
            ParameterValues(command));
        AssertUniqueParameterNames(command);
    }

    /// <summary>空集合只替换集合标记，普通参数和值类型元数据保持不变。</summary>
    [Fact]
    public void BindParameters_WhenCollectionIsEmpty_ShouldPreserveScalarParameterAndMetadata()
    {
        var scalar = new SqlParam("@code", "A-17", DbType.String, ParameterDirection.Input,
            size: 128, precision: 9, scale: 3);
        var command = Bind("select @ids where code=@code and other=@CODE",
            new SqlParam("@ids", Array.Empty<int>(), DbType.Int32), scalar);

        Assert.Equal("select (SELECT 1 WHERE 1 = 0) where code=@code and other=@CODE", command.CommandText);
        Assert.Equal(new[] { "code" }, ParameterNames(command));
        var parameter = Assert.Single(command.Parameters.Cast<DbParameter>());
        Assert.Equal("code", parameter.ParameterName);
        Assert.Equal("A-17", parameter.Value);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal(ParameterDirection.Input, parameter.Direction);
        Assert.Equal(128, parameter.Size);
        Assert.Equal((byte)9, parameter.Precision);
        Assert.Equal((byte)3, parameter.Scale);
    }

    /// <summary>含有方言相关的反斜杠转义时，拒绝可能误改字符串的集合展开。</summary>
    [Fact]
    public void BindParameters_WhenSqlHasAmbiguousBackslashEscapes_ShouldRejectExpansion()
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            Bind("select @ids, 'it\\'s @ids'", new SqlParam("@ids", new[] { 1 })));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    /// <summary>直接绑定时保留已有参数，并对集合项保留类型和长度元数据。</summary>
    [Fact]
    public void BindParameters_Directly_WhenCommandHasParameters_ShouldReserveNamesAndMetadata()
    {
        var command = new RecordingDbCommand("select :ids, :ids0, :data, ':ids'");
        var existing = command.CreateParameter();
        existing.ParameterName = ":IDS0";
        existing.Value = 3;
        command.Parameters.Add(existing);
        var bytes = new byte[] { 1, 2 };

        SqlGeneratedExecution.BindParameters(command, new[]
        {
            new SqlParam(":ids", new[] { 1, 2 }, DbType.Int32, ParameterDirection.Input, size: 8),
            new SqlParam(":data", bytes, DbType.Binary)
        });

        Assert.Equal("select (:ids0_1,:ids1), :ids0, :data, ':ids'", command.CommandText);
        Assert.Equal(new[] { ":IDS0", "ids0_1", "ids1", "data" }, ParameterNames(command));
        Assert.All(command.Parameters.Cast<DbParameter>().Skip(1).Take(2), parameter =>
        {
            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(ParameterDirection.Input, parameter.Direction);
            Assert.Equal(8, parameter.Size);
        });
        Assert.Same(bytes, command.Parameters.Cast<DbParameter>().Last().Value);
    }

    /// <summary>并发绑定使用各自的命令与值，参数名称和内容不会跨请求共享。</summary>
    [Fact]
    public void BindParameters_Directly_WhenRequestsRunConcurrently_ShouldKeepParametersIsolated()
    {
        Parallel.For(0, 32, index =>
        {
            var command = new RecordingDbCommand("select @ids, @ids0");
            SqlGeneratedExecution.BindParameters(command, new[]
            {
                new SqlParam("ids", new[] { index, index + 1 }),
                new SqlParam("ids0", index + 2)
            });

            Assert.Equal("select (@ids0_1,@ids1), @ids0", command.CommandText);
            Assert.Equal(new object[] { index, index + 1, index + 2 }, ParameterValues(command));
        });
    }

    /// <summary>参数序列和集合值各只枚举一次，空元素绑定为数据库空值。</summary>
    [Fact]
    public void BindParameters_Directly_WhenSequencesAreSingleUse_ShouldEnumerateOnce()
    {
        var parameterEnumerations = 0;
        var itemEnumerations = 0;
        IEnumerable<int?> Items()
        {
            Assert.Equal(1, ++itemEnumerations);
            yield return 7;
            yield return null;
        }
        IEnumerable<SqlParam> Parameters()
        {
            Assert.Equal(1, ++parameterEnumerations);
            yield return new SqlParam("ids", Items(), DbType.Int32);
        }
        var command = new RecordingDbCommand("select @ids");

        SqlGeneratedExecution.BindParameters(command, Parameters());

        Assert.Equal("select (@ids0,@ids1)", command.CommandText);
        Assert.Equal(new object[] { 7, DBNull.Value }, ParameterValues(command));
        Assert.Equal(1, parameterEnumerations);
        Assert.Equal(1, itemEnumerations);
    }

    /// <summary>生成式查询继续拒绝非输入参数。</summary>
    [Fact]
    public void BindParameters_Directly_WhenParameterIsOutput_ShouldRejectIt()
    {
        var command = new RecordingDbCommand("select @value");

        Assert.Throws<NotSupportedException>(() => SqlGeneratedExecution.BindParameters(command,
            new[] { new SqlParam("value", null, direction: ParameterDirection.Output) }));
        Assert.Empty(command.Parameters.Cast<DbParameter>());
    }

    private static RecordingDbCommand Bind(string sql, params SqlParam[] parameters)
    {
        var command = new RecordingDbCommand(sql);
        GeneratedParameterBinder.Value.Invoke(null, new object[] { command, parameters });
        return command;
    }

    private static MethodInfo CreateGeneratedParameterBinder()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;
            public sealed class BinderRow { public int Id { get; set; } }
            public static class BinderConsumer
            {
                public static List<BinderRow> Read(SqlLambdaQuery query) => query.ToList<BinderRow>();
            }
            """;
        var result = RunGenerator(source);
        var assembly = LoadGeneratedAssembly(result);
        var queryCalls = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls", throwOnError: true)!;
        return queryCalls.GetMethod("BindParameters", BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private static string[] ParameterNames(RecordingDbCommand command) =>
        command.Parameters.Cast<DbParameter>().Select(parameter => parameter.ParameterName).ToArray();

    private static object[] ParameterValues(RecordingDbCommand command) =>
        command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).ToArray();

    private static void AssertParameter(DbParameter parameter, string name, object value, DbType dbType)
    {
        Assert.Equal(name, parameter.ParameterName);
        Assert.Equal(value, parameter.Value);
        Assert.Equal(dbType, parameter.DbType);
        Assert.Equal(ParameterDirection.Input, parameter.Direction);
    }

    private static void AssertUniqueParameterNames(RecordingDbCommand command)
    {
        var names = ParameterNames(command);
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private sealed class RecordingDbCommand : DbCommand
    {
        private readonly RecordingDbParameterCollection _parameters = new();

        internal RecordingDbCommand(string commandText) => CommandText = commandText;

        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; }
        public override string SourceColumn { get; set; }
        public override object Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override byte Precision { get; set; }
        public override byte Scale { get; set; }
        public override DataRowVersion SourceVersion { get; set; }

        public override void ResetDbType() => DbType = DbType.Object;
    }

    private sealed class RecordingDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new();

        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value);
        }

        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(parameter =>
            string.Equals(parameter.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                RemoveAt(index);
        }

        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
                Add(value);
            else
                SetParameter(index, value);
        }
    }
}
