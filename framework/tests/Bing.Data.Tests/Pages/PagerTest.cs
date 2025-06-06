﻿using Xunit;

namespace Bing.Data.Tests.Pages;

/// <summary>
/// 分页参数测试
/// </summary>
public class PagerTest
{
    /// <summary>
    /// 分页参数
    /// </summary>
    private readonly Pager _pager;

    /// <summary>
    /// 初始化一个<see cref="PagerTest"/>类型的实例
    /// </summary>
    public PagerTest() => _pager = new Pager();

    /// <summary>
    /// 测试 - 分页默认值
    /// </summary>
    [Fact]
    public void Test_Default()
    {
        Assert.Equal(1, _pager.Page);
        Assert.Equal(20, _pager.PageSize);
        Assert.Equal(0, _pager.TotalCount);
        Assert.Equal(0, _pager.GetPageCount());
        Assert.Equal(0, _pager.GetSkipCount());
        Assert.Equal("", _pager.Order);
        Assert.Equal(1, _pager.GetStartNumber());
        Assert.Equal(20, _pager.GetEndNumber());
    }

    /// <summary>
    /// 测试 - 总页数
    /// </summary>
    [Fact]
    public void Test_PageCount()
    {
        _pager.TotalCount = 0;
        Assert.Equal(0, _pager.GetPageCount());

        _pager.TotalCount = 100;
        Assert.Equal(5, _pager.GetPageCount());

        _pager.TotalCount = 1;
        Assert.Equal(1, _pager.GetPageCount());

        _pager.PageSize = 10;
        _pager.TotalCount = 100;
        Assert.Equal(10, _pager.GetPageCount());
    }

    /// <summary>
    /// 测试 - 页索引 - 小于1，则修正为1
    /// </summary>
    [Fact]
    public void Test_Page()
    {
        _pager.Page = 0;
        Assert.Equal(1, _pager.Page);

        _pager.Page = -1;
        Assert.Equal(1, _pager.Page);
    }

    /// <summary>
    /// 测试 - 跳过的行数
    /// </summary>
    [Fact]
    public void Test_SkipCount()
    {
        _pager.TotalCount = 100;

        _pager.Page = 0;
        Assert.Equal(0, _pager.GetSkipCount());

        _pager.Page = 1;
        Assert.Equal(0, _pager.GetSkipCount());

        _pager.Page = 2;
        Assert.Equal(20, _pager.GetSkipCount());

        _pager.Page = 3;
        Assert.Equal(40, _pager.GetSkipCount());

        _pager.Page = 4;
        Assert.Equal(60, _pager.GetSkipCount());

        _pager.Page = 5;
        Assert.Equal(80, _pager.GetSkipCount());

        _pager.Page = 6;
        Assert.Equal(100, _pager.GetSkipCount());

        _pager.TotalCount = 99;

        _pager.Page = 0;
        Assert.Equal(0, _pager.GetSkipCount());

        _pager.Page = 1;
        Assert.Equal(0, _pager.GetSkipCount());

        _pager.Page = 2;
        Assert.Equal(20, _pager.GetSkipCount());

        _pager.Page = 3;
        Assert.Equal(40, _pager.GetSkipCount());

        _pager.Page = 4;
        Assert.Equal(60, _pager.GetSkipCount());

        _pager.Page = 5;
        Assert.Equal(80, _pager.GetSkipCount());

        _pager.Page = 6;
        Assert.Equal(100, _pager.GetSkipCount());

        _pager.TotalCount = 0;
        _pager.Page = 1;
        Assert.Equal(0, _pager.GetSkipCount());
    }

    /// <summary>
    /// 测试 - 起始和结束行数
    /// </summary>
    [Fact]
    public void Test_GetStartNumber()
    {
        _pager.Page = 2;
        _pager.PageSize = 10;
        Assert.Equal(11, _pager.GetStartNumber());
        Assert.Equal(20, _pager.GetEndNumber());
    }
}
