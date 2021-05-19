using System;
using System.Collections.Generic;

namespace Bing.ObjectMapping
{
    /// <summary>
    /// 对象映射器(<see cref="IObjectMapper"/>) 扩展
    /// </summary>
    public static class ObjectMapperExtensions
    {
        #region SetMapper(设置对象映射器)

        /// <summary>
        /// 对象映射器
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static IObjectMapper _mapper;

        /// <summary>
        /// 设置对象映射器
        /// </summary>
        /// <param name="mapper">对象映射器</param>
        public static void SetMapper(IObjectMapper mapper) => _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

        #endregion

        #region MapTo(将源对象映射到目标对象)

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        public static TDestination MapTo<TDestination>(this object source) where TDestination : new()
        {
            CheckMapper();
            return _mapper.Map<object, TDestination>(source);
        }

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
            return _mapper.Map(source, destination);
        }

        /// <summary>
        /// 检查映射执行者是否为空
        /// </summary>
        private static void CheckMapper()
        {
            if (_mapper == null)
                throw new NullReferenceException("ObjectMapperExtensions.Mapper不能为空，请先设置值");
        }

        #endregion

        #region MapToList(将源集合映射到目标列表)

        /// <summary>
        /// 将源集合映射到目标列表
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型，范例：Sample，不用加List</typeparam>
        /// <param name="source">源集合</param>
        public static List<TDestination> MapToList<TDestination>(this System.Collections.IEnumerable source) => MapTo<List<TDestination>>(source);

        #endregion
    }
}
