using Bing.Modularity;
using Bing.Samples.Domain;

namespace Bing.Samples.Data
{
    /// <summary>
    /// Sample 数据模块
    /// </summary>
    [DependsOn(typeof(SamplesDomainModule))]
    public class SamplesDataModule : BingModule
    {
    }
}
