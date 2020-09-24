using AutoMapper;
using Bing.Mapping;
using Bing.Tests.Samples;

namespace Bing.AutoMapper.Tests
{
    /// <summary>
    /// 测试 - 映射器配置文件
    /// </summary>
    public class TestMapperConfiguration : Profile, IOrderedMapperProfile
    {
        /// <summary>
        /// 排序
        /// </summary>
        public int Order => 0;

        /// <summary>
        /// 初始化一个<see cref="TestMapperConfiguration"/>类型的实例
        /// </summary>
        public TestMapperConfiguration()
        {
            CreateMap<AutoMapperSourceSample, AutoMapperTargetSample>()
                .ForMember(x => x.TargetSampleValue, x => x.MapFrom(p => p.SourceStringValue));
        }
    }
}
