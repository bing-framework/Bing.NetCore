using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;

namespace Bing.AutoMapper
{
    /// <summary>
    /// AutoMapper映射类
    /// </summary>
    public class AutoMapperMapper : Bing.Mapping.IMapper
    {
        /// <summary>
        /// 同步锁
        /// </summary>
        private static readonly object Sync = new object();

        #region MapTo(将源对象映射到目标对象)

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        public TDestination MapTo<TDestination>(object source) where TDestination : new() => MapTo(source, new TDestination());

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        public TDestination MapTo<TSource, TDestination>(TSource source, TDestination destination) => MapTo<TDestination>(source, destination);

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        private static TDestination MapTo<TDestination>(object source, TDestination destination)
        {
            try
            {
                if (source == null)
                    return default;
                if (destination == null)
                    return default;
                var sourceType = GetType(source);
                var destinationType = GetType(destination);
                return GetResult(sourceType, destinationType, source, destination);
            }
            catch (AutoMapperMappingException e)
            {
                return TryGetResult(e,source, destination);
            }
        }

        /// <summary>
        /// 尝试获取映射结果
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="ex">AutoMapper映射异常</param>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        private static TDestination TryGetResult<TDestination>(AutoMapperMappingException ex, object source, TDestination destination)
        {
            try
            {
                return GetResult(GetType(ex.MemberMap.SourceType), GetType(ex.MemberMap.DestinationType), source, destination);
            }
            catch (AutoMapperMappingException)
            {
                return GetResult(ex.MemberMap.SourceType, ex.MemberMap.DestinationType, source, destination);
            }
        }

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <param name="obj">对象</param>
        private static Type GetType(object obj) => GetType(obj.GetType());

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <param name="type">类型</param>
        private static Type GetType(Type type) => Reflection.Reflections.GetElementType(type);

        /// <summary>
        /// 获取映射结果
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        private static TDestination GetResult<TDestination>(Type sourceType, Type destinationType, object source, TDestination destination)
        {
            if (Exists(sourceType, destinationType))
                return GetResult(source, destination);
            lock (Sync)
            {
                if (Exists(sourceType, destinationType))
                    return GetResult(source, destination);
                Init(sourceType, destinationType);
            }
            return GetResult(source, destination);
        }

        /// <summary>
        /// 是否已存在映射配置
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        private static bool Exists(Type sourceType, Type destinationType) => AutoMapperConfiguration.MapperConfiguration?.FindTypeMapFor(sourceType, destinationType) != null;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        private static void Init(Type sourceType, Type destinationType)
        {
            if (AutoMapperConfiguration.MapperConfiguration == null)
            {
                var config = new MapperConfiguration(t => t.CreateMap(sourceType, destinationType));
                AutoMapperConfiguration.Init(config);
            }
            else
            {
                var maps = AutoMapperConfiguration.MapperConfiguration.GetAllTypeMaps();
                var config = new MapperConfiguration(t => t.CreateMap(sourceType, destinationType));
                foreach (var map in maps)
                    config.RegisterTypeMap(map);
                AutoMapperConfiguration.Init(config);
            }

        }

        /// <summary>
        /// 获取映射结果
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        private static TDestination GetResult<TDestination>(object source, TDestination destination) => AutoMapperConfiguration.Mapper.Map(source, destination);

        #endregion

        #region MapToList(将源集合映射到目标列表)

        /// <summary>
        /// 将源集合映射到目标列表
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型，范例：Sample，不用加List</typeparam>
        /// <param name="source">源集合</param>
        public List<TDestination> MapToList<TDestination>(IEnumerable source) => MapTo<List<TDestination>>(source);

        #endregion

        #region ToOutput(将数据源映射为指定输出DTO的集合)

        /// <summary>
        /// 将数据源映射为指定<typeparamref name="TOutputDto"/>集合
        /// </summary>
        /// <typeparam name="TOutputDto">输出Dto类型</typeparam>
        /// <param name="source">源类型</param>
        /// <param name="membersToExpand">成员展开</param>
        public IQueryable<TOutputDto> ToOutput<TOutputDto>(IQueryable source, params Expression<Func<TOutputDto, object>>[] membersToExpand) => source.ProjectTo(AutoMapperConfiguration.MapperConfiguration, membersToExpand);

        #endregion
    }
}
