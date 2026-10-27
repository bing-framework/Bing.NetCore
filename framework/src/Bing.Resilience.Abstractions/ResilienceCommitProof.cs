namespace Bing.Resilience;

/// <summary>由持久操作层提供的提交证明。验证必须读取已提交的权威存储，不能以排队中状态代替。</summary>
public interface IResilienceDurableCommitProof
{
    string OperationId { get; }
    string ScopeHash { get; }
    string KeyHash { get; }
    string Fingerprint { get; }
    DateTimeOffset Deadline { get; }
    ValueTask<bool> VerifyAsync(CancellationToken token);
}
