using System.Data;

namespace Bing.Data.Metadata;

/// <summary>
/// Oracle数据类型转换器
/// </summary>
public class OracleTypeConverter : ITypeConverter
{
    /// <inheritdoc />
    public DbType? ToDbType(string dataType, int? length = null)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return null;
        throw new System.NotImplementedException();
    }
}
