using System.Collections;
using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 生成式参数源与数据库参数定制器的兼容性测试。
/// </summary>
public sealed class SqlGeneratedParameterCustomizationTest
{
    /// <summary>
    /// 测试目的：生成式参数源应按通用绑定器的顺序应用 Provider 定制器和显式类型适配器。
    /// </summary>
    [Fact]
    public void Apply_ShouldPreserveProviderThenTypeHandlerOrder()
    {
        // Arrange
        var order = new List<string>();
        var provider = new RecordingCustomizer("provider", order,
            (parameter, _) => parameter.Value = "provider");
        var adapter = new RecordingAdapter(order);
        var dbParameter = new Mock<IDbDataParameter>();
        dbParameter.SetupAllProperties();
        dbParameter.Object.ParameterName = ":ID";
        var collection = new TestParameterCollection();
        collection.Add(dbParameter.Object);
        var command = new Mock<IDbCommand>();
        command.SetupGet(item => item.Parameters).Returns(collection);
        var sqlParameter = new SqlParam("@id", 7)
        {
            DatabaseType = DatabaseType.SqlServer
        };

        // Act
        SqlGeneratedParameterCustomization.Apply(command.Object, new[] { sqlParameter },
            new ISqlDbParameterCustomizer[] { adapter, provider });

        // Assert
        Assert.Equal(new[] { "provider", "adapter" }, order);
        Assert.Equal(14, dbParameter.Object.Value);
        Assert.Equal(17, dbParameter.Object.Size);
    }

    private sealed class RecordingCustomizer : ISqlDbParameterCustomizer
    {
        private readonly string _name;
        private readonly IList<string> _order;
        private readonly Action<IDbDataParameter, SqlParam> _configure;

        public RecordingCustomizer(string name, IList<string> order,
            Action<IDbDataParameter, SqlParam> configure)
        {
            _name = name;
            _order = order;
            _configure = configure;
        }

        public bool CanHandle(DatabaseType databaseType) => true;

        public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
        {
            _order.Add(_name);
            _configure(dbParameter, sqlParameter);
        }
    }

    private sealed class RecordingAdapter : ISqlDbParameterCustomizer, ISqlTypeHandlerParameterAdapter
    {
        private readonly IList<string> _order;

        public RecordingAdapter(IList<string> order) => _order = order;

        public bool CanHandle(DatabaseType databaseType) => true;

        public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
        {
            _order.Add("adapter");
            dbParameter.Value = (int)sqlParameter.Value * 2;
            dbParameter.Size = 17;
        }
    }

    private sealed class TestParameterCollection : IDataParameterCollection
    {
        private readonly List<IDbDataParameter> _items = new();

        public object this[string parameterName]
        {
            get => _items.FirstOrDefault(item => string.Equals(item.ParameterName, parameterName,
                StringComparison.OrdinalIgnoreCase));
            set => throw new NotSupportedException();
        }

        public object this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException();
        }

        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public int Count => _items.Count;
        public bool IsSynchronized => false;
        public object SyncRoot { get; } = new();

        public int Add(object value)
        {
            _items.Add((IDbDataParameter)value);
            return _items.Count - 1;
        }

        public void Clear() => _items.Clear();
        public bool Contains(string parameterName) => IndexOf(parameterName) >= 0;
        public bool Contains(object value) => _items.Contains((IDbDataParameter)value);
        public void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);
        public IEnumerator GetEnumerator() => _items.GetEnumerator();
        public int IndexOf(string parameterName) => _items.FindIndex(item =>
            string.Equals(item.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
        public int IndexOf(object value) => _items.IndexOf((IDbDataParameter)value);
        public void Insert(int index, object value) => _items.Insert(index, (IDbDataParameter)value);
        public void Remove(object value) => _items.Remove((IDbDataParameter)value);
        public void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                _items.RemoveAt(index);
        }

        public void RemoveAt(int index) => _items.RemoveAt(index);
    }
}
