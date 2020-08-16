using System;
using Bing.Tests.XUnitHelpers;
using Bing.Uow;
using NSubstitute;
using Xunit;

namespace Bing.Tests.Datas.UnitOfWorks
{
    /// <summary>
    /// 工作单元服务测试
    /// </summary>
    public class UnitOfWorkManagerTest
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        private readonly IUnitOfWork _unitOfWork;
        /// <summary>
        /// 工作单元2
        /// </summary>
        private readonly IUnitOfWork _unitOfWork2;
        /// <summary>
        /// 工作单元服务
        /// </summary>
        private readonly UnitOfWorkManager _manager;

        /// <summary>
        /// 初始化一个<see cref="UnitOfWorkManagerTest"/>类型的实例
        /// </summary>
        public UnitOfWorkManagerTest()
        {
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _unitOfWork2 = Substitute.For<IUnitOfWork>();
            _manager = new UnitOfWorkManager();
        }

        /// <summary>
        /// 测试 - 注册一个空工作单元
        /// </summary>
        [Fact]
        public void Test_Register_Null()
        {
            AssertHelper.Throws<ArgumentNullException>(() => _manager.Register(null));
        }

        /// <summary>
        /// 测试 - 注册一个工作单元并提交
        /// </summary>
        [Fact]
        public void Test_Commit_1()
        {
            _manager.Register(_unitOfWork);
            _manager.Commit();
            _unitOfWork.Received(1).Commit();
        }

        /// <summary>
        /// 测试 - 注册2个工作单元并提交
        /// </summary>
        [Fact]
        public void Test_Commit_2()
        {
            _manager.Register(_unitOfWork);
            _manager.Register(_unitOfWork2);
            _manager.Commit();
            _unitOfWork.Received(1).Commit();
            _unitOfWork.Received(1).Commit();
        }

        /// <summary>
        /// 测试 - 注册一个工作单元并提交
        /// </summary>
        [Fact]
        public async void Test_CommitAsync_1()
        {
            _manager.Register(_unitOfWork);
            await _manager.CommitAsync();
            await _unitOfWork.Received(1).CommitAsync();
        }

        /// <summary>
        /// 测试 - 注册2个工作单元并提交
        /// </summary>
        [Fact]
        public async void Test_CommitAsync_2()
        {
            _manager.Register(_unitOfWork);
            _manager.Register(_unitOfWork2);
            await _manager.CommitAsync();
            await _unitOfWork.Received(1).CommitAsync();
            await _unitOfWork.Received(1).CommitAsync();
        }
    }
}
