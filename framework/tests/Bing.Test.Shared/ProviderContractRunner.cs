namespace Bing.Test.Shared;

/// <summary>
/// Provider 合同执行场景。
/// </summary>
public sealed class ProviderContractScenario
{
    /// <summary>
    /// 初始化一个 <see cref="ProviderContractScenario"/> 类型的实例。
    /// </summary>
    /// <param name="provider">Provider 名称。</param>
    /// <param name="capability">能力名称。</param>
    /// <param name="name">场景名称。</param>
    /// <param name="executeAsync">执行场景的异步委托。</param>
    /// <param name="fixedState">未执行场景的固定状态。</param>
    /// <param name="evidence">场景证据说明。</param>
    /// <param name="artifact">场景制品路径或标识。</param>
    /// <param name="realIntegrationEvidenceFactory">创建真实集成证据的工厂。</param>
    public ProviderContractScenario(string provider, string capability, string name,
        Func<CancellationToken, Task> executeAsync = null,
        ProviderCapabilityEvidenceState? fixedState = null, string evidence = null, string artifact = null,
        Func<ProviderIntegrationEvidenceMetadata> realIntegrationEvidenceFactory = null)
    {
        Provider = Require(provider, nameof(provider));
        Capability = Require(capability, nameof(capability));
        Name = Require(name, nameof(name));
        ExecuteAsync = executeAsync;
        FixedState = fixedState;
        Evidence = evidence;
        Artifact = artifact;
        RealIntegrationEvidenceFactory = realIntegrationEvidenceFactory;
        if (executeAsync != null && fixedState != null)
            throw new ArgumentException("执行场景不能同时提供固定状态。", nameof(fixedState));
        if (executeAsync == null && fixedState == null)
            throw new ArgumentException("未执行场景必须提供固定状态。", nameof(executeAsync));
        if (fixedState == ProviderCapabilityEvidenceState.RealIntegrationProven)
            throw new ArgumentException("真实执行状态必须由场景执行结果产生。", nameof(fixedState));
        if (executeAsync == null && realIntegrationEvidenceFactory != null)
            throw new ArgumentException("固定状态场景不能提供真实集成证据工厂。", nameof(realIntegrationEvidenceFactory));
    }

    /// <summary>
    /// 获取 Provider 名称。
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// 获取能力名称。
    /// </summary>
    public string Capability { get; }

    /// <summary>
    /// 获取场景名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取场景执行委托。
    /// </summary>
    public Func<CancellationToken, Task> ExecuteAsync { get; }

    /// <summary>
    /// 获取未执行场景的固定状态。
    /// </summary>
    public ProviderCapabilityEvidenceState? FixedState { get; }

    /// <summary>
    /// 获取场景证据说明。
    /// </summary>
    public string Evidence { get; }

    /// <summary>
    /// 获取场景制品路径或标识。
    /// </summary>
    public string Artifact { get; }

    /// <summary>
    /// 获取真实集成证据工厂。
    /// </summary>
    public Func<ProviderIntegrationEvidenceMetadata> RealIntegrationEvidenceFactory { get; }

    /// <summary>
    /// 验证文本值不为空并去除首尾空白。
    /// </summary>
    /// <param name="value">待验证的文本值。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>规范化后的文本值。</returns>
    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value.Trim();
}

/// <summary>
/// 执行 Provider 合同并生成六态能力证据。
/// </summary>
public static class ProviderContractRunner
{
    /// <summary>
    /// 执行 Provider 合同场景并生成能力证据。
    /// </summary>
    /// <param name="scenarios">待执行的合同场景集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按输入顺序生成的能力证据。</returns>
    public static async Task<IReadOnlyList<ProviderCapabilityEvidence>> RunAsync(
        IEnumerable<ProviderContractScenario> scenarios, CancellationToken cancellationToken = default)
    {
        if (scenarios == null)
            throw new ArgumentNullException(nameof(scenarios));

        var results = new List<ProviderCapabilityEvidence>();
        foreach (var scenario in scenarios)
        {
            if (scenario == null)
                throw new ArgumentException("合同场景不能为空。", nameof(scenarios));
            var state = scenario.FixedState;
            if (state == null)
            {
                await scenario.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                var integrationEvidence = scenario.RealIntegrationEvidenceFactory?.Invoke();
                if (integrationEvidence?.ArtifactKind == ProviderCapabilityArtifactKind.ReleaseEvidence &&
                    !integrationEvidence.IsTrustedReleaseEvidence)
                    throw new ArgumentException("发布级证据必须由已验证的可信运行产生。",
                        nameof(scenario.RealIntegrationEvidenceFactory));
                state = integrationEvidence == null
                    ? ProviderCapabilityEvidenceState.UnitProven
                    : ProviderCapabilityEvidenceState.RealIntegrationProven;
                results.Add(state == ProviderCapabilityEvidenceState.RealIntegrationProven
                    ? ProviderCapabilityEvidence.CreateRealIntegration(scenario.Provider, scenario.Capability,
                        scenario.Name, scenario.Evidence, scenario.Artifact, integrationEvidence)
                    : new ProviderCapabilityEvidence(scenario.Provider, scenario.Capability, scenario.Name,
                        state.Value, scenario.Evidence, scenario.Artifact));
                continue;
            }
            results.Add(new ProviderCapabilityEvidence(scenario.Provider, scenario.Capability, scenario.Name,
                state.Value, scenario.Evidence, scenario.Artifact));
        }
        return results;
    }
}