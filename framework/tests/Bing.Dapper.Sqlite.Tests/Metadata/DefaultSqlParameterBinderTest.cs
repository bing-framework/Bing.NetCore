using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using Bing.Data.Sql;
using Dapper;

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
        var parameter = Assert.Single(command.CreatedParameters);

        // Assert
        Assert.Equal("name", parameter.ParameterName);
        Assert.Equal("abc", parameter.Value);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal(20, parameter.Size);
    }

    /// <summary>
    /// 测试样例
    /// </summary>
    private sealed class Sample
    {
        /// <summary>
        /// 获取或设置字符串值
        /// </summary>
        [StringLength(20)]
        public string StringValue { get; set; }
    }

    /// <summary>
    /// 测试用命令对象
    /// </summary>
    private sealed class FakeDbCommand : IDbCommand
    {
        /// <summary>
        /// 存储模拟命令的参数集合。
        /// </summary>
        private readonly FakeParameterCollection _parameters = new();

        /// <summary>
        /// 获取模拟命令创建的参数列表。
        /// </summary>
        public List<IDbDataParameter> CreatedParameters => _parameters.Items;

        /// <inheritdoc />
        public string CommandText { get; set; }

        /// <inheritdoc />
        public int CommandTimeout { get; set; }

        /// <inheritdoc />
        public CommandType CommandType { get; set; }

        /// <inheritdoc />
        public IDbConnection Connection { get; set; }

        /// <inheritdoc />
        public IDataParameterCollection Parameters => _parameters;

        /// <inheritdoc />
        public IDbTransaction Transaction { get; set; }

        /// <inheritdoc />
        public UpdateRowSource UpdatedRowSource { get; set; }

        /// <inheritdoc />
        public void Cancel() { }

        /// <inheritdoc />
        public IDbDataParameter CreateParameter() => new FakeDbDataParameter();

        /// <inheritdoc />
        public void Dispose() { }

        /// <inheritdoc />
        public int ExecuteNonQuery() => throw new NotSupportedException();

        /// <inheritdoc />
        public IDataReader ExecuteReader() => throw new NotSupportedException();

        /// <inheritdoc />
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();

        /// <inheritdoc />
        public object ExecuteScalar() => throw new NotSupportedException();

        /// <inheritdoc />
        public void Prepare() { }
    }

    /// <summary>
    /// 测试用参数集合
    /// </summary>
    private sealed class FakeParameterCollection : IDataParameterCollection
    {
        /// <summary>
        /// 获取模拟参数集合中的数据库参数。
        /// </summary>
        public List<IDbDataParameter> Items { get; } = new();

        /// <inheritdoc />
        public object this[string parameterName]
        {
            get => Items.FirstOrDefault(t => t.ParameterName == parameterName);
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public object this[int index]
        {
            get => Items[index];
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public bool IsFixedSize => false;

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public int Count => Items.Count;

        /// <inheritdoc />
        public bool IsSynchronized => false;

        /// <inheritdoc />
        public object SyncRoot { get; } = new();

        /// <inheritdoc />
        public int Add(object value)
        {
            Items.Add((IDbDataParameter)value);
            return Items.Count - 1;
        }

        /// <inheritdoc />
        public void Clear() => Items.Clear();

        /// <inheritdoc />
        public bool Contains(string parameterName) => Items.Any(t => t.ParameterName == parameterName);

        /// <inheritdoc />
        public bool Contains(object value) => Items.Contains((IDbDataParameter)value);

        /// <inheritdoc />
        public void CopyTo(Array array, int index) => Items.ToArray().CopyTo(array, index);

        /// <inheritdoc />
        public IEnumerator GetEnumerator() => Items.GetEnumerator();

        /// <inheritdoc />
        public int IndexOf(string parameterName) => Items.FindIndex(t => t.ParameterName == parameterName);

        /// <inheritdoc />
        public int IndexOf(object value) => Items.IndexOf((IDbDataParameter)value);

        /// <inheritdoc />
        public void Insert(int index, object value) => Items.Insert(index, (IDbDataParameter)value);

        /// <inheritdoc />
        public void Remove(object value) => Items.Remove((IDbDataParameter)value);

        /// <inheritdoc />
        public void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                Items.RemoveAt(index);
        }

        /// <inheritdoc />
        public void RemoveAt(int index) => Items.RemoveAt(index);
    }

    /// <summary>
    /// 测试用数据库参数
    /// </summary>
    private sealed class FakeDbDataParameter : IDbDataParameter
    {
        /// <inheritdoc />
        public byte Precision { get; set; }

        /// <inheritdoc />
        public byte Scale { get; set; }

        /// <inheritdoc />
        public int Size { get; set; }

        /// <inheritdoc />
        public DbType DbType { get; set; }

        /// <inheritdoc />
        public ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        /// <inheritdoc />
        public bool IsNullable => true;

        /// <inheritdoc />
        public string ParameterName { get; set; }

        /// <inheritdoc />
        public string SourceColumn { get; set; }

        /// <inheritdoc />
        public DataRowVersion SourceVersion { get; set; }

        /// <inheritdoc />
        public object Value { get; set; }
    }
}
