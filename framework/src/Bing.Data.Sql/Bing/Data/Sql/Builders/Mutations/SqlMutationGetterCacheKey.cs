namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 属性 Getter 缓存键。
/// </summary>
/// <param name="SourceTypeHandle">源对象运行时类型句柄，用于隔离不同实体或原始值类型的 Getter。</param>
/// <param name="PropertyName">规范化属性名称，用于隔离同一类型上的不同属性 Getter。</param>
internal readonly record struct SqlMutationGetterCacheKey(RuntimeTypeHandle SourceTypeHandle, string PropertyName);