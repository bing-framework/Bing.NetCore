using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 空JSON结果
/// </summary>
public class NullJsonResult : JsonResult
{
    /// <summary>
    /// 初始化一个<see cref="NullJsonResult"/>类型的实例
    /// </summary>
    public NullJsonResult() : base(null)
    {
    }
}