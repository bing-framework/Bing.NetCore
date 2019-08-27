using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Xml操作 - 工具
    /// </summary>
    public partial class Xml
    {
        /// <summary>
        /// 将Xml字符串转换为XDocument
        /// </summary>
        /// <param name="xml">Xml字符串</param>
        public static XDocument ToDocument(string xml) => XDocument.Parse(xml);

        /// <summary>
        /// 将Xml字符串转换为XELement列表
        /// </summary>
        /// <param name="xml">Xml字符串</param>
        public static List<XElement> ToElements(string xml)
        {
            var document = ToDocument(xml);
            return document?.Root == null ? new List<XElement>() : document.Root.Elements().ToList();
        }

        /// <summary>
        /// 校验Xml字符串是否符合指定Xml架构文件
        /// </summary>
        /// <param name="xmlFile">Xml文件</param>
        /// <param name="schemaFile">架构文件</param>
        public static void Validate(string xmlFile, string schemaFile)
        {
            if(string.IsNullOrWhiteSpace(xmlFile))
                throw new ArgumentNullException(nameof(xmlFile));
            if(string.IsNullOrWhiteSpace(schemaFile))
                throw new ArgumentNullException(nameof(schemaFile));
            XmlReader reader = null;
            try
            {
                var result = new Tuple<bool, string>(true, string.Empty);
                var settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                settings.Schemas.Add(null, schemaFile);
                // 设置验证Xml出错时的事件
                settings.ValidationEventHandler += (obj, e) =>
                {
                    result = new Tuple<bool, string>(false, e.Message);
                };
                using (reader = XmlReader.Create(xmlFile, settings))
                {
                    while (reader.Read()) { }
                }
                if(!result.Item1)
                    throw new ArgumentException(result.Item2);
            }
            finally
            {
                reader?.Close();
            }
        }
    }
}
