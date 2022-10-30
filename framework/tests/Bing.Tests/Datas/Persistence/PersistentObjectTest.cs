﻿using System;
using Bing.Tests.Samples;
using Xunit;

namespace Bing.Tests.Datas.Persistence;

/// <summary>
/// 持久化对象测试
/// </summary>
public class PersistentObjectTest
{
    /// <summary>
    /// 持久化对象测试样例
    /// </summary>
    private PersistentObjectSample _sample;
    /// <summary>
    /// 持久化对象测试样例2
    /// </summary>
    private PersistentObjectSample _sample2;

    /// <summary>
    /// 初始化一个<see cref="PersistentObjectTest"/>类型的实例
    /// </summary>
    public PersistentObjectTest()
    {
        _sample = new PersistentObjectSample();
        _sample2 = new PersistentObjectSample();
    }

    /// <summary>
    /// 测试 - 实体相等性 - 判空
    /// </summary>
    [Fact]
    public void Test_Equals_Null()
    {
        Assert.False(_sample.Equals(_sample2));
        Assert.False(_sample.Equals(null));

        Assert.False(_sample == _sample2);
        Assert.False(_sample == null);
        Assert.False(null == _sample2);

        Assert.True(_sample != _sample2);
        Assert.True(_sample != null);
        Assert.True(null != _sample2);

        _sample2 = null;
        Assert.False(_sample.Equals(_sample2));

        _sample = null;
        Assert.True(_sample == _sample2);
        Assert.True(_sample2 == _sample);
    }

    /// <summary>
    /// 测试 - 实体相等性 - 类型无效
    /// </summary>
    [Fact]
    public void Test_Equals_InvalidType()
    {
        var id = Guid.NewGuid();
        _sample = new PersistentObjectSample(id);
        PersistentObjectSample2 sample2 = new PersistentObjectSample2(id);
        Assert.False(_sample.Equals(sample2));
        Assert.True(_sample != sample2);
        Assert.True(sample2 != _sample);
    }

    /// <summary>
    /// 测试 - 实体相等性 - 当两个实体的标识相同，则实体相同
    /// </summary>
    [Fact]
    public void Test_Equals_IdEquals()
    {
        var id = Guid.NewGuid();
        _sample = new PersistentObjectSample(id);
        _sample2 = new PersistentObjectSample(id);
        Assert.True(_sample.Equals(_sample2));
        Assert.True(_sample == _sample2);
        Assert.False(_sample != _sample2);
    }
}