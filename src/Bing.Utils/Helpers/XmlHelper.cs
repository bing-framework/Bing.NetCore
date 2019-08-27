using System;
using System.Data;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// XML操作类.
    /// </summary>
    public class XmlHelper
    {
        #region 私有字段

        /// <summary>
        /// The object XML document
        /// </summary>
        protected XmlDocument objXmlDoc = new XmlDocument();

        #endregion 私有字段 

        #region 构造函数

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlHelper" /> class.
        /// </summary>
        /// <param name="xmlFile">The XML file.</param>
        public XmlHelper(string xmlFile)
        {
            objXmlDoc.Load(xmlFile);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlHelper"/> class.
        /// </summary>
        /// <param name="xml">The XML.</param>
        public XmlHelper(Stream xml)
        {
            objXmlDoc.Load(xml);
        }

        #endregion 构造函数 

        #region 公共方法

        /// <summary>
        /// 查找数据,返回一个DataView.
        /// </summary>
        /// <param name="xmlPathNode">The XML path node.</param>
        /// <returns>DataView.</returns>
        public DataView GetData(string xmlPathNode)
        {
            using (var ds = new DataSet())
            {
                var node = objXmlDoc.SelectSingleNode(xmlPathNode);
                if (node == null)
                    return null;
                using (var read = new StringReader(node.OuterXml))
                {
                    ds.ReadXml(read);
                    return ds.Tables.Count <= 0 ? null : ds.Tables[0].DefaultView;
                }
            }
        }

        /// <summary>
        /// 查找数据,返回一个DataView
        /// </summary>
        /// <param name="xmlPathNode">The XML path node.</param>
        /// <returns>DataView.</returns>
        public DataView GetAllData(string xmlPathNode)
        {
            using (var ds = new DataSet())
            {
                using (var nodelist = objXmlDoc.SelectNodes(xmlPathNode))
                {
                    if (nodelist == null)
                        return null;
                    foreach (XmlNode node in nodelist)
                    {
                        using (var read = new StringReader(node.OuterXml))
                        {
                            ds.ReadXml(read);
                        }
                    }
                    return ds.Tables.Count <= 0 ? null : ds.Tables[0].DefaultView;
                }
            }
        }

        /// <summary>
        /// 更新节点内容.
        /// </summary>
        /// <param name="xmlPathNode">The XML path node.</param>
        /// <param name="content">The content.</param>
        public void Replace(string xmlPathNode, string content)
        {
            objXmlDoc.SelectSingleNode(xmlPathNode).InnerText = content;
        }

        /// <summary>
        /// 删除一个节点.
        /// </summary>
        /// <param name="node">The node.</param>
        public void Delete(string node)
        {
            string mainNode = node.Substring(0, node.LastIndexOf("/"));
            objXmlDoc.SelectSingleNode(mainNode).RemoveChild(objXmlDoc.SelectSingleNode(node));
        }

        /// <summary>
        /// 插入一节点和此节点的一子节点
        /// </summary>
        /// <param name="mainNode">The main node.</param>
        /// <param name="childNode">The child node.</param>
        /// <param name="element">The element.</param>
        /// <param name="content">The content.</param>
        public void InsertNode(string mainNode, string childNode, string element, string content)
        {
            var objRootNode = objXmlDoc.SelectSingleNode(mainNode);
            var objChildNode = objXmlDoc.CreateElement(childNode);
            objRootNode.AppendChild(objChildNode);
            var objElement = objXmlDoc.CreateElement(element);
            objElement.InnerText = content;
            objChildNode.AppendChild(objElement);
        }

        /// <summary>
        /// 插入一个节点，带一属性。
        /// </summary>
        /// <param name="mainNode">The main node.</param>
        /// <param name="element">The element.</param>
        /// <param name="attrib">The attribute.</param>
        /// <param name="attribContent">Content of the attribute.</param>
        /// <param name="content">The content.</param>
        public void InsertElement(string mainNode, string element, string attrib, string attribContent, string content)
        {
            var objNode = objXmlDoc.SelectSingleNode(mainNode);
            var objElement = objXmlDoc.CreateElement(element);
            objElement.SetAttribute(attrib, attribContent);
            objElement.InnerText = content;
            objNode.AppendChild(objElement);
        }

        /// <summary>
        /// 插入一个节点，不带属性
        /// </summary>
        /// <param name="mainNode">The main node.</param>
        /// <param name="element">The element.</param>
        /// <param name="content">The content.</param>
        public void InsertElement(string mainNode, string element, string content)
        {
            var objNode = objXmlDoc.SelectSingleNode(mainNode);
            var objElement = objXmlDoc.CreateElement(element);
            objElement.InnerText = content;
            objNode.AppendChild(objElement);
        }

        /// <summary>
        /// 保存xml文档
        /// </summary>
        /// <param name="strXmlFile">The string XML file.</param>
        public void Save(string strXmlFile)
        {
            objXmlDoc.Save(strXmlFile);
            objXmlDoc = null;
        }

        /// <summary>
        /// 选择匹配 XPath 表达式的节点列表。
        /// </summary>
        /// <param name="xpath">XPath 表达式</param>
        /// <returns>一个 System.Xml.XmlNodeList，包含匹配 XPath 查询的节点集合。不应该要求将 XmlNodeList“实时”连接到 XML 文档。也就是说，XML 文档中的更改不会出现在 XmlNodeList 中，反之亦然。</returns>
        public XmlNodeList SelectNodes(string xpath)
        {
            return objXmlDoc.SelectNodes(xpath);
        }

        /// <summary>
        /// 验证xmlString是否符合指定的XmlSchema文件
        /// </summary>
        /// <param name="xmlFile">The XML file.</param>
        /// <param name="schemaFile">The schema file.</param>
        /// <returns>验证成功返回由xmlString生成的XmlDocument</returns>
        /// <exception cref="System.ArgumentException">参数不合法</exception>
        public static void Validate(string xmlFile, string schemaFile)
        {
            if (string.IsNullOrEmpty(xmlFile) || string.IsNullOrEmpty(schemaFile))
            {
                throw new ArgumentException("参数不合法");
            }

            XmlReader reader = null;
            try
            {
                var settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                settings.Schemas.Add(null, schemaFile);
                using (reader = XmlReader.Create(xmlFile, settings))
                {
                    while (reader.Read()) { }
                }
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        #endregion 公共方法 
    }
}
