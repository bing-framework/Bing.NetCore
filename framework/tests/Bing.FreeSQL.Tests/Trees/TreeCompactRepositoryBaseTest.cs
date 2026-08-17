using System.Linq.Expressions;
using Bing.Data;
using Bing.Domain.Entities;
using Bing.Domain.Repositories;
using Bing.Trees;
using Moq;
using Xunit;

namespace Bing.FreeSQL.Tests.Trees;

/// <summary>
/// FreeSQL 紧凑树仓储测试。
/// </summary>
public sealed class TreeCompactRepositoryBaseTest
{
    /// <summary>
    /// 测试目的：紧凑树仓储必须排除父节点，并返回当前路径下的全部后代节点。
    /// </summary>
    [Fact]
    public async Task GetAllChildrenAsync_WhenSubtreeContainsParentAndDescendants_ShouldExcludeParentAndReturnDescendants()
    {
        // Arrange
        var parent = new TestTreeEntity(1, "1,", 1);
        var store = CreateStore(new[]
        {
            new TestTreePo { Id = 1, Path = "1," },
            new TestTreePo { Id = 2, Path = "1,2," },
            new TestTreePo { Id = 3, Path = "1,2,3," },
            new TestTreePo { Id = 4, Path = "4," }
        });
        var repository = new TestTreeRepository(store.Object);

        // Act
        var children = await repository.GetAllChildrenAsync(parent);

        // Assert
        Assert.Equal(new[] { 2, 3 }, children.Select(item => item.Id));
    }

    /// <summary>
    /// 测试目的：预取消的单实体写入不得调用可覆写的持久化映射。
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenCancellationRequested_ShouldNotInvokePersistenceMapping()
    {
        // Arrange
        var store = CreateStore(Array.Empty<TestTreePo>());
        var repository = new TestTreeRepository(store.Object);
        var entity = new TestTreeEntity(1, "1,", 1);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.AddAsync(entity,
            cancellationTokenSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.RemoveAsync(entity,
            cancellationTokenSource.Token));
        Assert.Equal(0, repository.ToPoCallCount);
    }

    /// <summary>
    /// 测试目的：预取消的紧凑仓储入口必须在调用可替换 Store 或任意实体映射前终止。
    /// </summary>
    [Fact]
    public async Task AsyncOperations_WhenCancellationRequested_ShouldNotUseStoreOrMapping()
    {
        // Arrange
        var store = CreateStore(Array.Empty<TestTreePo>());
        var repository = new TestTreeRepository(store.Object);
        var entity = new TestTreeEntity(1, "1,", 1);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var token = cancellationTokenSource.Token;

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.FindAsync(1, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.FindByIdsAsync(
            (IEnumerable<int>)new[] { 1 }, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.AddAsync(
            (IEnumerable<TestTreeEntity>)new[] { entity }, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.RemoveAsync((object)1, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.RemoveAsync(
            (IEnumerable<int>)new[] { 1 }, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.RemoveAsync(
            (IEnumerable<TestTreeEntity>)new[] { entity }, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.FindByIdNoTrackingAsync(1, token));

        // Assert
        Assert.Equal(0, repository.ToEntityCallCount);
        Assert.Equal(0, repository.ToPoCallCount);
        store.Verify(item => item.FindByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.FindByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.AddAsync(It.IsAny<IEnumerable<TestTreePo>>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.RemoveAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.RemoveAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.RemoveAsync(It.IsAny<IEnumerable<TestTreePo>>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.FindByIdNoTrackingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 创建按表达式筛选内存持久化对象的存储器。
    /// </summary>
    /// <param name="items">持久化对象集合。</param>
    /// <returns>存储器 Mock。</returns>
    private static Mock<IStore<TestTreePo, int>> CreateStore(IEnumerable<TestTreePo> items)
    {
        var store = new Mock<IStore<TestTreePo, int>>();
        store.Setup(item => item.FindAllAsync(It.IsAny<Expression<Func<TestTreePo, bool>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<TestTreePo, bool>> predicate, CancellationToken _) =>
                Task.FromResult(items.Where(predicate.Compile()).ToList()));
        return store;
    }

    /// <summary>
    /// 紧凑树仓储测试实现。
    /// </summary>
    private sealed class TestTreeRepository : TreeCompactRepositoryBase<TestTreeEntity, TestTreePo, int, int?>
    {
        /// <summary>
        /// 持久化映射调用次数。
        /// </summary>
        public int ToPoCallCount { get; private set; }

        /// <summary>
        /// 实体映射调用次数。
        /// </summary>
        public int ToEntityCallCount { get; private set; }

        /// <summary>
        /// 初始化测试仓储。
        /// </summary>
        /// <param name="store">持久化对象存储器。</param>
        public TestTreeRepository(IStore<TestTreePo, int> store) : base(store)
        {
        }

        /// <inheritdoc />
        public override Task<int> GenerateSortIdAsync(int? parentId) => Task.FromResult(0);

        /// <inheritdoc />
        protected override TestTreeEntity ToEntity(TestTreePo po)
        {
            ToEntityCallCount++;
            return new TestTreeEntity(po.Id, po.Path, po.Level);
        }

        /// <inheritdoc />
        protected override TestTreePo ToPo(TestTreeEntity entity)
        {
            ToPoCallCount++;
            return new TestTreePo
            {
                Id = entity.Id,
                Path = entity.Path,
                Level = entity.Level
            };
        }
    }

    /// <summary>
    /// 测试领域树节点。
    /// </summary>
    private sealed class TestTreeEntity : TreeEntityBase<TestTreeEntity, int, int?>
    {
        /// <summary>
        /// 初始化测试树节点。
        /// </summary>
        /// <param name="id">节点标识。</param>
        /// <param name="path">物化路径。</param>
        /// <param name="level">节点层级。</param>
        public TestTreeEntity(int id, string path, int level) : base(id, path, level)
        {
        }
    }

    /// <summary>
    /// 测试持久化树节点。
    /// </summary>
    public sealed class TestTreePo : IKey<int>, IVersion, IPath
    {
        /// <summary>
        /// 节点标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 乐观锁版本。
        /// </summary>
        public byte[] Version { get; set; }

        /// <summary>
        /// 物化路径。
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 节点层级。
        /// </summary>
        public int Level { get; set; }
    }
}