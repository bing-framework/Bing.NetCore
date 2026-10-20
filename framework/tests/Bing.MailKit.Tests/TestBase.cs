using Xunit.Abstractions;

namespace Bing.MailKit.Tests;

/// <summary>
/// 提供邮件测试的公共基类。
/// </summary>
public class TestBase
{
    /// <summary>
    /// 保存测试输出写入器。
    /// </summary>
    protected ITestOutputHelper Output;

    /// <summary>
    /// 初始化一个 <see cref="TestBase"/> 类型的实例。
    /// </summary>
    /// <param name="output">测试输出写入器。</param>
    public TestBase(ITestOutputHelper output)
    {
        Output = output;
    }
}
