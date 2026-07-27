using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 数据库提供程序。
/// </summary>
/// <remarks>
/// 提供程序及其公开属性必须不可变并可在线程间安全共享。
/// </remarks>
public interface ISqlProvider
{
    /// <summary>
    /// Provider 唯一标识。
    /// </summary>
    /// <remarks>
    /// 标识在注册时忽略大小写并移除首尾空白；<see cref="DatabaseType"/> 仅用于官方 Provider 的兼容查找与路由。
    /// </remarks>
    string Key { get; }

    /// <summary>
    /// 数据库类型。
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// SQL 方言。
    /// </summary>
    IDialect Dialect { get; }

    /// <summary>
    /// SQL 子句工厂。
    /// </summary>
    ISqlClauseFactory ClauseFactory { get; }

    /// <summary>
    /// 表引用解析器。
    /// </summary>
    ISqlTableReferenceParser TableReferenceParser { get; }

    /// <summary>
    /// 分页 SQL 渲染器。
    /// </summary>
    ISqlPaginationRenderer PaginationRenderer { get; }

    /// <summary>
    /// 参数管理器工厂。
    /// </summary>
    IParameterManagerFactory ParameterManagerFactory { get; }

    /// <summary>
    /// 参数字面值解析器。
    /// </summary>
    IParamLiteralsResolver ParamLiteralsResolver { get; }
}

