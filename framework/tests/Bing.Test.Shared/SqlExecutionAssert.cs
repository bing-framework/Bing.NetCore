using Bing.Data.Sql.Builders.Params;

namespace Bing.Test.Shared;

/// <summary>
/// SQL 执行观测断言。
/// </summary>
public static class SqlExecutionAssert
{
    /// <summary>
    /// 断言已观测到的命令 SQL 和参数。
    /// </summary>
    /// <param name="observer">命令观察器。</param>
    /// <param name="expectedSql">测试方法中声明的目标 SQL。</param>
    /// <param name="provider">当前 Provider 标识。</param>
    public static void Command(TestCommandObserver observer, string expectedSql, string provider = null)
    {
        if (observer == null)
            throw new ArgumentNullException(nameof(observer));
        SqlAssert.Equal(expectedSql, observer.CommandText, provider, observer.Parameters);
    }
}