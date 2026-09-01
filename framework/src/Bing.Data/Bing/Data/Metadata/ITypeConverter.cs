using System.Data;

namespace Bing.Data.Metadata;

/// <summary>
/// 将数据类型转换为数据库类型。
/// </summary>
public interface ITypeConverter
{
    /// <summary>
    /// 将数据类型转换为 <see cref="DbType"/>。
    /// </summary>
    /// <param name="dataType">要转换的数据类型名称。</param>
    /// <param name="length">数据长度；无需长度时传入 <see langword="null"/>。</param>
    /// <returns>转换后的数据库类型；不支持该数据类型时返回 <see langword="null"/>。</returns>
    DbType? ToDbType(string dataType, int? length = null);
}
