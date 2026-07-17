namespace Bing.Test.Shared;

/// <summary>
/// 集成测试环境变量门控。
/// </summary>
public static class IntegrationTestGate
{
    internal const string GlobalEnvironmentVariable = "RUN_INTEGRATION_TESTS";

    /// <summary>
    /// 获取集成测试跳过原因。
    /// </summary>
    /// <param name="provider">数据库 Provider 名称。</param>
    /// <returns>未启用时返回跳过原因；已启用时返回 null。</returns>
    internal static string GetSkipReason(string provider = null)
    {
        if (IsEnabled(GlobalEnvironmentVariable))
            return null;
        if (string.IsNullOrWhiteSpace(provider))
            return $"集成测试已跳过。设置环境变量 {GlobalEnvironmentVariable}=true 以启用。";

        var providerVariable = GetProviderEnvironmentVariable(provider);
        if (IsEnabled(providerVariable))
            return null;
        return $"集成测试已跳过。设置环境变量 {GlobalEnvironmentVariable}=true 或 {providerVariable}=true 以启用。";
    }

    /// <summary>
    /// 判断指定 Provider 是否已启用集成测试。
    /// </summary>
    /// <param name="provider">数据库 Provider 名称。</param>
    /// <returns>已启用时返回 true。</returns>
    public static bool IsProviderEnabled(string provider = null) => GetSkipReason(provider) == null;

    /// <summary>
    /// 获取 Provider 对应的环境变量名称。
    /// </summary>
    /// <param name="provider">数据库 Provider 名称。</param>
    /// <returns>环境变量名称。</returns>
    /// <exception cref="ArgumentException">Provider 名称为空或仅含分隔符时抛出。</exception>
    internal static string GetProviderEnvironmentVariable(string provider)
    {
        var normalizedProvider = NormalizeProvider(provider);
        return $"RUN_{normalizedProvider}_INTEGRATION_TESTS";
    }

    /// <summary>
    /// 规范化 Provider 名称。
    /// </summary>
    /// <param name="provider">数据库 Provider 名称。</param>
    /// <returns>去除连字符和点号后的大写名称。</returns>
    /// <exception cref="ArgumentException">Provider 名称为空或仅含分隔符时抛出。</exception>
    internal static string NormalizeProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("集成测试 Provider 名称不能为空。", nameof(provider));
        var normalizedProvider = provider.Trim().Replace("-", string.Empty).Replace(".", string.Empty).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedProvider))
            throw new ArgumentException("集成测试 Provider 名称必须包含有效字符。", nameof(provider));
        return normalizedProvider;
    }

    /// <summary>
    /// 判断环境变量是否显式启用。
    /// </summary>
    /// <param name="environmentVariable">环境变量名称。</param>
    /// <returns>启用时返回 true。</returns>
    internal static bool IsEnabled(string environmentVariable) => string.Equals(
        Environment.GetEnvironmentVariable(environmentVariable), "true", StringComparison.OrdinalIgnoreCase);
}