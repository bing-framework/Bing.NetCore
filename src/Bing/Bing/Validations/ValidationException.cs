using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Bing.Exceptions;

namespace Bing.Validations
{
    /// <summary>
    /// 验证异常
    /// </summary>
    [Serializable]
    public class ValidationException : BingException
    {
        /// <summary>
        /// 验证标识
        /// </summary>
        private const string ValidationFlag = "__VALID_FLG";

        /// <summary>
        /// 验证消息键
        /// </summary>
        private const string ValidationMessageInfoKey = "__VALIDATION_MESSAGES";

        /// <summary>
        /// 验证消息集合
        /// </summary>
        private readonly IEnumerable<string> _validationMessage = Enumerable.Empty<string>();

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        public ValidationException() => Flag = ValidationFlag;

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        public ValidationException(string message) : base(message, ValidationFlag) { }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="flag">错误标识</param>
        public ValidationException(string message, string flag) : base(message, flag) { }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="innerException">内部异常</param>
        public ValidationException(string message, Exception innerException) : base(message, ValidationFlag, innerException) { }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="flag">错误标识</param>
        /// <param name="innerException">内部异常</param>
        public ValidationException(string message, string flag, Exception innerException) : base(message, flag, innerException) { }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="validationMessage">验证消息集合</param>
        public ValidationException(IEnumerable<string> validationMessage) : base(string.Empty, ValidationFlag) =>
            _validationMessage = validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="validationMessage">验证消息集合</param>
        /// <param name="flag">错误标识</param>
        public ValidationException(IEnumerable<string> validationMessage, string flag) : base(string.Empty, flag) =>
            _validationMessage = validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="validationMessage">验证消息集合</param>
        public ValidationException(string message, IEnumerable<string> validationMessage) : base(message, ValidationFlag) => _validationMessage =
            validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="validationMessage">验证消息集合</param>
        /// <param name="flag">错误标识</param>
        public ValidationException(string message, IEnumerable<string> validationMessage, string flag) : base(message, flag) => _validationMessage =
            validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">错误消息</param>
        public ValidationException(long errorCode, string message) : base(errorCode, message, ValidationFlag) { }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">错误消息</param>
        /// <param name="flag">错误标识</param>
        public ValidationException(long errorCode, string message, string flag) : base(errorCode, message, flag) { }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">错误消息</param>
        /// <param name="innerException">内部异常</param>
        public ValidationException(long errorCode, string message, Exception innerException) : base(errorCode, message, ValidationFlag, innerException) { }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">错误消息</param>
        /// <param name="flag">错误标识</param>
        /// <param name="innerException">内部异常</param>
        public ValidationException(long errorCode, string message, string flag, Exception innerException) : base(errorCode, message, flag, innerException) { }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="validationMessage">验证消息集合</param>
        public ValidationException(long errorCode, IEnumerable<string> validationMessage) : base(errorCode, string.Empty, ValidationFlag) => _validationMessage =
            validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="validationMessage">验证消息集合</param>
        /// <param name="flag">错误标识</param>
        public ValidationException(long errorCode, IEnumerable<string> validationMessage, string flag) : base(errorCode, string.Empty, flag) => _validationMessage =
            validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">错误消息</param>
        /// <param name="validationMessage">验证消息集合</param>
        public ValidationException(long errorCode, string message, IEnumerable<string> validationMessage) : base(errorCode, message, ValidationFlag) => _validationMessage =
            validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="message">错误消息</param>
        /// <param name="validationMessage">验证消息集合</param>
        /// <param name="flag">错误标识</param>
        public ValidationException(long errorCode, string message, IEnumerable<string> validationMessage, string flag) : base(errorCode, message, flag) => _validationMessage =
            validationMessage ?? throw new ArgumentNullException(nameof(validationMessage));

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="info">序列化信息</param>
        /// <param name="streamingContext">流上下文</param>
        protected ValidationException(SerializationInfo info, StreamingContext streamingContext) : base(info,
            streamingContext)
        {
            _validationMessage =
                (IEnumerable<string>)info.GetValue(ValidationMessageInfoKey, typeof(IEnumerable<string>));
            Flag = ValidationFlag;
        }

        /// <summary>
        /// 初始化一个<see cref="ValidationException"/>类型的实例
        /// </summary>
        /// <param name="options">Bing框架异常选项配置</param>
        public ValidationException(BingExceptionOptions options) : base(options)
        {
            if (options != null && options.ExtraErrors.TryGetValue(ValidationMessageInfoKey, out var value) &&
                value is IEnumerable<string> messages)
                _validationMessage = messages;
        }

        /// <summary>
        /// 验证消息集合
        /// </summary>
        public IEnumerable<string> ValidationMessage => _validationMessage;

        /// <summary>
        /// 获取对象数据
        /// </summary>
        /// <param name="info">序列化信息</param>
        /// <param name="streamingContext">流上下文</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext streamingContext)
        {
            base.GetObjectData(info, streamingContext);
            info.AddValue(ValidationMessageInfoKey, _validationMessage);
        }

        /// <summary>
        /// 获取完整的消息
        /// </summary>
        public override string GetFullMessage() => ToString();

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Message != null)
                sb.Append(Message);
            if (ValidationMessage != null)
            {
                foreach (var message in ValidationMessage)
                {
                    sb.AppendLine();
                    sb.Append("  ");
                    sb.Append(message);
                }
            }
            return sb.Length > 0 ? sb.ToString() : base.ToString();
        }
    }
}
