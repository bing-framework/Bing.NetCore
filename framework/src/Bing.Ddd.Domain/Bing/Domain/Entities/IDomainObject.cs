using Bing.Validation;

namespace Bing.Domain.Entities;

/// <summary>
/// 表示可执行模型验证的领域对象。
/// </summary>
/// <remarks>实体和值对象可通过该契约参与统一的验证流程。</remarks>
public interface IDomainObject : IVerifyModel
{
}