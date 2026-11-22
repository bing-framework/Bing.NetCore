using System.Collections;
using System.Data;
using System.Data.Common;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using Xunit;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>
/// 写入命令生成式参数绑定契约测试。
/// </summary>
public sealed class SqlWriteCommandGeneratedBinderTest
{
    /// <summary>
    /// 测试目的：生成式绑定器应与冻结 SQL、参数和执行元数据一起保留，并可通过副本替换绑定器。
    /// </summary>
    [Fact]
    public void WithGeneratedParameterBinder_ShouldPreserveSqlParametersAndExecutionMetadata()
    {
        // Arrange
        var binder = new Action<DbCommand, IEnumerable<SqlParam>>((_, _) => { });
        var command = new SqlWriteCommand(
            "Update Users Set Name=@_p_0 Where Id=@_p_1",
            new[]
            {
                new SqlParam("@_p_0", "Ada"),
                new SqlParam("@_p_1", 7)
            },
            validateAffectedRows: true)
            .WithGeneratedParameterBinder(binder);

        // Act
        var parameters = command.CreateParameters().ToArray();
        var replacement = new Action<DbCommand, IEnumerable<SqlParam>>((_, _) => { });
        var copied = command.WithGeneratedParameterBinder(replacement);
        var validationCopy = command.WithValidateAffectedRows(false);

        // Assert
        Assert.Equal("Update Users Set Name=@_p_0 Where Id=@_p_1", command.Sql);
        Assert.True(command.IsGeneratedExecution);
        Assert.Same(binder, command.GeneratedParameterBinder);
        Assert.True(command.ValidateAffectedRows);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, parameters.Select(item => item.Name));
        Assert.Equal(new object[] { "Ada", 7 }, parameters.Select(item => item.Value));
        Assert.NotSame(command, copied);
        Assert.Same(replacement, copied.GeneratedParameterBinder);
        Assert.Equal(command.Sql, copied.Sql);
        Assert.Equal(command.ValidateAffectedRows, copied.ValidateAffectedRows);
        Assert.False(validationCopy.ValidateAffectedRows);
        Assert.Same(binder, validationCopy.GeneratedParameterBinder);
    }

    /// <summary>
    /// 测试目的：静态写入参数绑定器应直接复制冻结参数及其 ADO.NET 元数据。
    /// </summary>
    [Fact]
    public void GeneratedBinder_ShouldCreateTypedParametersWithoutReflectionBinder()
    {
        // Arrange
        var command = new RecordingDbCommand();
        var parameters = new[]
        {
            new SqlParam("@name", "Ada", DbType.String, ParameterDirection.Input, size: 64),
            new SqlParam("@id", 7, DbType.Int32)
        };

        // Act
        SqlGeneratedParameterBinding.Bind(command, parameters);

        // Assert
        Assert.Equal(new[] { "@name", "@id" }, command.Parameters.Cast<DbParameter>()
            .Select(item => item.ParameterName));
        Assert.Equal(new object[] { "Ada", 7 }, command.Parameters.Cast<DbParameter>()
            .Select(item => item.Value));
        var name = Assert.IsType<RecordingDbParameter>(command.Parameters[0]);
        Assert.Equal(DbType.String, name.DbType);
        Assert.Equal(ParameterDirection.Input, name.Direction);
        Assert.Equal(64, name.Size);
    }

    private sealed class RecordingDbCommand : DbCommand
    {
        private readonly RecordingDbParameterCollection _parameters = new();

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
