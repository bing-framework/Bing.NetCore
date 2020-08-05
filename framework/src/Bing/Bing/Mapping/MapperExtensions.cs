using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Bing.Extensions;
using Bing.Properties;

namespace Bing.Mapping
{
    /// <summary>
    /// 对象映射扩展
    /// </summary>
    public static class MapperExtensions
    {
        #region SetMapper(设置对象映射执行者)

        /// <summary>
        /// 对象映射执行者
        /// </summary>
        private static IMapper _mapper;

        /// <summary>
        /// 设置对象映射执行者
        /// </summary>
        /// <param name="mapper">对象映射执行者</param>
        public static void SetMapper(IMapper mapper)
        {
            mapper.CheckNotNull(nameof(mapper));
            _mapper = mapper;
        }

        #endregion

        #region MapTo(将源对象映射到目标对象)

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        public static TDestination MapTo<TSource, TDestination>(this TSource source, TDestination destination)
        {
            CheckMapper();
            source.CheckNull(nameof(source));
            destination.CheckNull(nameof(destination));
            return _mapper.MapTo(source, destination);
        }

        /// <summary>
        /// 检查映射执行者是否为空
        /// </summary>
        private static void CheckMapper()
        {
            if (_mapper == null)
                throw new NullReferenceException(R.Map_MapperIsNull);
        }

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        public static TDestination MapTo<TDestination>(this object source) where TDestination : new()
        {
            CheckMapper();
            source.CheckNull(nameof(source));
            return _mapper.MapTo<TDestination>(source);
        }

        #endregion

        #region MapToList(将源集合映射到目标列表)

        /// <summary>
        /// 将源集合映射到目标列表
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型，范例：Sample，不用加List</typeparam>
        /// <param name="source">源集合</param>
        public static List<TDestination> MapToList<TDestination>(this IEnumerable source)
        {
            CheckMapper();
            source.CheckNull(nameof(source));
            return _mapper.MapToList<TDestination>(source);
        }

        #endregion

        #region ToOutput(将数据源映射为指定输出DTO的集合)

        /// <summary>
        /// 将数据源映射为指定<typeparamref name="TOutputDto"/>集合
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TOutputDto">输出Dto类型</typeparam>
        /// <param name="source">源类型</param>
        /// <param name="membersToExpand">成员展开</param>
        public static IQueryable<TOutputDto> ToOutput<TEntity, TOutputDto>(this IQueryable<TEntity> source,
            params Expression<Func<TOutputDto, object>>[] membersToExpand)
        {
            CheckMapper();
            return _mapper.ToOutput<TOutputDto>(source, membersToExpand);
        }

        #endregion
    }
}
;
