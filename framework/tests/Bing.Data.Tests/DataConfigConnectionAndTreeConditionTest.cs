using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Queries.Conditions;
using Bing.Trees;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Data.Tests;

// ─── 测试辅助：实体样本（同时实现 IPath + IEnabled + IParentId<Guid?>）────────

file class TreeEntity : IPath, IEnabled, IParentId<Guid?>
{
    public string Path { get; set; }
    public int Level { get; set; }
    public bool Enabled { get; set; }
    public Guid? ParentId { get; set; }
}

// ─── ConnectionStringCollection 测试 ─────────────────────────────

/// <summary>
/// <see cref="ConnectionStringCollection"/> 单元测试
/// </summary>
public class ConnectionStringCollectionTest
{
    /// <summary>
    /// 测试目的：常量 DefaultConnectionStringName 值应为 "Default"。
    /// </summary>
    [Fact]
    public void DefaultConnectionStringName_ShouldBe_Default()
    {
        // Assert
        ConnectionStringCollection.DefaultConnectionStringName.ShouldBe("Default");
    }

    /// <summary>
    /// 测试目的：Default 属性赋值后可正确读取。
    /// </summary>
    [Fact]
    public void Default_SetAndGet_ShouldReturnSameValue()
    {
        // Arrange
        var col = new ConnectionStringCollection();

        // Act
        col.Default = "Server=localhost;Database=test";

        // Assert
        col.Default.ShouldBe("Server=localhost;Database=test");
    }

    /// <summary>
    /// 测试目的：Default 未赋值时应返回 null。
    /// </summary>
    [Fact]
    public void Default_WhenNotSet_ShouldReturnNull()
    {
        // Arrange
        var col = new ConnectionStringCollection();

        // Assert
        col.Default.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetConnectionString 传入存在的名称应返回对应值。
    /// </summary>
    [Fact]
    public void GetConnectionString_ExistingName_ShouldReturnValue()
    {
        // Arrange
        var col = new ConnectionStringCollection();
        col["Slave"] = "Server=slave;";

        // Act
        var result = col.GetConnectionString("Slave");

        // Assert
        result.ShouldBe("Server=slave;");
    }

    /// <summary>
    /// 测试目的：GetConnectionString 传入不存在的名称应回退到 Default。
    /// </summary>
    [Fact]
    public void GetConnectionString_MissingName_ShouldFallbackToDefault()
    {
        // Arrange
        var col = new ConnectionStringCollection();
        col.Default = "Server=default;";

        // Act
        var result = col.GetConnectionString("NotExist");

        // Assert
        result.ShouldBe("Server=default;");
    }

    /// <summary>
    /// 测试目的：连接字符串名称缺失或为空白时应回退到默认连接字符串。
    /// </summary>
    [Fact]
    public void GetConnectionString_NameMissingOrWhitespace_ShouldFallbackToDefault()
    {
        // Arrange
        var col = new ConnectionStringCollection { Default = "Server=default;" };

        // Act and Assert
        col.GetConnectionString(null).ShouldBe("Server=default;");
        col.GetConnectionString(string.Empty).ShouldBe("Server=default;");
        col.GetConnectionString("   ").ShouldBe("Server=default;");
    }

    /// <summary>
    /// 测试目的：GetConnectionString 传入 "Default" 应返回 Default 属性的值。
    /// </summary>
    [Fact]
    public void GetConnectionString_Default_ShouldReturnDefaultValue()
    {
        // Arrange
        var col = new ConnectionStringCollection();
        col.Default = "Server=master;";

        // Act
        var result = col.GetConnectionString("Default");

        // Assert
        result.ShouldBe("Server=master;");
    }
}

// ─── DataConfig 测试 ──────────────────────────────────────────────

/// <summary>
/// <see cref="DataConfig"/> 单元测试
/// </summary>
public class DataConfigTest
{
    /// <summary>
    /// 测试目的：默认 LogLevel 应为 DataLogLevel.Sql。
    /// </summary>
    [Fact]
    public void Default_LogLevel_ShouldBeSql()
    {
        // Act
        var config = new DataConfig();

        // Assert
        config.LogLevel.ShouldBe(DataLogLevel.Sql);
    }

    /// <summary>
    /// 测试目的：默认 AutoCommit 应为 false。
    /// </summary>
    [Fact]
    public void Default_AutoCommit_ShouldBeFalse()
    {
        // Act
        var config = new DataConfig();

        // Assert
        config.AutoCommit.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：默认 EnabledValidateVersion 应为 true。
    /// </summary>
    [Fact]
    public void Default_EnabledValidateVersion_ShouldBeTrue()
    {
        // Act
        var config = new DataConfig();

        // Assert
        config.EnabledValidateVersion.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：默认 EnabledDeleteFilter 应为 true。
    /// </summary>
    [Fact]
    public void Default_EnabledDeleteFilter_ShouldBeTrue()
    {
        // Act
        var config = new DataConfig();

        // Assert
        config.EnabledDeleteFilter.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：默认构造器应自动初始化 SqlOptions 属性（非 null）。
    /// </summary>
    [Fact]
    public void Default_SqlOptions_ShouldNotBeNull()
    {
        // Act
        var config = new DataConfig();

        // Assert
        config.SqlOptions.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：默认 AdoLogInterceptor 应为 null。
    /// </summary>
    [Fact]
    public void Default_AdoLogInterceptor_ShouldBeNull()
    {
        // Act
        var config = new DataConfig();

        // Assert
        config.AdoLogInterceptor.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：各属性赋值后应正确读取。
    /// </summary>
    [Fact]
    public void Properties_SetAndGet_ShouldRoundtrip()
    {
        // Arrange
        var config = new DataConfig();

        // Act
        config.LogLevel = DataLogLevel.All;
        config.AutoCommit = true;
        config.EnabledValidateVersion = false;
        config.EnabledDeleteFilter = false;

        // Assert
        config.LogLevel.ShouldBe(DataLogLevel.All);
        config.AutoCommit.ShouldBeTrue();
        config.EnabledValidateVersion.ShouldBeFalse();
        config.EnabledDeleteFilter.ShouldBeFalse();
    }
}

// ─── SqlOptions 测试 ──────────────────────────────────────────────

/// <summary>
/// <see cref="SqlOptions"/> 单元测试
/// </summary>
public class SqlOptionsTest
{
    /// <summary>
    /// 测试目的：默认 DatabaseType 应为 SqlServer。
    /// </summary>
    [Fact]
    public void Default_DatabaseType_ShouldBeSqlServer()
    {
        // Act
        var options = new SqlOptions();

        // Assert
        options.DatabaseType.ShouldBe(DatabaseType.SqlServer);
    }

    /// <summary>
    /// 测试目的：默认 ConnectionString 和 Connection 均为 null。
    /// </summary>
    [Fact]
    public void Default_ConnectionFields_ShouldBeNull()
    {
        // Act
        var options = new SqlOptions();

        // Assert
        options.ConnectionString.ShouldBeNull();
        options.Connection.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：各属性赋值后应正确读取。
    /// </summary>
    [Fact]
    public void Properties_SetAndGet_ShouldRoundtrip()
    {
        // Arrange
        var options = new SqlOptions();
        var mockConn = new Mock<IDbConnection>().Object;

        // Act
        options.DatabaseType = DatabaseType.MySql;
        options.ConnectionString = "Server=localhost";
        options.Connection = mockConn;

        // Assert
        options.DatabaseType.ShouldBe(DatabaseType.MySql);
        options.ConnectionString.ShouldBe("Server=localhost");
        options.Connection.ShouldBeSameAs(mockConn);
    }

    /// <summary>
    /// 测试目的：SQL 选项不应再公开未参与执行或诊断的日志级别和类别配置。
    /// </summary>
    [Fact]
    public void PublicApi_WhenInspected_ShouldNotExposeUnusedLogConfiguration()
    {
        // Arrange and Act
        var type = typeof(SqlOptions);

        // Assert
        type.GetProperty("LogLevel").ShouldBeNull();
        type.GetProperty("LogCategory").ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：SqlOptions&lt;T&gt; 是 SqlOptions 的子类，属性应继承。
    /// </summary>
    [Fact]
    public void SqlOptionsGeneric_ShouldInheritFromSqlOptions()
    {
        // Act
        var options = new SqlOptions<DataConfigTest>();

        // Assert
        (options is SqlOptions).ShouldBeTrue();
        options.DatabaseType.ShouldBe(DatabaseType.SqlServer); // 继承默认值
    }
}

// ─── DefaultDatabase 测试 ─────────────────────────────────────────

/// <summary>
/// <see cref="DefaultDatabase"/> 单元测试
/// </summary>
public class DefaultDatabaseTest
{
    /// <summary>
    /// 测试目的：构造函数传入 null 连接时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_WhenConnectionIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DefaultDatabase(null));
    }

    /// <summary>
    /// 测试目的：GetConnection 应返回构造时传入的同一连接实例。
    /// </summary>
    [Fact]
    public void GetConnection_ShouldReturnSameInstanceAsProvided()
    {
        // Arrange
        var mockConn = new Mock<IDbConnection>().Object;
        var db = new DefaultDatabase(mockConn);

        // Act
        var result = db.GetConnection();

        // Assert
        result.ShouldBeSameAs(mockConn);
    }
}

// ─── TreeCondition 测试 ──────────────────────────────────────────

/// <summary>
/// <see cref="TreeCondition{TEntity, TParentId}"/> 单元测试
/// </summary>
public class TreeConditionTest
{
    private static Mock<ITreeQueryParameter<Guid?>> CreateParam(
        string path = null, int? level = null, bool? enabled = null)
    {
        var mock = new Mock<ITreeQueryParameter<Guid?>>();
        mock.Setup(p => p.Path).Returns(path);
        mock.Setup(p => p.Level).Returns(level);
        mock.Setup(p => p.Enabled).Returns(enabled);
        mock.Setup(p => p.ParentId).Returns((Guid?)null);
        return mock;
    }

    /// <summary>
    /// 测试目的：参数为 null 时，不应抛出异常，且 GetCondition 返回 null。
    /// </summary>
    [Fact]
    public void Constructor_WhenParameterIsNull_ShouldNotThrow()
    {
        // Act
        var condition = Should.NotThrow(() =>
            new TreeCondition<TreeEntity, Guid?>(null));

        // Assert
        condition.GetCondition().ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：所有参数均为空时，GetCondition 应返回 null（无约束）。
    /// </summary>
    [Fact]
    public void GetCondition_WhenAllParamsAreNull_ShouldReturnNull()
    {
        // Arrange
        var param = CreateParam();
        var condition = new TreeCondition<TreeEntity, Guid?>(param.Object);

        // Assert
        condition.GetCondition().ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：设置 Path 时，GetCondition 应只匹配路径前缀包含该路径的实体。
    /// </summary>
    [Fact]
    public void GetCondition_WithPath_ShouldFilterByPathPrefix()
    {
        // Arrange
        var param = CreateParam(path: "/root/");
        var cond = new TreeCondition<TreeEntity, Guid?>(param.Object);
        var expr = cond.GetCondition();

        var entities = new List<TreeEntity>
        {
            new() { Path = "/root/child1/", Level = 2, Enabled = true },
            new() { Path = "/other/", Level = 1, Enabled = true },
        };

        // Act
        var results = entities.AsQueryable().Where(expr).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Path.ShouldBe("/root/child1/");
    }

    /// <summary>
    /// 测试目的：设置 Level 时，GetCondition 应只匹配对应级数的实体。
    /// </summary>
    [Fact]
    public void GetCondition_WithLevel_ShouldFilterByLevel()
    {
        // Arrange
        var param = CreateParam(level: 2);
        var cond = new TreeCondition<TreeEntity, Guid?>(param.Object);
        var expr = cond.GetCondition();

        var entities = new List<TreeEntity>
        {
            new() { Path = "/a/", Level = 1, Enabled = true },
            new() { Path = "/a/b/", Level = 2, Enabled = true },
            new() { Path = "/a/b/c/", Level = 3, Enabled = true },
        };

        // Act
        var results = entities.AsQueryable().Where(expr).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Level.ShouldBe(2);
    }

    /// <summary>
    /// 测试目的：设置 Enabled = true 时，GetCondition 应只匹配已启用的实体。
    /// </summary>
    [Fact]
    public void GetCondition_WithEnabled_ShouldFilterByEnabledFlag()
    {
        // Arrange
        var param = CreateParam(enabled: true);
        var cond = new TreeCondition<TreeEntity, Guid?>(param.Object);
        var expr = cond.GetCondition();

        var entities = new List<TreeEntity>
        {
            new() { Path = "/a/", Level = 1, Enabled = true },
            new() { Path = "/b/", Level = 1, Enabled = false },
        };

        // Act
        var results = entities.AsQueryable().Where(expr).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Enabled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：同时设置 Path + Level + Enabled 时，应组合 AND 条件过滤。
    /// </summary>
    [Fact]
    public void GetCondition_CombinedConditions_ShouldApplyAll()
    {
        // Arrange
        var param = CreateParam(path: "/root/", level: 2, enabled: true);
        var cond = new TreeCondition<TreeEntity, Guid?>(param.Object);
        var expr = cond.GetCondition();

        var entities = new List<TreeEntity>
        {
            new() { Path = "/root/a/", Level = 2, Enabled = true },   // 全匹配
            new() { Path = "/root/a/", Level = 2, Enabled = false },  // Enabled 不匹配
            new() { Path = "/root/a/", Level = 3, Enabled = true },   // Level 不匹配
            new() { Path = "/other/a/", Level = 2, Enabled = true },  // Path 不匹配
        };

        // Act
        var results = entities.AsQueryable().Where(expr).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Path.ShouldBe("/root/a/");
        results[0].Level.ShouldBe(2);
        results[0].Enabled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：TreeCondition&lt;TEntity&gt;（Guid? 简化版）构造应正常工作。
    /// </summary>
    [Fact]
    public void TreeCondition_GuidShorthand_ShouldWork()
    {
        // Arrange
        var param = CreateParam(enabled: false);
        var cond = new TreeCondition<TreeEntity>(param.Object);

        // Assert
        cond.GetCondition().ShouldNotBeNull();
    }
}
