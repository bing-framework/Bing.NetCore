namespace Bing.Utils.Parameters.Parsers
{
    /// <summary>
    /// 参数解析器工厂
    /// </summary>
    public interface IParameterParserFactory
    {
        /// <summary>
        /// 创建参数解析器
        /// </summary>
        /// <param name="type">参数解析器类型</param>
        /// <returns></returns>
        IParameterParser CreateParameterParser(ParameterParserType type);
    }
}
