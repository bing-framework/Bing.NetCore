using System;
using AspectCore.Extensions.DependencyInjection;
using Bing.DependencyInjection;
using Bing.Exceptions;
using Bing.Tests.Samples;
using Bing.Tests.XUnitHelpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Tests.Domains
{
    /// <summary>
    /// 验证测试
    /// </summary>
    public class ValidTest : IDisposable
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 测试初始化
        /// </summary>
        public ValidTest()
        {
            var services = new ServiceCollection();
            services.AddScoped<IRepositorySample, RepositorySample>();
            services.EnableAop();
            _serviceProvider = services.BuildServiceContextProvider();
        }

        /// <summary>
        /// 测试清理
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// 测试 - 添加时验证无效
        /// </summary>
        [Fact]
        public void Test_Add_Invalid()
        {
            var entity = new EntitySample();
            var repository = _serviceProvider.GetService<IRepositorySample>();
            AssertHelper.Throws<Warning>(() => repository.Add(entity), "名称不能为空");
        }

        /// <summary>
        /// 测试 - 添加时验证有效
        /// </summary>
        [Fact]
        public void Test_Add_Valid()
        {
            var entity = new EntitySample {Name = "a"};
            var repository = _serviceProvider.GetService<IRepositorySample>();
            repository.Add(entity);
        }
    }
}
