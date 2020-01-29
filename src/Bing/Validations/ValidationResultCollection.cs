using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bing.Validations
{
    ///// <summary>
    ///// 验证结果集合
    ///// </summary>
    //public class ValidationResultCollection : IEnumerable<ValidationResult>
    //{
    //    #region 字段

    //    /// <summary>
    //    /// 验证结果
    //    /// </summary>
    //    private readonly List<ValidationResult> _results;

    //    #endregion

    //    #region 属性

    //    /// <summary>
    //    /// 是否有效
    //    /// </summary>
    //    public bool IsValid => _results.Count == 0;

    //    /// <summary>
    //    /// 验证结果个数
    //    /// </summary>
    //    public int Count => _results.Count;

    //    /// <summary>
    //    /// 成功验证结果集合
    //    /// </summary>
        

    //    #endregion

    //    #region 构造函数

    //    /// <summary>
    //    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    //    /// </summary>
    //    public ValidationResultCollection() : this("")
    //    {
    //    }

    //    /// <summary>
    //    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    //    /// </summary>
    //    /// <param name="result">验证结果</param>
    //    public ValidationResultCollection(string result)
    //    {
    //        _results = new List<ValidationResult>();
    //        if (string.IsNullOrWhiteSpace(result))
    //            return;
    //        _results.Add(new ValidationResult(result));
    //    }

    //    #endregion

    //    /// <summary>
    //    /// 添加验证结果
    //    /// </summary>
    //    /// <param name="result">验证结果</param>
    //    public void Add(ValidationResult result)
    //    {
    //        if (result == null)
    //            return;
    //        _results.Add(result);
    //    }

    //    /// <summary>
    //    /// 添加验证结果集合
    //    /// </summary>
    //    /// <param name="results">验证结果集合</param>
    //    public void AddList(IEnumerable<ValidationResult> results)
    //    {
    //        if (results == null)
    //            return;
    //        foreach (var result in results)
    //            Add(result);
    //    }

    //    /// <summary>
    //    /// 获取迭代器
    //    /// </summary>
    //    IEnumerator<ValidationResult> IEnumerable<ValidationResult>.GetEnumerator() => _results.GetEnumerator();

    //    /// <summary>
    //    /// 获取迭代器
    //    /// </summary>
    //    IEnumerator IEnumerable.GetEnumerator() => _results.GetEnumerator();
    //}
}
