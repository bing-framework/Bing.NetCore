﻿using Bing.Helpers;

namespace Bing.Tests.Samples;

public class A
{
    public string Name { get; set; }
    public B B { get; set; }
}

public class B
{
    public string Name { get; set; }
    public C C { get; set; }
}

public class C
{
    public string Name { get; set; }
    public A A { get; set; }
}

/// <summary>
/// Json测试样例
/// </summary>
public class JsonTestSample
{
    /// <summary>
    /// 名称,测试公共属性，且首字母大写
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 私有属性，应被忽略
    /// </summary>
    private string A { get; set; }
    /// <summary>
    /// 昵称，用来测试小写
    /// </summary>
    public string nickname { get; set; }
    /// <summary>
    /// 测试null
    /// </summary>
    public object Value { get; set; }
    /// <summary>
    /// 日期
    /// </summary>
    public string Date { get; set; }
    /// <summary>
    /// 测试整型，不添加引号
    /// </summary>
    public int Age { get; set; }
    /// <summary>
    /// 测试布尔型
    /// </summary>
    public bool isShow { get; set; }

    /// <summary>
    /// 创建客户
    /// </summary>
    public static JsonTestSample Create()
    {
        return new JsonTestSample()
        {
            Name = "a",
            A = "1",
            nickname = "b",
            Value = null,
            Date = Conv.ToDate("2012-1-1").ToString(),
            Age = 1,
            isShow = true
        };
    }
}