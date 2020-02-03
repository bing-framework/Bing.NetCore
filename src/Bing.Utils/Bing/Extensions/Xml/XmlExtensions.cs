using System.Xml;
using System.Xml.Linq;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// Xml 扩展
    /// </summary>
    public static class XmlExtensions
    {
        #region ToXElement(将XmlNode转换为XElement)

        /// <summary>
        /// 将XmlNode转换为XElement
        /// </summary>
        /// <param name="node">Xml节点</param>
        /// <returns></returns>
        public static XElement ToXElement(this XmlNode node)
        {
            XDocument xdoc = new XDocument();
            using (var xmlWriter = xdoc.CreateWriter())
            {
                node.WriteTo(xmlWriter);
            }

            return xdoc.Root;
        }

        #endregion

        #region ToXmlNode(将XElement转换为XmlNode)

        /// <summary>
        /// 将XElement转换为XmlNode
        /// </summary>
        /// <param name="element">Xml元素</param>
        /// <returns></returns>
        public static XmlNode ToXmlNode(this XElement element)
        {
            using (var xmlReader = element.CreateReader())
            {
                var xml = new XmlDocument();
                xml.Load(xmlReader);
                return xml;
            }
        }

        #endregion

    }
}
