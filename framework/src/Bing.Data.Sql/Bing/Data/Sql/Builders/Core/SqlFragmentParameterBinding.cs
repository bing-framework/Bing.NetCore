using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>将逻辑键绑定到内置管理器保存的参数描述符。</summary>
internal readonly struct SqlFragmentParameterBinding
{
    private static readonly object MissingParameter = new();
    private readonly object _identity;

    internal SqlFragmentParameterBinding(SqlParameterKey key, IParameterManager manager, string name)
    {
        Key = key;
        _identity = GetIdentity(manager, name);
    }

    internal SqlParameterKey Key { get; }

    // 内置管理器的描述符只替换、不原地修改，克隆共享描述符而不共享参数字典。
    // 外部管理器没有参数变更身份合同，保留其原有行为。
    internal bool IsCurrent(IParameterManager manager, string name)
    {
        if (_identity == null)
            return true;
        if (_identity is SqlParam parameter)
            return GetStorage(manager)?.ContainsStoredSqlParam(parameter) == true;
        return ReferenceEquals(_identity, GetIdentity(manager, name));
    }

    private static object GetIdentity(IParameterManager manager, string name)
    {
        var storage = GetStorage(manager);
        if (storage == null)
            return null;
        return storage.TryGetStoredSqlParam(name, out var parameter)
            ? parameter : MissingParameter;
    }

    private static ParameterManager GetStorage(IParameterManager manager)
    {
        var storage = manager is ParameterLimitManagerBase limited ? limited.GetStorage(out _, out _) : manager;
        return storage?.GetType() == typeof(ParameterManager) ? (ParameterManager)storage : null;
    }
}
