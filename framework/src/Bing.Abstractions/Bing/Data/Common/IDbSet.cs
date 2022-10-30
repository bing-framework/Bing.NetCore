namespace Bing.Data.Common;

/// <summary>
/// DbSet 元接口
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface IDbSet<TEntity> where TEntity : class
{
}