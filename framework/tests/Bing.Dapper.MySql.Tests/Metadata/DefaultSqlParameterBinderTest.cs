using System.Collections;
using System.Data;
using Bing.Data.Sql;
using Dapper;
using Xunit;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// 测试目的：验证 Dapper 参数绑定器会把增强参数元数据写入实际的 ADO.NET 参数对象。
/// </summary>
public class DefaultSqlParameterBinderTest
{
    /// <summary>
    /// 测试目的：参数映射中的长度与 DbType 元数据应被写入 IDbDataParameter。
    /// </summary>
    [Fact]
    public void Bind_WithParameterMap_ShouldPopulateDbParameterMetadata()
    {
        // Arrange
        var binder = new DefaultSqlParameterBinder();
        var map = new SqlParameterMap<Sample>().Add("name", t => t.StringValue, "abc");
        var parameters = Assert.IsAssignableFrom<SqlMapper.IDynamicParameters>(binder.Bind(map));
        var command = new FakeDbCommand();

        // Act
        parameters.AddParameters(command, null);
        var parameter = command.CreatedParameters.Single();

        // Assert
        Assert.Equal("name", parameter.ParameterName);
        Assert.Equal("abc", parameter.Value);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal(20, parameter.Size);
    }

    /// <summary>
    /// 默认参数绑定器测试使用的最小实体。
    /// </summary>
    private sealed class Sample
    {
        /// <summary>
        /// 受长度约束的字符串属性。
        /// </summary>
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string StringValue { get; set; }
    }

    /// <summary>
    /// 测试用命令对象
    /// </summary>
    private sealed class FakeDbCommand : IDbCommand
    {
        private readonly FakeParameterCollection _parameters = new();

        public List<IDbDataParameter> CreatedParameters => _parameters.Items;

        public string CommandText { get; set; }

        public int CommandTimeout { get; set; }

        public CommandType CommandType { get; set; }

        public IDbConnection Connection { get; set; }

        public IDataParameterCollection Parameters => _parameters;

        public IDbTransaction Transaction { get; set; }

        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }

        public IDbDataParameter CreateParameter() => new FakeDbDataParameter();

        public void Dispose() { }

        public int ExecuteNonQuery() => throw new NotSupportedException();

        public IDataReader ExecuteReader() => throw new NotSupportedException();

        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();

        public object ExecuteScalar() => throw new NotSupportedException();

        public void Prepare() { }
    }

    /// <summary>
    /// 测试用参数集合
    /// </summary>
    private sealed class FakeParameterCollection : IDataParameterCollection
    {
        public List<IDbDataParameter> Items { get; } = new();

        public object this[string parameterName]
        {
            get => Items.FirstOrDefault(t => t.ParameterName == parameterName);
            set => throw new NotSupportedException();
        }

        public object this[int index]
        {
            get => Items[index];
            set => throw new NotSupportedException();
        }

        public bool IsFixedSize => false;

        public bool IsReadOnly => false;

        public int Count => Items.Count;

        public bool IsSynchronized => false;

        public object SyncRoot { get; } = new();

        public int Add(object value)
        {
            Items.Add((IDbDataParameter)value);
            return Items.Count - 1;
        }

        public void Clear() => Items.Clear();

        public bool Contains(string parameterName) => Items.Any(t => t.ParameterName == parameterName);

        public bool Contains(object value) => Items.Contains((IDbDataParameter)value);

        public void CopyTo(Array array, int index) => Items.ToArray().CopyTo(array, index);

        public IEnumerator GetEnumerator() => Items.GetEnumerator();

        public int IndexOf(string parameterName) => Items.FindIndex(t => t.ParameterName == parameterName);

        public int IndexOf(object value) => Items.IndexOf((IDbDataParameter)value);

        public void Insert(int index, object value) => Items.Insert(index, (IDbDataParameter)value);

        public void Remove(object value) => Items.Remove((IDbDataParameter)value);

        public void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                Items.RemoveAt(index);
        }

        public void RemoveAt(int index) => Items.RemoveAt(index);
    }

    /// <summary>
    /// 测试用数据库参数
    /// </summary>
    private sealed class FakeDbDataParameter : IDbDataParameter
    {
        public byte Precision { get; set; }

        public byte Scale { get; set; }

        public int Size { get; set; }

        public DbType DbType { get; set; }

        public ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public bool IsNullable => true;

        public string ParameterName { get; set; }

        public string SourceColumn { get; set; }

        public DataRowVersion SourceVersion { get; set; }

        public object Value { get; set; }
    }
}
