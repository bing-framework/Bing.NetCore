using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    /// 测试 - 绑定器应把 decimal 参数的精度与小数位写入数据库参数。
    /// </summary>
    [Fact]
    public void DapperParameterBinder_ShouldApplyPrecisionAndScale()
    {
        // Arrange
        var binder = new DefaultSqlParameterBinder();
        var map = new SqlParameterMap<DecimalSample>().Add("amount", t => t.Amount, 12.34m);
        var parameters = Assert.IsAssignableFrom<SqlMapper.IDynamicParameters>(binder.Bind(map));
        var command = new FakeDbCommand();

        // Act
        parameters.AddParameters(command, null);
        var parameter = Assert.Single(command.CreatedParameters);

        // Assert
        Assert.Equal("amount", parameter.ParameterName);
        Assert.Equal(DbType.Decimal, parameter.DbType);
        Assert.Equal((byte)10, parameter.Precision);
        Assert.Equal((byte)2, parameter.Scale);
    }

    /// <summary>
    /// 测试 - 输出参数访问器应读取执行完成后 ADO 参数的最终值。
    /// </summary>
    [Fact]
    public void Bind_WithOutputParameter_ShouldExposeFinalValueThroughAccessor()
    {
        // Arrange
        var binder = new DefaultSqlParameterBinder();
        var map = new SqlParameterMap<Sample>().AddOutput("result", t => t.StringValue, DbType.String, 20);
        var parameters = Assert.IsAssignableFrom<SqlMapper.IDynamicParameters>(binder.Bind(map));
        var accessor = Assert.IsAssignableFrom<ISqlOutputParameterAccessor>(parameters);
        var command = new FakeDbCommand();

        // Act
        parameters.AddParameters(command, null);
        command.CreatedParameters.Single().Value = "done";

        // Assert
        accessor.GetValue<string>("@result").ShouldBe("done");
        accessor.TryGetValue<string>(":result", out var value).ShouldBeTrue();
        value.ShouldBe("done");
    }

    /// <summary>
    /// 测试样例
    /// </summary>
    private sealed class Sample
    {
        /// <summary>
        /// 字符串值
        /// </summary>
        [StringLength(20)]
        public string StringValue { get; set; }
    }

    /// <summary>
    /// decimal 参数测试样例
    /// </summary>
    private sealed class DecimalSample
    {
        /// <summary>
        /// 金额
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }
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
