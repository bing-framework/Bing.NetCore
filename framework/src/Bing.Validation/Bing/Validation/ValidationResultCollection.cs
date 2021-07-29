using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Bing.Validation
{
    /// <summary>
    /// 验证结果集合
    /// </summary>
    public class ValidationResultCollection : List<ValidationResult>
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => Count == 0;

        /// <summary>
        /// 成功验证结果集合
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public static readonly ValidationResultCollection Success = new ValidationResultCollection();

        /// <summary>
        /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
        /// </summary>
        public ValidationResultCollection() : this("")
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
        /// </summary>
        /// <param name="result">验证结果</param>
        public ValidationResultCollection(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
                return;
            Add(new ValidationResult(result));
        }

        /// <summary>
        /// 添加验证结果集合
        /// </summary>
        /// <param name="results">验证结果集合</param>
        public void AddList(IEnumerable<ValidationResult> results)
        {
            if (results == null)
                return;
            foreach (var result in results)
                Add(result);
        }

        /// <summary>
        /// 输出验证消息
        /// </summary>
        public override string ToString()
        {
            if (IsValid)
                return string.Empty;
            return this.First().ErrorMessage;
        }
    }
}
