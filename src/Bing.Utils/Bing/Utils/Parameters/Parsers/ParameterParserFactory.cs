using System;

namespace Bing.Utils.Parameters.Parsers
{
    /// <summary>
    /// 参数解析器工厂
    /// </summary>
    public class ParameterParserFactory : IParameterParserFactory
    {
        /// <summary>
        /// 创建参数解析器
        /// </summary>
        /// <param name="type">参数解析器类型</param>
        public IParameterParser CreateParameterParser(ParameterParserType type)
        {
            switch (type)
            {
                case ParameterParserType.Url:
                    return new UrlParameterParser();

                case ParameterParserType.Json:
                    return new JsonParameterParser();

                case ParameterParserType.Jsonp:
                    return new JsonpParameterParser();

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
