using System;
using System.Linq;
using System.Xml;
using Bing.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// Xml节点(<see cref="XmlNode"/>) 扩展
    /// </summary>
    public static class XmlNodeExtensions
    {
        #region CreateChildNode(创建Xml子节点)

        /// <summary>
        /// 创建Xml子节点，并追加到父节点
        /// </summary>
        /// <param name="parentNode">父节点</param>
        /// <param name="name">子节点的名称</param>
        /// <param name="namespaceUri">节点的命名空间</param>
        public static XmlNode CreateChildNode(this XmlNode parentNode, string name, string namespaceUri = "")
        {
            var document = parentNode is XmlDocument xmlDocument ? xmlDocument : parentNode.OwnerDocument;
            if (document == null)
                throw new ArgumentException(nameof(document));
            var node = !string.IsNullOrWhiteSpace(namespaceUri)
                ? document.CreateElement(name, namespaceUri)
                : document.CreateElement(name);
            parentNode.AppendChild(node);
            return node;
        }

        #endregion

        #region CreateCDataSection(创建CData节点)

        /// <summary>
        /// 创建CData节点，并追加到父节点
        /// </summary>
        /// <param name="parentNode">父节点</param>
        /// <param name="data">CData节</param>
        public static XmlCDataSection CreateCDataSection(this XmlNode parentNode, string data = "")
        {
            var document = parentNode is XmlDocument xmlDocument ? xmlDocument : parentNode.OwnerDocument;
            if (document == null)
                throw new ArgumentException(nameof(document));
            var node = document.CreateCDataSection(data);
            parentNode.AppendChild(node);
            return node;
        }

        #endregion

        #region GetCDataSection(获取CData节点的内容)

        /// <summary>
        /// 获取CData节点的内容
        /// </summary>
        /// <param name="parentNode">父节点</param>
        public static string GetCdataSection(this XmlNode parentNode) => parentNode.ChildNodes.OfType<XmlCDataSection>().Select(node => node.Value).FirstOrDefault();

        #endregion

        #region GetAttribute(获取Xml节点属性值)

        /// <summary>
        /// 获取Xml节点属性值
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="name">属性名</param>
        /// <param name="defaultValue">默认值。如果没有匹配属性存在</param>
        public static string GetAttribute(this XmlNode node, string name, string defaultValue = null)
        {
            if (node.Attributes == null)
                return defaultValue;
            var attribute = node.Attributes[name];
            return attribute != null ? attribute.InnerText : defaultValue;
        }

        /// <summary>
        /// 获取Xml节点属性值
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="node">节点</param>
        /// <param name="name">属性名</param>
        /// <param name="defaultValue">>默认值。如果没有匹配属性存在</param>
        public static T GetAttribute<T>(this XmlNode node, string name, T defaultValue = default(T))
        {
            var value = node.GetAttribute(name);
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            return Conv.To<T>(value);
        }

        #endregion

        #region SetAttribute(设置Xml节点属性值)

        /// <summary>
        /// 设置Xml节点属性值
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        public static void SetAttribute(this XmlNode node, string name, object value) => SetAttribute(node, name, value.SafeString());

        /// <summary>
        /// 设置Xml节点属性值
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        public static void SetAttribute(this XmlNode node, string name, string value)
        {
            if (node == null)
                return;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                return;
            if (node.Attributes == null)
                return;
            var attribute = node.Attributes[name, node.NamespaceURI];
            if (attribute == null)
            {
                attribute = node.OwnerDocument?.CreateAttribute(name, node.OwnerDocument.NamespaceURI);
                node.Attributes.Append(attribute);
            }

            attribute.InnerText = value;
        }

        #endregion

    }
}
