using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Bing.Mapping
{
    /// <summary>
    /// 对象映射
    /// </summary>
    public interface IMapper
    {
        /// <summary>
        /// 将对象映射为指定类型
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        TDestination MapTo<TDestination>(object source) where TDestination : new();

        /// <summary>
        /// 将源对象的对象更新目标类型的对象
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        TDestination MapTo<TSource, TDestination>(TSource source, TDestination destination);

        /// <summary>
        /// 将源集合映射到目标列表
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型，范例：Sample，不用加List</typeparam>
        /// <param name="source">源集合</param>
        List<TDestination> MapToList<TDestination>(System.Collections.IEnumerable source);

        /// <summary>
        /// 将数据源映射为指定输出DTO的集合
        /// </summary>
        /// <typeparam name="TOutputDto">输出DTO类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="membersToExpand">成员展开</param>
        IQueryable<TOutputDto> ToOutput<TOutputDto>(IQueryable source,
            params Expression<Func<TOutputDto, object>>[] membersToExpand);
    }
}
