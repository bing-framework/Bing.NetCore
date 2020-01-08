using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using Bing.Utils.Helpers;

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

        /// <summary>
        /// 配置提供器
        /// </summary>
        private static IConfigurationProvider _config;

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
                return GetResult(GetType(e.MemberMap.SourceType), GetType(e.MemberMap.DestinationType), source,
                    destination);
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
        private static Type GetType(Type type) => Reflection.GetElementType(type);

        /// <summary>
        /// 获取映射结果
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        private static TDestination GetResult<TDestination>(Type sourceType,Type destinationType, object source, TDestination destination)
        {
            if (Exists(sourceType, destinationType))
                return GetResult(source, destination);
            lock (Sync)
            {
                if (Exists(sourceType, destinationType))
                    return GetResult(source, destination);
                Init(sourceType,destinationType);
            }
            return GetResult(source, destination);
        }

        /// <summary>
        /// 是否已存在映射配置
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        private static bool Exists(Type sourceType, Type destinationType) => _config?.FindTypeMapFor(sourceType, destinationType) != null;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        private static void Init(Type sourceType, Type destinationType)
        {
            if (_config == null)
            {
                _config = new MapperConfiguration(t => t.CreateMap(sourceType, destinationType));
                return;
            }
            var maps = _config.GetAllTypeMaps();
            _config = new MapperConfiguration(t => t.CreateMap(sourceType, destinationType));
            foreach (var map in maps)
                _config.RegisterTypeMap(map);
        }

        /// <summary>
        /// 获取映射结果
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        private static TDestination GetResult<TDestination>(object source, TDestination destination) => new Mapper(_config).Map(source, destination);

        #endregion

        #region MapToList(将源集合映射到目标列表)

        /// <summary>
        /// 将源集合映射到目标列表
        /// </summary>
        /// <typeparam name="TDestination">目标元素类型，范例：Sample，不用加List</typeparam>
        /// <param name="source">源集合</param>
        public List<TDestination> MapToList<TDestination>(IEnumerable source)
        {
            return MapTo<List<TDestination>>(source);
        }

        #endregion

        #region ToOutput(将数据源映射为指定输出DTO的集合)

        /// <summary>
        /// 将数据源映射为指定<typeparamref name="TOutputDto"/>集合
        /// </summary>
        /// <typeparam name="TOutputDto">输出Dto类型</typeparam>
        /// <param name="source">源类型</param>
        /// <param name="membersToExpand">成员展开</param>
        public IQueryable<TOutputDto> ToOutput<TOutputDto>(IQueryable source, params Expression<Func<TOutputDto, object>>[] membersToExpand)
        {
            //return source.ProjectTo(membersToExpand);
            throw new NotImplementedException();
        }

        #endregion

    }
}
