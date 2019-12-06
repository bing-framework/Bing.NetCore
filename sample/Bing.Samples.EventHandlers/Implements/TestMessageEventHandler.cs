using System;
using System.Threading.Tasks;
using Bing.Events;
using Bing.Exceptions;
using Bing.Logs.Extensions;
using Bing.Samples.Data;
using Bing.Samples.Domain.Events;
using Bing.Samples.Domain.Models;
using Bing.Samples.Domain.Services.Abstractions;
using Bing.Samples.EventHandlers.Abstractions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Samples.EventHandlers.Implements
{
    /// <summary>
    /// 测试 消息事件处理器
    /// </summary>
    public class TestMessageEventHandler : MessageEventHandlerBase, ITestMessageEventHandler
    {
        /// <summary>
        /// 初始化一个<see cref="TestMessageEventHandler"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="roleManager">角色管理</param>
        public TestMessageEventHandler(ISampleUnitOfWork unitOfWork, IRoleManager roleManager)
        {
            UnitOfWork = unitOfWork;
            RoleManager = roleManager;
        }

        /// <summary>
        /// 工作单元
        /// </summary>
        protected ISampleUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 角色管理
        /// </summary>
        protected IRoleManager RoleManager { get; set; }

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="message">消息</param>
        [EventHandler("CreateRole")]
        public async Task CreateRoleAsync(CreateRoleMessage message)
        {
            var role = new Role();
            role.Code = message.Code;
            role.Name = message.Name;
            role.Type = message.Type;
            role.Enabled = true;
            role.IsDeleted = false;
            //var begin = DateTime.Now;
            //Log.Caption("测试时间开始");
            //await Task.Delay(10000);
            //var end = DateTime.Now;
            //Log.Content($"{begin.ToDateTimeString()},{end.ToDateTimeString()}").Info();
            //throw new Warning("测试异常");
            await RoleManager.CreateAsync(role);
            await UnitOfWork.CommitAsync();
        }
    }
}
