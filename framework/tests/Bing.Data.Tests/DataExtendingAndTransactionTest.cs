using System.Data;
using System.Threading.Tasks;
using Bing.Data.ObjectExtending;
using Bing.Data.Transaction;
using Bing.Trees;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Data.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// ExtraPropertyDictionary
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// ExtraPropertyDictionary 及其扩展方法测试
/// </summary>
public class ExtraPropertyDictionaryTest
{
    // ════════════════════════════════════════════════════════════════
    // 构造
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后字典应为空。
    /// </summary>
    [Fact]
    public void DefaultCtor_ShouldBeEmpty()
    {
        // Act
        var dict = new ExtraPropertyDictionary();

        // Assert
        dict.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：用现有字典构造后，所有键值应被复制。
    /// </summary>
    [Fact]
    public void CopyCtor_ShouldCopyAllEntries()
    {
        // Arrange
        var source = new Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = 42
        };

        // Act
        var dict = new ExtraPropertyDictionary(source);

        // Assert
        dict.Count.ShouldBe(2);
        dict["Key1"].ShouldBe("Value1");
        dict["Key2"].ShouldBe(42);
    }

    // ════════════════════════════════════════════════════════════════
    // GetProperty<T>
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：source 为 null 时 GetProperty 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void GetProperty_NullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        ExtraPropertyDictionary dict = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => dict.GetProperty<string>("Key"));
    }

    /// <summary>
    /// 测试目的：键不存在时 GetProperty 应返回该类型默认值。
    /// </summary>
    [Fact]
    public void GetProperty_MissingKey_ShouldReturnDefault()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();

        // Act
        var result = dict.GetProperty<int>("NotExist");

        // Assert
        result.ShouldBe(default(int));
    }

    /// <summary>
    /// 测试目的：键存在时 GetProperty 应返回正确的强类型值。
    /// </summary>
    [Fact]
    public void GetProperty_ExistingKey_ShouldReturnTypedValue()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();
        dict["Name"] = "Alice";

        // Act
        var result = dict.GetProperty<string>("Name");

        // Assert
        result.ShouldBe("Alice");
    }

    /// <summary>
    /// 测试目的：存储 int 值，GetProperty 应转换为 int 正确返回。
    /// </summary>
    [Fact]
    public void GetProperty_IntValue_ShouldConvertToInt()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();
        dict["Age"] = "30";

        // Act
        var result = dict.GetProperty<int>("Age");

        // Assert
        result.ShouldBe(30);
    }

    // ════════════════════════════════════════════════════════════════
    // SetProperty
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：source 为 null 时 SetProperty 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void SetProperty_NullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        ExtraPropertyDictionary dict = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => dict.SetProperty("Key", "Value"));
    }

    /// <summary>
    /// 测试目的：SetProperty 新键应正确添加。
    /// </summary>
    [Fact]
    public void SetProperty_NewKey_ShouldAdd()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();

        // Act
        dict.SetProperty("Role", "Admin");

        // Assert
        dict["Role"].ShouldBe("Admin");
    }

    /// <summary>
    /// 测试目的：SetProperty 已有键应覆盖旧值（不重复）。
    /// </summary>
    [Fact]
    public void SetProperty_ExistingKey_ShouldOverwrite()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();
        dict["Role"] = "User";

        // Act
        dict.SetProperty("Role", "Admin");

        // Assert
        dict["Role"].ShouldBe("Admin");
        dict.Count.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：SetProperty 应返回自身（支持链式调用）。
    /// </summary>
    [Fact]
    public void SetProperty_ShouldReturnSameInstance()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();

        // Act
        var returned = dict.SetProperty("Key", "Value");

        // Assert
        returned.ShouldBeSameAs(dict);
    }

    // ════════════════════════════════════════════════════════════════
    // RemoveProperty
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：source 为 null 时 RemoveProperty 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void RemoveProperty_NullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        ExtraPropertyDictionary dict = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => dict.RemoveProperty("Key"));
    }

    /// <summary>
    /// 测试目的：移除存在的键后，字典中不应再包含该键。
    /// </summary>
    [Fact]
    public void RemoveProperty_ExistingKey_ShouldRemove()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();
        dict["Tag"] = "test";

        // Act
        dict.RemoveProperty("Tag");

        // Assert
        dict.ContainsKey("Tag").ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：移除不存在的键时，不应抛出异常（幂等安全）。
    /// </summary>
    [Fact]
    public void RemoveProperty_MissingKey_ShouldNotThrow()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();

        // Act & Assert
        Should.NotThrow(() => dict.RemoveProperty("NotExist"));
    }

    /// <summary>
    /// 测试目的：RemoveProperty 应返回自身（支持链式调用）。
    /// </summary>
    [Fact]
    public void RemoveProperty_ShouldReturnSameInstance()
    {
        // Arrange
        var dict = new ExtraPropertyDictionary();

        // Act
        var returned = dict.RemoveProperty("Key");

        // Assert
        returned.ShouldBeSameAs(dict);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TransactionActionManager
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// TransactionActionManager 单元测试
/// </summary>
public class TransactionActionManagerTest
{
    // ════════════════════════════════════════════════════════════════
    // Count
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：初始状态 Count 应为 0。
    /// </summary>
    [Fact]
    public void Count_Initial_ShouldBeZero()
    {
        // Act
        var mgr = new TransactionActionManager();

        // Assert
        mgr.Count.ShouldBe(0);
    }

    // ════════════════════════════════════════════════════════════════
    // Register
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Register(null) 应静默忽略，Count 不增加。
    /// </summary>
    [Fact]
    public void Register_NullAction_ShouldIgnore()
    {
        // Arrange
        var mgr = new TransactionActionManager();

        // Act
        mgr.Register(null);

        // Assert
        mgr.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：Register 一个有效操作后 Count 应为 1。
    /// </summary>
    [Fact]
    public void Register_ValidAction_ShouldIncreaseCount()
    {
        // Arrange
        var mgr = new TransactionActionManager();

        // Act
        mgr.Register(_ => Task.CompletedTask);

        // Assert
        mgr.Count.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：Register 多次，Count 应依次累加。
    /// </summary>
    [Fact]
    public void Register_MultipleActions_ShouldAccumulateCount()
    {
        // Arrange
        var mgr = new TransactionActionManager();

        // Act
        mgr.Register(_ => Task.CompletedTask);
        mgr.Register(_ => Task.CompletedTask);
        mgr.Register(_ => Task.CompletedTask);

        // Assert
        mgr.Count.ShouldBe(3);
    }

    // ════════════════════════════════════════════════════════════════
    // CommitAsync
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：CommitAsync 应按注册顺序依次调用所有操作，并将 IDbTransaction 传递给每个操作。
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldInvokeAllActionsInOrder()
    {
        // Arrange
        var mgr = new TransactionActionManager();
        var txn = new Mock<IDbTransaction>().Object;
        var order = new List<int>();

        mgr.Register(_ => { order.Add(1); return Task.CompletedTask; });
        mgr.Register(_ => { order.Add(2); return Task.CompletedTask; });
        mgr.Register(_ => { order.Add(3); return Task.CompletedTask; });

        // Act
        await mgr.CommitAsync(txn);

        // Assert
        order.ShouldBe(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// 测试目的：CommitAsync 执行完毕后，Count 应清零（列表清空）。
    /// </summary>
    [Fact]
    public async Task CommitAsync_AfterCommit_CountShouldBeZero()
    {
        // Arrange
        var mgr = new TransactionActionManager();
        var txn = new Mock<IDbTransaction>().Object;
        mgr.Register(_ => Task.CompletedTask);
        mgr.Register(_ => Task.CompletedTask);

        // Act
        await mgr.CommitAsync(txn);

        // Assert
        mgr.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：CommitAsync 应将正确的 IDbTransaction 实例传递给每个操作。
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldPassCorrectTransactionToActions()
    {
        // Arrange
        var mgr = new TransactionActionManager();
        var txn = new Mock<IDbTransaction>().Object;
        IDbTransaction receivedTxn = null;

        mgr.Register(t => { receivedTxn = t; return Task.CompletedTask; });

        // Act
        await mgr.CommitAsync(txn);

        // Assert
        receivedTxn.ShouldBeSameAs(txn);
    }

    /// <summary>
    /// 测试目的：空管理器 CommitAsync 应直接完成，不抛异常。
    /// </summary>
    [Fact]
    public async Task CommitAsync_EmptyManager_ShouldCompleteWithoutThrowing()
    {
        // Arrange
        var mgr = new TransactionActionManager();
        var txn = new Mock<IDbTransaction>().Object;

        // Act & Assert
        await Should.NotThrowAsync(() => mgr.CommitAsync(txn));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TreeQueryParameter
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// TreeQueryParameter 测试用子类（TreeQueryParameter 构造函数受 protected 保护）
/// </summary>
file class GuidTreeQuery : TreeQueryParameter<Guid?>
{
    public GuidTreeQuery() { }
}

/// <summary>
/// TreeQueryParameter 单元测试
/// </summary>
public class TreeQueryParameterTest
{
    // ════════════════════════════════════════════════════════════════
    // 默认值
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后 Order 应为 "SortId"。
    /// </summary>
    [Fact]
    public void DefaultCtor_Order_ShouldBeSortId()
    {
        // Act
        var query = new GuidTreeQuery();

        // Assert
        query.Order.ShouldBe("SortId");
    }

    /// <summary>
    /// 测试目的：Path 默认值应为空字符串，且 setter/getter 透明。
    /// </summary>
    [Fact]
    public void Path_DefaultValue_ShouldBeEmptyString()
    {
        // Act
        var query = new GuidTreeQuery();

        // Assert
        query.Path.ShouldBe(string.Empty);
    }

    // ════════════════════════════════════════════════════════════════
    // Path getter trim
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Path 设置带前后空格的值时，getter 应自动 Trim。
    /// </summary>
    [Fact]
    public void Path_WithPaddingWhitespace_GetterShouldTrim()
    {
        // Arrange
        var query = new GuidTreeQuery();

        // Act
        query.Path = "  001.002.  ";

        // Assert
        query.Path.ShouldBe("001.002.");
    }

    /// <summary>
    /// 测试目的：Path 设置为 null 时，getter 应返回空字符串（null 安全）。
    /// </summary>
    [Fact]
    public void Path_SetNull_GetterShouldReturnEmpty()
    {
        // Arrange
        var query = new GuidTreeQuery();

        // Act
        query.Path = null;

        // Assert
        query.Path.ShouldBe(string.Empty);
    }

    // ════════════════════════════════════════════════════════════════
    // IsSearch
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：不设置任何搜索条件时，IsSearch 应返回 false。
    /// </summary>
    [Fact]
    public void IsSearch_NoSearchConditions_ShouldReturnFalse()
    {
        // Act
        var query = new GuidTreeQuery();

        // Assert
        query.IsSearch().ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：设置 ParentId 后，IsSearch 应返回 true。
    /// </summary>
    [Fact]
    public void IsSearch_WithParentId_ShouldReturnTrue()
    {
        // Arrange
        var query = new GuidTreeQuery { ParentId = Guid.NewGuid() };

        // Assert
        query.IsSearch().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：设置 Path 后，IsSearch 应返回 true。
    /// </summary>
    [Fact]
    public void IsSearch_WithPath_ShouldReturnTrue()
    {
        // Arrange
        var query = new GuidTreeQuery { Path = "001." };

        // Assert
        query.IsSearch().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：仅设置 Page/PageSize/Order（分页排序字段）时，IsSearch 应仍返回 false。
    /// </summary>
    [Fact]
    public void IsSearch_OnlyPagingFields_ShouldReturnFalse()
    {
        // Arrange
        var query = new GuidTreeQuery { Page = 2, PageSize = 20, Order = "SortId" };

        // Assert
        query.IsSearch().ShouldBeFalse();
    }
}
