using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using IMapper = Bing.Mapping.IMapper;

namespace Bing.AutoMapper
{
    /// <summary>
    /// AutoMapper映射类
    /// </summary>
    public class AutoMapperMapper : IMapper
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
        public TDestination MapTo<TDestination>(object source) where TDestination : new()
        {
            return MapTo(source, new TDestination());
        }

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        public TDestination MapTo<TSource, TDestination>(TSource source, TDestination destination)
        {
            return MapTo<TDestination>(source, destination);
        }

        /// <summary>
        /// 将源对象映射到目标对象
        /// </summary>
        /// <typeparam name="TDestination">目标类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="destination">目标对象</param>
        private static TDestination MapTo<TDestination>(object source, TDestination destination)
        {
            if (source == null)
            {
                return default(TDestination);
            }
            if (destination == null)
            {
                return default(TDestination);
            }
            var sourceType = GetType(source);
            var destinationType = GetType(destination);
            var map = GetMap(sourceType, destinationType);
            if (map != null)
            {
                return Mapper.Map(source, destination);
            }
            lock (Sync)
            {
                map = GetMap(sourceType, destinationType);
                if (map != null)
                {
                    return Mapper.Map(source, destination);
                }
                InitMaps(sourceType, destinationType);
            }
            return Mapper.Map(source, destination);
        }

        /// <summary>
        /// 获取映射配置
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        private static TypeMap GetMap(Type sourceType, Type destinationType)
        {
            try
            {
                return Mapper.Configuration.FindTypeMapFor(sourceType, destinationType);
            }
            catch (InvalidOperationException)
            {
                lock (Sync)
                {
                    try
                    {
                        return Mapper.Configuration.FindTypeMapFor(sourceType, destinationType);
                    }
                    catch (InvalidOperationException)
                    {
                        InitMaps(sourceType, destinationType);
                    }
                    return Mapper.Configuration.FindTypeMapFor(sourceType, destinationType);
                }
            }
        }

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <param name="obj">对象</param>
        private static Type GetType(object obj)
        {
            var type = obj.GetType();
            if (obj is System.Collections.IEnumerable == false)
            {
                return type;
            }
            if (type.IsArray)
            {
                return type.GetElementType();
            }
            var genericArgumentsTypes = type.GetTypeInfo().GetGenericArguments();
            if (genericArgumentsTypes == null || genericArgumentsTypes.Length == 0)
            {
                throw new ArgumentException("泛型类型参数不能为空");
            }
            return genericArgumentsTypes[0];
        }

        /// <summary>
        /// 初始化映射配置
        /// </summary>
        /// <param name="sourceType">源类型</param>
        /// <param name="destinationType">目标类型</param>
        private static void InitMaps(Type sourceType, Type destinationType)
        {
            try
            {
                var maps = Mapper.Configuration.GetAllTypeMaps();
                ClearConfig();
                Mapper.Initialize(config => { config.CreateMap(sourceType, destinationType); });
                foreach (var map in maps)
                {
                    Mapper.Configuration.RegisterTypeMap(map);
                }
            }
            catch (InvalidOperationException)
            {
                Mapper.Initialize(config => { config.CreateMap(sourceType, destinationType); });
            }
        }

        /// <summary>
        /// 清空配置
        /// </summary>
        private static void ClearConfig()
        {
            var typeMapper = typeof(Mapper).GetTypeInfo();
            var configuration = typeMapper.GetDeclaredField("_configuration");
            configuration.SetValue(null, null, BindingFlags.Static, null, CultureInfo.CurrentCulture);
        }

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
        /// <returns></returns>
        public IQueryable<TOutputDto> ToOutput<TOutputDto>(IQueryable source, params Expression<Func<TOutputDto, object>>[] membersToExpand)
        {
            return source.ProjectTo(membersToExpand);
        }

        #endregion

    }
}
