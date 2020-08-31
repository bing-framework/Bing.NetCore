using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Data;
using Bing.Admin.Service.Abstractions;

namespace Bing.Admin.Service.Implements
{
    /// <summary>
    /// 测试服务
    /// </summary>
    public class TestService : Bing.Application.Services.AppServiceBase, ITestService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 文件仓储
        /// </summary>
        protected IFileRepository FileRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="TestService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="fileRepository">文件仓储</param>
        public TestService(IAdminUnitOfWork unitOfWork, IFileRepository fileRepository)
        {
            UnitOfWork = unitOfWork;
            FileRepository = fileRepository;
        }

        /// <summary>
        /// 批量插入文件
        /// </summary>
        public async Task BatchInsertFileAsync(long qty)
        {
            var list = new List<File>();
            for (var i = 0; i < qty; i++)
            {
                var item = new File
                {
                    Name = $"批量生成-{DateTime.Now:yyyyMMddHHmmss}-{i}",
                    Size = i * 100,
                    ExtName = ".txt",
                    Address = "Test",
                    Creator = "系统初始化",
                    CreatorId = Guid.Empty,
                    CreationTime = DateTime.Now,
                };

                item.Init();
                list.Add(item);
            }

            await FileRepository.AddAsync(list);
            await UnitOfWork.CommitAsync();
        }
    }
}
