using AutoMapper;
using Bing.ObjectMapping;
using Bing.Tests.Samples;

namespace Bing.AutoMapper.Tests
{
    /// <summary>
    /// 测试 - 映射器配置文件
    /// </summary>
    public class TestMapperConfiguration : Profile, IObjectMapperProfile
    {
        /// <summary>
        /// 创建映射配置
        /// </summary>
        public void CreateMap()
        {
            CreateMap<AutoMapperSourceSample, AutoMapperTargetSample>()
                .ForMember(x => x.TargetSampleValue, x => x.MapFrom(p => p.SourceStringValue + "-001"));
        }
    }
}
