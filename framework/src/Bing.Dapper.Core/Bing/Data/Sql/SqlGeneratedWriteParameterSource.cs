using System.Data;
using System.Data.Common;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 将写入命令的结构化参数交给源生成绑定器，绕过通用参数反射路径。
/// </summary>
internal sealed class SqlGeneratedWriteParameterSource : ISqlStaticParameterSource
{
    private readonly IEnumerable<SqlParam> _values;
    private readonly Action<DbCommand, IEnumerable<SqlParam>> _bind;
    private readonly IReadOnlyCollection<ISqlDbParameterCustomizer> _customizers;

    /// <summary>
    /// 初始化一个 <see cref="SqlGeneratedWriteParameterSource"/> 类型的实例。
    /// </summary>
    /// <param name="values">本次执行的参数快照。</param>
    /// <param name="bind">源生成参数绑定器。</param>
    /// <param name="customizers">当前执行上下文登记的数据库参数定制器。</param>
    public SqlGeneratedWriteParameterSource(IEnumerable<SqlParam> values,
        Action<DbCommand, IEnumerable<SqlParam>> bind,
        IReadOnlyCollection<ISqlDbParameterCustomizer> customizers = null)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
        _bind = bind ?? throw new ArgumentNullException(nameof(bind));
        _customizers = customizers ?? Array.Empty<ISqlDbParameterCustomizer>();
    }

    /// <inheritdoc />
    public void BindTo(IDbCommand command)
    {
        _bind((DbCommand)command, _values);
        if (command == null || _customizers.Count == 0)
            return;
        SqlGeneratedParameterCustomization.Apply(command, _values, _customizers);
    }
}
