using System;

namespace Bing.Http
{
    /// <summary>
    /// 远程服务验证错误信息
    /// </summary>
    [Serializable]
    public class RemoteServiceValidationErrorInfo
    {
        /// <summary>
        /// 验证错误消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 无效成员（字段/属性）
        /// </summary>
        public string[] Members{ get; set; }

        /// <summary>
        /// 初始化一个<see cref="RemoteServiceValidationErrorInfo"/>类型的实例
        /// </summary>
        public RemoteServiceValidationErrorInfo(){}

        /// <summary>
        /// 初始化一个<see cref="RemoteServiceValidationErrorInfo"/>类型的实例
        /// </summary>
        /// <param name="message">验证错误消息</param>
        public RemoteServiceValidationErrorInfo(string message) => Message = message;

        /// <summary>
        /// 初始化一个<see cref="RemoteServiceValidationErrorInfo"/>类型的实例
        /// </summary>
        /// <param name="message">验证错误消息</param>
        /// <param name="members">无效成员（字段/属性）</param>
        public RemoteServiceValidationErrorInfo(string message, string[] members) : this(message) => Members = members;

        /// <summary>
        /// 初始化一个<see cref="RemoteServiceValidationErrorInfo"/>类型的实例
        /// </summary>
        /// <param name="message">验证错误消息</param>
        /// <param name="member">无效成员（字段/属性）</param>
        public RemoteServiceValidationErrorInfo(string message, string member) : this(message, new[] {member}) { }
    }
}
