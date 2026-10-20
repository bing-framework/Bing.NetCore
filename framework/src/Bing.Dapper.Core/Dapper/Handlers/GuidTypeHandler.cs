namespace Dapper.Handlers;

/// <summary>
/// Guid类型处理器
/// </summary>
public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    /// <inheritdoc />
    public override void SetValue(System.Data.IDbDataParameter parameter, Guid value)
    {
        if (parameter == null || value == Guid.Empty)
            return;
        var values = value.ToByteArray();
        byte[] result =
        {
            values[3], values[2], values[1], values[0], values[5], values[4], values[7], values[6], values[8],
            values[9], values[10], values[11], values[12], values[13], values[14], values[15]
        };
        parameter.Value = result;
    }

    /// <inheritdoc />
    public override Guid Parse(object value)
    {
        if (value == null)
            return Guid.Empty;
        var values = (byte[])value;
        byte[] result =
        {
            values[3], values[2], values[1], values[0], values[5], values[4], values[7], values[6], values[8],
            values[9], values[10], values[11], values[12], values[13], values[14], values[15]
        };
        return new Guid(result);
    }
}
