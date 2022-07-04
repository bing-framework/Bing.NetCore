using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bing.AutoMapper;
using Bing.DependencyInjection;
using Bing.Tests.Samples;
using NSubstitute;
using Xunit;
using Bing.Extensions;
using Bing.ObjectMapping;
using Bing.Reflection;
using Bing.Uow;

namespace Bing.Tests.Applications
{
    /// <summary>
    /// 增删改查服务测试 - 批量保存
    /// </summary>
    public partial class CrudServiceTest
    {
        /// <summary>
        /// Id
        /// </summary>
        private Guid _id;

        /// <summary>
        /// Id2
        /// </summary>
        private Guid _id2;

        /// <summary>
        /// 实体
        /// </summary>
        private EntitySample _entity;

        /// <summary>
        /// 实体2
        /// </summary>
        private EntitySample _entity2;

        /// <summary>
        /// 数据传输对象
        /// </summary>
        private DtoSample _dto;

        /// <summary>
        /// 数据传输对象2
        /// </summary>
        private DtoSample _dto2;

        /// <summary>
        /// 增删改查服务
        /// </summary>
        private CrudServiceSample _service;

        /// <summary>
        /// 工作单元
        /// </summary>
        private IUnitOfWork _unitOfWork;

        /// <summary>
        /// 仓储
        /// </summary>
        private IRepositorySample _repository;

        /// <summary>
        /// 测试 - 初始化
        /// </summary>
        public CrudServiceTest()
        {
            _id = Guid.NewGuid();
            _id2 = Guid.NewGuid();
            _entity = new EntitySample(_id) { Name = "A" };
            _entity2 = new EntitySample(_id2) { Name = "B" };
            _dto = new DtoSample { Id = _id.ToString(), Name = "A" };
            _dto2 = new DtoSample { Id = _id2.ToString(), Name = "B" };
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _repository = Substitute.For<IRepositorySample>();
            _service = new CrudServiceSample(_unitOfWork, _repository);
            _service.LazyServiceProvider = Substitute.For<ILazyServiceProvider>();
            var allAssemblyFinder = new AppDomainAllAssemblyFinder();
            var mapperProfileTypeFinder = new MapperProfileTypeFinder(allAssemblyFinder);
            var instances = mapperProfileTypeFinder
                .FindAll()
                .Select(type => Bing.Reflection.Reflections.CreateInstance<IObjectMapperProfile>(type))
                .ToList();
            var configuration = new MapperConfiguration(cfg =>
            {
                foreach (var instance in instances)
                {

                    Debug.WriteLine($"初始化AutoMapper配置：{instance.GetType().FullName}");
                    instance.CreateMap();
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    cfg.AddProfile(instance as Profile);
                }
            });
            var mapper = new AutoMapperObjectMapper(configuration, instances);
            ObjectMapperExtensions.SetMapper(mapper);
        }

        /// <summary>
        /// 获取实体集合
        /// </summary>
        private List<EntitySample> GetEntities()
        {
            return new List<EntitySample> { _entity, _entity2 };
        }

        /// <summary>
        /// 测试 - 添加
        /// </summary>
        [Fact]
        public async Task Test_SaveAsync_Add()
        {
            await _service.SaveAsync(new DtoSample { Name = "a" });
            await _repository.Received().AddAsync(Arg.Is<EntitySample>(t => t.Name == "a"));
        }

        /// <summary>
        /// 测试 - 修改
        /// </summary>
        [Fact]
        public async Task Test_SaveAsync_Update()
        {
            var now = DateTime.Now;
            _repository.FindAsync(_id).Returns(t => new EntitySample(_id) { Name = "a", LastModificationTime = now });
            await _service.SaveAsync(new DtoSample { Id = _id.ToString(), Name = "b",LastModificationTime = now.AddDays(1)});
            await _repository.DidNotReceive().AddAsync(Arg.Any<EntitySample>());
            await _repository.Received().UpdateAsync(Arg.Is<EntitySample>(t => t.Name == "b"));
        }

        /// <summary>
        /// 测试 - 删除
        /// </summary>
        [Fact]
        public async Task Test_DeleteAsync()
        {
            var ids = new[] { _id, _id2 };
            _repository.FindByIdsAsync(ids.Join()).Returns(GetEntities());
            await _service.DeleteAsync(ids.Join());
            await _repository.Received().RemoveAsync(Arg.Is<List<EntitySample>>(t => t.All(d => ids.Contains(d.Id))));
        }

        /// <summary>
        /// 测试 - 删除 - id无效
        /// </summary>
        [Fact]
        public async Task Test_DeleteAsync_IdInvalid()
        {
            var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
            _repository.FindByIdsAsync(ids.Join()).Returns(new List<EntitySample>());
            await _service.DeleteAsync(ids.Join());
            await _repository.DidNotReceive().RemoveAsync(Arg.Any<List<EntitySample>>());
        }

    }
}
