using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Enums;
using Dapper;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// 测试目的：验证 Dapper 参数绑定器会把增强参数元数据写入实际的 ADO.NET 参数对象。
/// </summary>
public class DefaultSqlParameterBinderTest
{
    /// <summary>
    /// 测试目的：参数绑定运行时契约仅供框架内部使用，公开程序集不得暴露可替换的绑定 SPI。
    /// </summary>
    [Fact]
    public void ParameterBindingRuntimeContracts_ShouldNotBePublic()
    {
        // Arrange
        var exportedTypeNames = typeof(DefaultSqlParameterBinder).Assembly.GetExportedTypes()
            .Select(type => type.Name);

        // Assert
        Assert.DoesNotContain("ISqlParameterBinder", exportedTypeNames);
        Assert.DoesNotContain("ISqlParameterContextBinder", exportedTypeNames);
        Assert.DoesNotContain("IDapperParameterBinder", exportedTypeNames);
        Assert.DoesNotContain("IDapperParameterSet", exportedTypeNames);
        Assert.False(typeof(DefaultSqlParameterBinder).IsPublic);
    }

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
    /// 测试目的：输出参数访问器应区分缺失参数、数据库 NULL 和类型转换失败， Try 模式不得因缺失或不可转换值抛出异常。
    /// </summary>
    [Fact]
    public void OutputParameterAccessor_WhenValueIsMissingNullOrIncompatible_ShouldFollowExplicitContract()
    {
        // Arrange
        var binder = new DefaultSqlParameterBinder();
        var parameters = Assert.IsAssignableFrom<SqlMapper.IDynamicParameters>(binder.Bind(
            new SqlParameterCollection().AddOutput("result", DbType.String)));
        var accessor = Assert.IsAssignableFrom<ISqlOutputParameterAccessor>(parameters);
        var command = new FakeDbCommand();
        parameters.AddParameters(command, null);
        var output = Assert.Single(command.CreatedParameters);

        // Act / Assert - Missing
        accessor.TryGetValue<int>("missing", out _).ShouldBeFalse();
        Should.Throw<KeyNotFoundException>(() => accessor.GetValue<int>("missing"));

        // Act / Assert - Database null
        output.Value = DBNull.Value;
        accessor.GetValue<string>("result").ShouldBeNull();
        accessor.TryGetValue<string>("result", out var nullableValue).ShouldBeTrue();
        nullableValue.ShouldBeNull();
        accessor.TryGetValue<int>("result", out _).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => accessor.GetValue<int>("result"));

        // Act / Assert - Convertible and incompatible values
        output.Value = "42";
        accessor.GetValue<int>("result").ShouldBe(42);
        output.Value = "not-an-int";
        accessor.TryGetValue<int>("result", out _).ShouldBeFalse();
        Should.Throw<InvalidCastException>(() => accessor.GetValue<int>("result"));
    }

    /// <summary>
    /// 测试 - 框架参数集合应在实际命令中保留输入和输出参数的完整元数据。
    /// </summary>
    [Fact]
    public void Bind_WithSqlParameterCollection_ShouldPopulateInputAndOutputDbParameters()
    {
        // Arrange
        var binder = new DefaultSqlParameterBinder();
        var collection = new SqlParameterCollection()
            .Add("@name", "Bing", DbType.String, 32)
            .AddOutput(":result", DbType.Int32);
        var parameters = Assert.IsAssignableFrom<SqlMapper.IDynamicParameters>(binder.Bind(collection));
        var accessor = Assert.IsAssignableFrom<ISqlOutputParameterAccessor>(parameters);
        var command = new FakeDbCommand();

        // Act
        parameters.AddParameters(command, null);
        command.CreatedParameters.Single(t => t.ParameterName == "result").Value = 42;

        // Assert
        command.CreatedParameters.Count.ShouldBe(2);
        var input = command.CreatedParameters.Single(t => t.ParameterName == "name");
        input.Value.ShouldBe("Bing");
        input.Size.ShouldBe(32);
        input.Direction.ShouldBe(ParameterDirection.Input);
        var output = command.CreatedParameters.Single(t => t.ParameterName == "result");
        output.Direction.ShouldBe(ParameterDirection.Output);
        accessor.GetValue<int>("result").ShouldBe(42);
        accessor.TryGetValue<string>("name", out _).ShouldBeFalse();
        Should.Throw<KeyNotFoundException>(() => accessor.GetValue<string>("name"));
    }

    /// <summary>
    /// 测试 - 绑定器从 Builder 的增强参数快照读取时应保留值、原始值和完整数据库元数据。
    /// </summary>
    [Fact]
    public void GetSqlParams_WhenBuilderUsesEnhancedParameterSnapshot_ShouldPreserveMetadata()
    {
        // Arrange
        var builder = new SqlServerBuilder();
        var manager = Assert.IsAssignableFrom<IAdvancedParameterManager>(((ISqlCommonPartAccessor)builder).ParameterManager);
        manager.Add(new SqlParam(":amount", 12.34m, DbType.Decimal, ParameterDirection.InputOutput, 20, 10, 2)
        {
            OriginalValue = "12.34",
            DatabaseType = DatabaseType.SqlServer,
            ProviderTypeName = "decimal",
            Source = SqlParameterSource.Manual,
            MetadataLevel = SqlParameterMetadataLevel.Full,
            StorageKind = ColumnStorageKind.Number,
            ConverterKind = FieldValueConverterKind.Custom,
            CustomConverterName = "CurrencyConverter"
        });
        var binder = new DefaultSqlParameterBinder();

        // Act
        var parameter = Assert.Single(binder.GetSqlParams(builder, new SqlOptions()));

        // Assert
        Assert.Equal("@amount", parameter.Name);
        Assert.Equal(12.34m, parameter.Value);
        Assert.Equal("12.34", parameter.OriginalValue);
        Assert.Equal(DbType.Decimal, parameter.DbType);
        Assert.Equal(ParameterDirection.InputOutput, parameter.Direction);
        Assert.Equal(20, parameter.Size);
        Assert.Equal((byte)10, parameter.Precision);
        Assert.Equal((byte)2, parameter.Scale);
        Assert.Equal(DatabaseType.SqlServer, parameter.DatabaseType);
        Assert.Equal("decimal", parameter.ProviderTypeName);
        Assert.Equal(SqlParameterSource.Manual, parameter.Source);
        Assert.Equal(SqlParameterMetadataLevel.Full, parameter.MetadataLevel);
        Assert.Equal(ColumnStorageKind.Number, parameter.StorageKind);
        Assert.Equal(FieldValueConverterKind.Custom, parameter.ConverterKind);
        Assert.Equal("CurrencyConverter", parameter.CustomConverterName);
    }

    /// <summary>
    /// 测试样例。
    /// </summary>
    private sealed class Sample
    {
        /// <summary>
        /// 获取或设置字符串值。
        /// </summary>
        [StringLength(20)]
        public string StringValue { get; set; }
    }

    /// <summary>
    /// decimal 参数测试样例。
    /// </summary>
    private sealed class DecimalSample
    {
        /// <summary>
        /// 获取或设置金额。
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// 测试用命令对象。
    /// </summary>
    private sealed class FakeDbCommand : IDbCommand
    {
        /// <summary>
        /// 测试命令的参数集合。
        /// </summary>
        private readonly FakeParameterCollection _parameters = new();

        /// <summary>
        /// 获取已创建的数据库参数。
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
    /// 测试用参数集合。
    /// </summary>
    private sealed class FakeParameterCollection : IDataParameterCollection
    {
        /// <summary>
        /// 获取参数项列表。
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
    /// 测试用数据库参数。
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
