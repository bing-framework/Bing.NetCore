using System;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;

namespace Bing.AutoMapper
{
    /// <summary>
    /// AutoMapper对象映射器
    /// </summary>
    public class AutoMapperObjectMapper : Bing.ObjectMapping.IObjectMapper
    {
        /// <summary>
        /// 同步锁
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly object Sync = new object();

        /// <summary>
        /// 配置提供程序
        /// </summary>
        private IConfigurationProvider _configuration;

        /// <summary>
        /// AutoMapper对象映射器
        /// </summary>
        private IMapper _mapper;

        /// <summary>
        /// 初始化一个<see cref="AutoMapperObjectMapper"/>类型的实例
        /// </summary>
        /// <param name="configuration">AutoMapper配置提供程序</param>
        public AutoMapperObjectMapper(IConfigurationProvider configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _mapper = _configuration.CreateMapper();
        }

        #region Map(将源对象映射到目标对象)

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        public TDestination Map<TSource, TDestination>(TSource source) => Map<TSource, TDestination>(source, default);

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            try
            {
                if (source == null)
                    return default;
                var sourceType = GetType(source);
                var destinationType = GetType(destination);
                return GetResult(sourceType, destinationType, source, destination);
            }
            catch (AutoMapperMappingException e)
            {
                return GetResult(GetType(e.MemberMap.SourceType), GetType(e.MemberMap.DestinationType), source, destination);
            }
        }

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="obj">对象</param>
        private Type GetType<T>(T obj) => GetType(obj == null ? typeof(T) : obj.GetType());

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
        private TDestination GetResult<TDestination>(Type sourceType, Type destinationType, object source, TDestination destination)
        {
            if (Exists(sourceType, destinationType))
                return GetResult(source, destination);
            lock (Sync)
            {
                if (Exists(sourceType, destinationType))
                    return GetResult(source, destination);
                ConfigMap(sourceType, destinationType);
            }
            return GetResult(source, destination);
        }

        /// <summary>
        /// 是否已存在映射配置
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        private bool Exists(Type sourceType, Type destinationType) => _configuration.FindTypeMapFor(sourceType, destinationType) != null;

        /// <summary>
        /// 获取映射结果
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        private TDestination GetResult<TDestination>(object source, TDestination destination) => _mapper.Map(source, destination);

        /// <summary>
        /// 配置映射
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        private void ConfigMap(Type sourceType, Type destinationType)
        {
            var maps = _configuration.GetAllTypeMaps();
            _configuration=new MapperConfiguration(t =>
            {
                t.CreateMap(sourceType, destinationType);
                foreach (var map in maps) 
                    t.CreateMap(map.SourceType, map.DestinationType);
            });
            _mapper = _configuration.CreateMapper();
        }

        #endregion

        #region ToOutput(将数据源映射为指定输出DTO的集合)

        /// <summary>
        /// 将数据源映射为指定<typeparamref name="TOutputDto"/>集合
        /// </summary>
        /// <typeparam name="TOutputDto">输出Dto类型</typeparam>
        /// <param name="source">源类型</param>
        /// <param name="membersToExpand">成员展开</param>
        public IQueryable<TOutputDto> ToOutput<TOutputDto>(IQueryable source, params Expression<Func<TOutputDto, object>>[] membersToExpand) => source.ProjectTo(_configuration, membersToExpand);

        #endregion
    }
}
