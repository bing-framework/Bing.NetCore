namespace Bing.Data.Sql;

// SqlQueryBase - 钩子方法
public partial class SqlQueryBase
{
    /// <summary>
    /// 执行前操作，返回false停止执行
    /// </summary>
    protected virtual bool ExecuteBefore() => true;

    /// <summary>
    /// 执行后操作
    /// </summary>
    /// <param name="result">结果</param>
    protected virtual void ExecuteAfter(object result)
    {
        Clear();
    }
}
