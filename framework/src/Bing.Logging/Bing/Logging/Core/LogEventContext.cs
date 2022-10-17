using System.Collections.Generic;
using System.Linq;
using Bing.Logging.Core.Callers;
using Bing.Logging.ExtraSupports;
using Bing.Utils.Json;

namespace Bing.Logging.Core
{
    /// <summary>
    /// 日志事件上下文
    /// </summary>
    public class LogEventContext
    {
        #region Tags(标签列表)

        /// <summary>
        /// 标签列表
        /// </summary>
        private readonly List<string> _tags = new List<string>();

        /// <summary>
        /// 标签列表
        /// </summary>
        internal IReadOnlyList<string> Tags => _tags;

        /// <summary>
        /// 设置标签列表
        /// </summary>
        /// <param name="tags">标签列表</param>
        public LogEventContext SetTags(params string[] tags)
        {
            if (tags == null)
                return this;
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                if (_tags.Contains(tag))
                    continue;
                _tags.Add(tag);
            }
            return this;
        }

        #endregion

        #region Parameters(参数列表)

        /// <summary>
        /// 参数列表
        /// </summary>
        private readonly List<object> _parameters = new List<object>();

        /// <summary>
        /// 设置参数
        /// </summary>
        /// <param name="parameter">参数</param>
        public LogEventContext SetParameter(object parameter)
        {
            if (parameter == null)
                return this;
            _parameters.Add(parameter);
            return this;
        }

        /// <summary>
        /// 设置参数列表
        /// </summary>
        /// <param name="parameters">参数列表</param>
        public LogEventContext SetParameters(params object[] parameters)
        {
            foreach (var parameter in parameters)
                SetParameter(parameter);
            return this;
        }

        /// <summary>
        /// 参数列表
        /// </summary>
        internal IReadOnlyList<object> Parameters => _parameters;

        #endregion

        #region ExtraProperties(扩展属性)

        /// <summary>
        /// 扩展属性
        /// </summary>
        private readonly ContextData _extraProperties = new ContextData();

        /// <summary>
        /// 设置扩展属性
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="value">值</param>
        public LogEventContext SetExtraProperty(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                return this;
            _extraProperties.AddOrUpdateItem($"{ContextDataTypes.ExtraProperty}{name}", value, false);
            return this;
        }

        /// <summary>
        /// 扩展属性
        /// </summary>
        public ContextData ExtraProperties => _extraProperties;

        #endregion

        #region CallerInfo

        /// <summary>
        /// 调用者信息
        /// </summary>
        private ILogCallerInfo _callerInfo = NullLogCallerInfo.Instance;

        /// <summary>
        /// 设置调用者信息
        /// </summary>
        /// <param name="memberName">调用方法名称</param>
        /// <param name="sourceFilePath">调用文件路径</param>
        /// <param name="sourceLineNumber">调用代码行号</param>
        public LogEventContext SetCallerInfo(string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
        {
            if (!string.IsNullOrWhiteSpace(memberName) || !string.IsNullOrWhiteSpace(sourceFilePath) || sourceLineNumber > 0)
            {
                _callerInfo = new LogCallerInfo(memberName, sourceFilePath, sourceLineNumber);
            }
            return this;
        }

        /// <summary>
        /// 日志调用者信息
        /// </summary>
        public ILogCallerInfo LogCallerInfo => _callerInfo;

        #endregion

        #region ExposeScopeState(公开作用域状态)

        /// <summary>
        /// 公开作用域状态
        /// </summary>
        public IDictionary<string, object> ExposeScopeState()
        {
            var dict = new Dictionary<string, object>();
            // 写入标签
            if (Tags.Any()) 
                dict[ContextDataTypes.Tags] = Tags;
            // 写入扩展属性
            if (ExtraProperties.Any())
            {
                foreach (var kvp in ExtraProperties) 
                    dict.Add(kvp.Key, kvp.Value.Value);
            }
            // 写入日志调用者信息
            if (LogCallerInfo is not NullLogCallerInfo) 
                dict[ContextDataTypes.CallerInfo] = LogCallerInfo.ToJson();
            return dict;
        }

        #endregion
    }
}
