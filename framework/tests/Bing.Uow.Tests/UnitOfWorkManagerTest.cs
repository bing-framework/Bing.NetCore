using Bing.Uow;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// <see cref="UnitOfWorkManager"/> 单元测试。
/// Mock IUnitOfWork，验证管理器协调行为，不依赖真实 DB。
/// </summary>
public class UnitOfWorkManagerTest
{
    private static Mock<IUnitOfWork> CreateMockUow(int commitResult = 1)
    {
        var mock = new Mock<IUnitOfWork>();
        mock.Setup(u => u.Commit()).Returns(commitResult);
        mock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(commitResult);
        return mock;
    }

    // ── Register ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Register 后 GetUnitOfWorks 应包含已注册的工作单元。
    /// </summary>
    [Fact]
    public void Register_ShouldAddUnitOfWorkToCollection()
    {
        // Arrange
        var manager = new UnitOfWorkManager();
        var uow = CreateMockUow().Object;

        // Act
        manager.Register(uow);

        // Assert
        manager.GetUnitOfWorks().ShouldContain(uow);
    }

    /// <summary>
    /// 测试目的：同一个工作单元重复注册，集合中只应存在一次（HashSet 去重）。
    /// </summary>
    [Fact]
    public void Register_SameUowTwice_ShouldNotDuplicate()
    {
        // Arrange
        var manager = new UnitOfWorkManager();
        var uow = CreateMockUow().Object;

        // Act
        manager.Register(uow);
        manager.Register(uow);

        // Assert
        manager.GetUnitOfWorks().Count.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：注册 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Register_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var manager = new UnitOfWorkManager();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => manager.Register(null));
    }

    // ── Commit ────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Commit 应调用所有已注册工作单元的 Commit 方法，每个仅调用一次。
    /// </summary>
    [Fact]
    public void Commit_ShouldCallCommitOnAllRegisteredUows()
    {
        // Arrange
        var manager = new UnitOfWorkManager();
        var mock1 = CreateMockUow();
        var mock2 = CreateMockUow();
        manager.Register(mock1.Object);
        manager.Register(mock2.Object);

        // Act
        manager.Commit();

        // Assert
        mock1.Verify(u => u.Commit(), Times.Once);
        mock2.Verify(u => u.Commit(), Times.Once);
    }

    /// <summary>
    /// 测试目的：无注册工作单元时，Commit 不抛异常（零元素场景）。
    /// </summary>
    [Fact]
    public void Commit_WithNoUows_ShouldNotThrow()
    {
        // Arrange
        var manager = new UnitOfWorkManager();

        // Act & Assert
        Should.NotThrow(() => manager.Commit());
    }

    // ── CommitAsync ───────────────────────────────────────────────

    /// <summary>
    /// 测试目的：CommitAsync 应异步调用所有已注册工作单元的 CommitAsync，每个仅调用一次。
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldCallCommitAsyncOnAllRegisteredUows()
    {
        // Arrange
        var manager = new UnitOfWorkManager();
        var mock1 = CreateMockUow();
        var mock2 = CreateMockUow();
        manager.Register(mock1.Object);
        manager.Register(mock2.Object);

        // Act
        await manager.CommitAsync();

        // Assert
        mock1.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        mock2.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 测试目的：CommitAsync 在无注册工作单元时应正常完成，不抛异常。
    /// </summary>
    [Fact]
    public async Task CommitAsync_WithNoUows_ShouldCompleteSuccessfully()
    {
        // Arrange
        var manager = new UnitOfWorkManager();

        // Act & Assert
        await Should.NotThrowAsync(async () => await manager.CommitAsync());
    }

    /// <summary>
    /// 测试目的：传入 CancellationToken 时应将 Token 正确传递给各工作单元。
    /// </summary>
    [Fact]
    public async Task CommitAsync_WithCancellationToken_ShouldPassTokenToUows()
    {
        // Arrange
        var manager = new UnitOfWorkManager();
        var mock = CreateMockUow();
        manager.Register(mock.Object);
        var cts = new CancellationTokenSource();

        // Act
        await manager.CommitAsync(cts.Token);

        // Assert
        mock.Verify(u => u.CommitAsync(cts.Token), Times.Once);
    }

    // ── GetUnitOfWorks ────────────────────────────────────────────

    /// <summary>
    /// 测试目的：初始状态下 GetUnitOfWorks 应返回空集合，不为 null。
    /// </summary>
    [Fact]
    public void GetUnitOfWorks_InitialState_ShouldBeEmptyNotNull()
    {
        // Arrange
        var manager = new UnitOfWorkManager();

        // Act
        var uows = manager.GetUnitOfWorks();

        // Assert
        uows.ShouldNotBeNull();
        uows.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：GetUnitOfWorks 返回的集合应为只读，防止调用方直接修改内部状态。
    /// </summary>
    [Fact]
    public void GetUnitOfWorks_ShouldReturnReadOnlyCollection()
    {
        // Arrange
        var manager = new UnitOfWorkManager();
        manager.Register(CreateMockUow().Object);

        // Act
        var uows = manager.GetUnitOfWorks();

        // Assert — IReadOnlyCollection<T> 不能 Cast 为 IList<T>
        (uows is IReadOnlyCollection<IUnitOfWork>).ShouldBeTrue();
    }
}
