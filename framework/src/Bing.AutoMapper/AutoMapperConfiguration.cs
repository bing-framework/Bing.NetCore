using AutoMapper;

namespace Bing.AutoMapper
{
    /// <summary>
    /// AutoMapper 配置
    /// </summary>
    public static class AutoMapperConfiguration
    {
        /// <summary>
        /// 映射器
        /// </summary>
        public static IMapper Mapper { get; private set; }

        /// <summary>
        /// 映射器配置
        /// </summary>
        public static MapperConfiguration MapperConfiguration { get; internal set; }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="config">映射器配置</param>
        public static void Init(MapperConfiguration config)
        {
            MapperConfiguration = config;
            Mapper = config.CreateMapper();
        }
    }
}
