﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Properties;
using Bing.Tests.Samples;
using Bing.Tests.XUnitHelpers;
using Bing.Trees;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bing.Tests.Domains.Trees;

/// <summary>
/// 树型更新路径服务测试
/// </summary>
public class UpdatePathManagerTest
{
    /// <summary>
    /// 树型服务
    /// </summary>
    private readonly UpdatePathManager<Role, Guid, Guid?> _manager;

    /// <summary>
    /// 角色仓储
    /// </summary>
    private readonly IRoleRepository _mockRepository;

    /// <summary>
    /// 编号
    /// </summary>
    private readonly Guid _id = "10000001-AAF2-4D03-9310-FEF2F47B9FE2".ToGuid();

    /// <summary>
    /// 编号2
    /// </summary>
    private readonly Guid _id2 = "10000002-AAF2-4D03-9310-FEF2F47B9FE2".ToGuid();

    /// <summary>
    /// 编号3
    /// </summary>
    private readonly Guid _id3 = "10000003-AAF2-4D03-9310-FEF2F47B9FE2".ToGuid();

    /// <summary>
    /// 编号4
    /// </summary>
    private readonly Guid _id4 = "10000004-AAF2-4D03-9310-FEF2F47B9FE2".ToGuid();

    /// <summary>
    /// 编号5
    /// </summary>
    private readonly Guid _id5 = "10000005-AAF2-4D03-9310-FEF2F47B9FE2".ToGuid();

    /// <summary>
    /// 初始化一个<see cref="UpdatePathManagerTest"/>类型的实例
    /// </summary>
    public UpdatePathManagerTest()
    {
        _mockRepository = Substitute.For<IRoleRepository>();
        _manager = new UpdatePathManager<Role, Guid, Guid?>(_mockRepository);
    }

    /// <summary>
    /// 测试 - 修改父节点 - 数据库中未找到 - Path无变化
    /// </summary>
    [Fact]
    public async Task Test_UpdatePathAsync_1()
    {
        //设置
        string path = $"{_id},";
        _mockRepository.FindByIdAsync(_id).ReturnsNull();

        //执行
        var role = new Role(_id, path, 0) { ParentId = _id2 };
        await _manager.UpdatePathAsync(role);

        //验证
        await _mockRepository.DidNotReceive().GetAllChildrenAsync(role);
        Assert.Equal(path, role.Path);
    }

    /// <summary>
    /// 测试 - 修改父节点 - 父节点未修改 - Path无变化
    /// </summary>
    [Fact]
    public async Task Test_UpdatePathAsync_2()
    {
        //设置
        string path = $"{_id2},{_id},";
        _mockRepository.FindByIdAsync(_id).Returns(new Role(_id, path, 2) { ParentId = _id2 });

        //执行
        var role = new Role(_id, path, 2) { ParentId = _id2 };
        await _manager.UpdatePathAsync(role);

        //验证
        await _mockRepository.DidNotReceive().GetAllChildrenAsync(role);
        Assert.Equal(path, role.Path);
    }

    /// <summary>
    /// 测试 - 修改父节点 - 父节点已修改 - 无下级子节点
    /// </summary>
    [Fact]
    public async Task Test_UpdatePathAsync_3()
    {
        //设置
        var path = $"{_id2},{_id},";
        var old = new Role(_id, path, 2) { ParentId = _id2 };
        _mockRepository.FindByIdNoTrackingAsync(_id).Returns(old);
        _mockRepository.FindByIdAsync(_id3).Returns(new Role(_id3, $"{_id3},", 1));
        var list = new List<Role> { };
        _mockRepository.GetAllChildrenAsync(old).Returns(list);

        //执行
        var role = new Role(_id, path, 2) { ParentId = _id3 };
        await _manager.UpdatePathAsync(role);

        //验证
        Assert.Equal($"{_id3},{_id},", role.Path);
        Assert.Equal(2, role.Level);
    }

    /// <summary>
    /// 测试 - 修改父节点 - 父节点已修改 - 下级1个子节点
    /// </summary>
    [Fact]
    public async Task Test_UpdatePathAsync_4()
    {
        //设置
        var path = $"{_id2},{_id},";
        var old = new Role(_id, path, 2) { ParentId = _id2 };
        var child = new Role(_id4, $"{path},{_id4}", 3) { ParentId = _id };
        _mockRepository.FindByIdNoTrackingAsync(_id).Returns(old);
        _mockRepository.FindByIdAsync(_id3).Returns(new Role(_id3, $"{_id3},", 1));
        var list = new List<Role> { child };
        _mockRepository.GetAllChildrenAsync(old).Returns(list);

        //执行
        var role = new Role(_id, path, 2) { ParentId = _id3 };
        await _manager.UpdatePathAsync(role);

        //验证
        Assert.Equal($"{_id3},{_id},", role.Path);
        Assert.Equal($"{_id3},{_id},{_id4},", list[0].Path);
    }

    /// <summary>
    /// 测试 - 修改父节点 - 父节点已修改 - 直接下级2个子节点
    /// </summary>
    [Fact]
    public async Task Test_UpdatePathAsync_5()
    {
        //设置
        var path = $"{_id2},{_id},";
        var old = new Role(_id, path, 2) { ParentId = _id2 };
        var child1 = new Role(_id4, $"{path},{_id4}", 3) { ParentId = _id };
        var child2 = new Role(_id5, $"{path},{_id5}", 3) { ParentId = _id };
        _mockRepository.FindByIdNoTrackingAsync(_id).Returns(old);
        _mockRepository.FindByIdAsync(_id3).Returns(new Role(_id3, $"{_id3},", 1));
        var list = new List<Role> { child1, child2 };
        _mockRepository.GetAllChildrenAsync(old).Returns(list);

        //执行
        var role = new Role(_id, path, 2) { ParentId = _id3 };
        await _manager.UpdatePathAsync(role);

        //验证
        Assert.Equal($"{_id3},{_id},", role.Path);
        Assert.Equal($"{_id3},{_id},{_id4},", list[0].Path);
        Assert.Equal($"{_id3},{_id},{_id5},", list[1].Path);
    }

    /// <summary>
    /// 测试 - 修改父节点 - 父节点已修改 - 下级2层子节点
    /// </summary>
    [Fact]
    public async Task Test_UpdatePathAsync_6()
    {
        //设置
        var path = $"{_id2},{_id},";
        var old = new Role(_id, path, 2) { ParentId = _id2 };
        var child1 = new Role(_id4, $"{path},{_id4}", 3) { ParentId = _id };
        var child2 = new Role(_id5, $"{path},{_id4},{_id5}", 4) { ParentId = _id4 };
        _mockRepository.FindByIdNoTrackingAsync(_id).Returns(old);
        _mockRepository.FindByIdAsync(_id3).Returns(new Role(_id3, $"{_id3},", 1));
        var list = new List<Role> { child1, child2 };
        _mockRepository.GetAllChildrenAsync(old).Returns(list);

        //执行
        Role role = new Role(_id, path, 2) { ParentId = _id3 };
        await _manager.UpdatePathAsync(role);

        //验证
        Assert.Equal($"{_id3},{_id},", role.Path);
        Assert.Equal($"{_id3},{_id},{_id4},", list[0].Path);
        Assert.Equal($"{_id3},{_id},{_id4},{_id5},", list[1].Path);
    }

    /// <summary>
    /// 测试 - 修改父节点 - 将父节点移动到子节点下
    /// </summary>
    [Fact]
    public async Task Test_UpdatePathAsync_7()
    {
        //设置
        string path = $"{_id},";
        var old = new Role(_id, path, 1);
        var child = new Role(_id2, $"{_id},{_id2},", 2) { ParentId = _id };
        _mockRepository.FindByIdNoTrackingAsync(_id).Returns(old);
        var list = new List<Role> { child };
        _mockRepository.GetAllChildrenAsync(old).Returns(list);

        //执行验证
        await AssertHelper.ThrowsAsync<Warning>(async () =>
        {
            var result = new Role(_id, path, 1) { ParentId = _id2 };
            await _manager.UpdatePathAsync(result);
        }, LibraryResource.NotSupportMoveToChildren);
    }

    /// <summary>
    /// 测试 - 修改父节点 - 将父节点设置为自己
    /// </summary>
    [Fact]
    public async Task Test_UpdatePathAsync_8()
    {
        //设置
        string path = $"{_id},";
        var old = new Role(_id, path, 1);
        var child = new Role(_id2, $"{_id},{_id2},", 2) { ParentId = _id };
        _mockRepository.FindByIdNoTrackingAsync(_id).Returns(old);
        var list = new List<Role> { child };
        _mockRepository.GetAllChildrenAsync(old).Returns(list);

        //执行验证
        await AssertHelper.ThrowsAsync<Warning>(async () =>
        {
            var result = new Role(_id, path, 1) { ParentId = _id };
            await _manager.UpdatePathAsync(result);
        }, LibraryResource.NotSupportMoveToChildren);
    }
}
