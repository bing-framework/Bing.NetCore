using System.Collections.Generic;

namespace Bing.Datas.Sql.Builders
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    public interface IParameterManager
    {
        /// <summary>
        /// 创建参数名
        /// </summary>
        string GenerateName();

        /// <summary>
        /// 获取参数列表
        /// </summary>
        IReadOnlyDictionary<string, object> GetParams();

        /// <summary>
        /// 添加参数，如果参数已存在则替换
        /// </summary>
        /// <param name="name">参数名</param>
        /// <param name="value">参数值</param>
        /// <param name="operator">运算符</param>
        void Add(string name, object value, Operator? @operator = null);

        /// <summary>
        /// 克隆
        /// </summary>
        IParameterManager Clone();

        /// <summary>
        /// 清空参数
        /// </summary>
        void Clear();
    }
}
