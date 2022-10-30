using AutoMapper;
using Bing.ObjectMapping;
using Bing.SampleClasses;

namespace Bing.AutoMapper;

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
        CreateMap<Sample, Sample4>()
            .ForMember(o => o.StringValue, o => o.MapFrom((s, d) => s.StringValue + "-1"));
        //CreateMap<AutoMapperSourceSample, AutoMapperTargetSample>()
        //    .ForMember(x => x.TargetSampleValue, x => x.MapFrom(p => p.SourceStringValue + "-001"));
    }
}