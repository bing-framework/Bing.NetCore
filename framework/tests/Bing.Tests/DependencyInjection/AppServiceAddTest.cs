using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Tests.DependencyInjection
{
    public class AppServiceAddTest
    {
        [Fact]
        public void Test_Ctor()
        {
            IServiceCollection services = new ServiceCollection();
            var module = new DependencyModule();
            services = module.AddServices(services);
        }

        [IgnoreDependency]
        private interface IIgnoreService { }

        private interface ITestService : IIgnoreService { }

        private class TransientTestService : ITestService, ITransientDependency { }

        private class ScopedTestService : ITestService, IScopedDependency { }

        private class SingletonTestService : ITestService, ISingletonDependency { }

        private interface IGenericService<T> { }

        private class GenericService<T> : IGenericService<T>, IScopedDependency { }
    }
}
