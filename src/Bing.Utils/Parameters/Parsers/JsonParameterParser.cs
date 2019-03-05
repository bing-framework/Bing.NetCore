using Bing.Utils.Extensions;
using Newtonsoft.Json.Linq;

namespace Bing.Utils.Parameters.Parsers
{
    /// <summary>
    /// Json参数解析器
    /// </summary>
    public class JsonParameterParser : ParameterParserBase, IParameterParser
    {
        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="data">数据</param>
        public override void LoadData(string data)
        {
            if (data.IsEmpty())
            {
                return;
            }

            var jObject = JObject.Parse(data);
            foreach (var token in jObject.Children())
            {
                AddNodes(token);
            }
        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="token">token节点</param>
        private void AddNodes(JToken token)
        {
            if (!(token is JProperty item))
            {
                return;
            }

            foreach (var value in item.Value)
            {
                AddNodes(value);
            }

            Add(item.Name, item.Value.SafeString());
        }
    }
}
