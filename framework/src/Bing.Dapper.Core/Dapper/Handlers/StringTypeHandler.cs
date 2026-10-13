namespace Dapper.Handlers;

/// <summary>
/// 字符串类型处理器
/// </summary>
public class StringTypeHandler : SqlMapper.TypeHandler<string>
{
    /// <inheritdoc />
    public override void SetValue(System.Data.IDbDataParameter parameter, string value)
    {
        if (parameter == null)
            return;
        parameter.Value = value;
    }

    /// <inheritdoc />
    public override string Parse(object value) => value?.ToString();
}
