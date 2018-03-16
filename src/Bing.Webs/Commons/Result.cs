using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Bing.Webs.Commons
{
    /// <summary>
    /// 返回结果
    /// </summary>
    public class Result:JsonResult
    {
        /// <summary>
        /// 状态码
        /// </summary>
        private readonly int _code;

        /// <summary>
        /// 消息
        /// </summary>
        private readonly string _message;

        /// <summary>
        /// 数据
        /// </summary>
        private readonly dynamic _data;

        /// <summary>
        /// 操作时间
        /// </summary>
        private readonly DateTime _operationTime;        

        /// <summary>
        /// 初始化一个<see cref="Result"/>类型的实例
        /// </summary>
        /// <param name="code">状态码</param>
        /// <param name="message">消息</param>
        /// <param name="data">数据</param>
        public Result(int code, string message, dynamic data = null):base(null)
        {
            _code = code;
            _message = message;
            _data = data;
            _operationTime = DateTime.Now;
        }

        /// <summary>
        /// 初始化返回结果
        /// </summary>
        /// <param name="code">状态码</param>
        /// <param name="message">消息</param>
        /// <param name="data">数据</param>
        public Result(StateCode code, string message, dynamic data = null) : base(null)
        {
            _code = code.Value();
            _message = message;
            _data = data;
            _operationTime=DateTime.Now;
        }

        /// <summary>
        /// 执行结果
        /// </summary>
        public override Task ExecuteResultAsync(ActionContext context)
        {
            if (_data != null)
            {
                this.Value = new
                {
                    Code = _code,
                    Message = _message,
                    OperationTime = _operationTime,
                    Data = _data
                };
            }
            else
            {
                this.Value = new
                {
                    Code = _code,
                    Message = _message,
                    OperationTime = _operationTime,
                };
            }            
            return base.ExecuteResultAsync(context);
        }
    }
}
