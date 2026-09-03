namespace Bing.Test.Shared;

/// <summary>
/// Provider 合同执行场景。
/// </summary>
public sealed class ProviderContractScenario
{
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

    public string Provider { get; }

    public string Capability { get; }

    public string Name { get; }

    public Func<CancellationToken, Task> ExecuteAsync { get; }

    public ProviderCapabilityEvidenceState? FixedState { get; }

    public string Evidence { get; }

    public string Artifact { get; }

    public Func<ProviderIntegrationEvidenceMetadata> RealIntegrationEvidenceFactory { get; }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。", parameterName) : value.Trim();
}

/// <summary>
/// 执行 Provider 合同并生成六态能力证据。
/// </summary>
public static class ProviderContractRunner
{
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