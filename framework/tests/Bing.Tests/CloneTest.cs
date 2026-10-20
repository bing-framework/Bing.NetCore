using Bing.Tests.Samples;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests;

/// <summary>
/// 深度克隆扩展测试。
/// </summary>
public class CloneTest : TestBase
{
    /// <summary>
    /// 初始化一个 <see cref="CloneTest"/> 类型的实例。
    /// </summary>
    /// <param name="output">当前测试的输出辅助对象。</param>
    public CloneTest(ITestOutputHelper output) : base(output)
    {
    }

    //[Fact]
    //public void Test_Clone()
    //{
    //    var parent = new ParentSample();
    //    parent.Name = "老铁";
    //    parent.Child = new ChildSample()
    //    {
    //        Name = "老铁Parent",
    //        One = new OneSample()
    //        {
    //            Name = "老铁One",
    //            Age = 10,
    //            Desc = "装逼套路"
    //        }
    //    };
    //    parent.SetName("苦逼了吧老铁");

    //    var clone = parent.Clone();
    //    clone.Name = "老王";
    //    clone.Child.Name = "隔壁老王Parent";
    //    clone.Child.One.Name = "老王One";
    //    clone.Child.One.Age = 11;
    //    clone.Child.One.Desc = "老王装逼套路";
    //    clone.SetName("苦逼了吧老王");

    //    Assert.Equal("老铁", parent.Name);
    //    Assert.Equal("老王", clone.Name);

    //    Assert.Equal("老铁Parent", parent.Child.Name);
    //    Assert.Equal("隔壁老王Parent", clone.Child.Name);

    //    Assert.Equal("苦逼了吧老铁", parent.GetName());
    //    Assert.Equal("苦逼了吧老王", clone.GetName());
    //}
}
