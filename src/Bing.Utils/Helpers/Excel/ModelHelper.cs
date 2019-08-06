// ***********************************************************************
// Assembly         : Utilities
// Author           : zhuzhiqing
// Created          : 06-27-2014
//
// Last Modified By : zhuzhiqing
// Last Modified On : 01-29-2015
/* * * * * * * * * * * * * * * * * * * * * * * * * * *
 * 作者：朱志清
 * 日期：2009-05-30
 * 描述：
 * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;
using System.Xml.Serialization;

namespace Bing.Utils.Helpers
{
    // TODO:新引入需整理
    /// <summary>
    /// Class ModelHelper.
    /// </summary>
    public static class ModelHelper
    {
        #region 序列化与反序列化

        /// <summary>
        /// 序列化对象
        /// </summary>
        /// <param name="obj">要序列化的对象</param>
        /// <returns>返回二进制</returns>
        public static byte[] SerializeModel(Object obj)
        {
            if (obj != null)
            {
                var binaryFormatter = new BinaryFormatter();
                var ms = new MemoryStream();
                binaryFormatter.Serialize(ms, obj);
                ms.Position = 0;
                var b = new Byte[ms.Length];
                ms.Read(b, 0, b.Length);
                ms.Close();
                return b;
            }
            else
                return new byte[0];
        }

        /// <summary>
        /// 序列化对象到指定的文件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t">需要序列化的对象</param>
        /// <param name="fileName">文件名称</param>
        public static void SerializeModel<T>(T t, string fileName) where T : class, new()
        {
            FileStream fs = null;
            try
            {
                // serialize it...
                fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                XmlSerializer serializer = new XmlSerializer(t.GetType());
                serializer.Serialize(fs, t);
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }
        }

        /// <summary>
        /// 反序列化对象
        /// </summary>
        /// <param name="b">要反序列化的二进制</param>
        /// <param name="sampleModel">The sample model.</param>
        /// <returns>返回对象</returns>
        public static object DeserializeModel(byte[] b, object sampleModel)
        {
            if (b == null || b.Length == 0)
                return sampleModel;

            var result = new object();
            var binaryFormatter = new BinaryFormatter();
            var ms = new MemoryStream();
            try
            {
                ms.Write(b, 0, b.Length);
                ms.Position = 0;
                result = binaryFormatter.Deserialize(ms);
                ms.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return result;
        }

        /// <summary>
        /// 反序列化指定的文件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName">文件</param>
        /// <returns>反系列化后的class  文件不存在返回Null</returns>
        public static T DeserializeModel<T>(string fileName) where T : class, new()
        {
            if (!File.Exists(fileName))
            {
                return null;
            }

            FileStream fs = null;
            try
            {
                // open the stream...
                fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                return serializer.Deserialize(fs) as T;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }
        }

        #endregion

        #region Model与XML互相转换

        /// <summary>
        /// Model转化为XML的方法
        /// </summary>
        /// <param name="model">要转化的Model</param>
        /// <returns>System.String.</returns>
        public static string ModelToXml(object model)
        {
            var xmldoc = new XmlDocument();
            XmlElement modelNode = xmldoc.CreateElement("Model");
            xmldoc.AppendChild(modelNode);

            if (model != null)
            {
                foreach (PropertyInfo property in model.GetType().GetProperties())
                {
                    XmlElement attribute = xmldoc.CreateElement(property.Name);
                    if (property.GetValue(model, null) != null)
                        attribute.InnerText = property.GetValue(model, null).ToString();
                    else
                        attribute.InnerText = "[Null]";
                    modelNode.AppendChild(attribute);
                }
            }

            return xmldoc.OuterXml;
        }

        /// <summary>
        /// Model转化为XML的方法
        /// </summary>
        /// <param name="model">要转化的Model</param>
        /// <param name="rootName">xml的根节点名</param>
        /// <param name="isAttribite">是否生成属性模式 ture:属性模式 false:节点模式</param>
        /// <returns>System.String.</returns>
        public static string ModelToXml(object model, string rootName, bool isAttribite)
        {
            var xmldoc = new XmlDocument();
            XmlElement modelNode = xmldoc.CreateElement(rootName);
            xmldoc.AppendChild(modelNode);

            if (model != null)
            {
                foreach (PropertyInfo property in model.GetType().GetProperties())
                {
                    if (!isAttribite)
                    {
                        XmlElement attribute = xmldoc.CreateElement(property.Name);
                        if (property.GetValue(model, null) != null)
                            attribute.InnerText = property.GetValue(model, null).ToString();
                        else
                            attribute.InnerText = "[Null]";
                        modelNode.AppendChild(attribute);
                    }
                    else
                    {
                        var innerText = "";
                        if (property.GetValue(model, null) != null)
                            innerText = property.GetValue(model, null).ToString();
                        else
                            innerText = "[Null]";
                        modelNode.SetAttribute(property.Name, innerText);
                    }
                }
            }
            return xmldoc.OuterXml;
        }

        /// <summary>
        /// XML转化为Model的方法
        /// </summary>
        /// <param name="xml">要转化的XML</param>
        /// <param name="sampleModel">Model的实体示例，New一个出来即可</param>
        /// <returns>System.Object.</returns>
        public static object XmlToModel(string xml, object sampleModel)
        {
            if (string.IsNullOrEmpty(xml))
                return sampleModel;
            var xmldoc = new XmlDocument();
            xmldoc.LoadXml(xml);
            return XmlToModel(xmldoc.SelectSingleNode("Model"), sampleModel, false);
        }

        /// <summary>
        /// XML转化为Model的方法
        /// </summary>
        /// <param name="xmlNode">要转化的XML</param>
        /// <param name="sampleModel">Model的实体示例，New一个出来即可</param>
        /// <param name="isAttribite">是否使用属性映射</param>
        /// <returns>System.Object.</returns>
        public static object XmlToModel(XmlNode xmlNode, object sampleModel, bool isAttribite)
        {
            if (xmlNode == null) return sampleModel;
            if (isAttribite)
            {
                Debug.Assert(xmlNode.Attributes != null, "xmlNode.Attributes != null");
                foreach (XmlAttribute node in xmlNode.Attributes)
                {
                    foreach (PropertyInfo property in sampleModel.GetType().GetProperties())
                    {
                        if (string.Compare(node.Name, property.Name, true) == 0)
                        {
                            SetValue(sampleModel, property, node.Value);
                        }
                    }
                }
            }
            else
            {
                XmlNodeList attributes = xmlNode.ChildNodes;
                foreach (XmlNode node in attributes)
                {
                    foreach (PropertyInfo property in sampleModel.GetType().GetProperties())
                    {
                        if (string.Compare(node.Name, property.Name, true) == 0)
                        {
                            SetValue(sampleModel, property, node.InnerText);
                        }
                    }
                }
            }
            return sampleModel;
        }

        /// <summary>
        /// Sets the value.
        /// </summary>
        /// <param name="sampleModel">The sample model.</param>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        private static void SetValue(object sampleModel, PropertyInfo property, string value)
        {
            if (value != "[Null]")
            {
                if (property.PropertyType == typeof(Guid))
                    property.SetValue(sampleModel, new Guid(value), null);
                else
                    property.SetValue(sampleModel, Convert.ChangeType(value, property.PropertyType),
                                      null);
            }
            else
            {
                property.SetValue(sampleModel, null, null);
            }
        }

        #endregion

        #region DataRow转换为实体类

        /// <summary>
        /// 将泛型类转换成DataTable
        /// </summary>
        /// <typeparam name="T">指定要转换的泛型</typeparam>
        /// <param name="varlist">源泛型.</param>
        /// <param name="tableName">DataTable的表名.</param>
        /// <returns>DataTable.</returns>
        public static DataTable ListToDataTable<T>(IEnumerable<T> varlist, string tableName)
        {
            DataTable dtReturn = string.IsNullOrEmpty(tableName) ? new DataTable() : new DataTable(tableName);

            // column names
            PropertyInfo[] oProps = null;
            if (oProps == null)
            {
                oProps = (typeof(T)).GetProperties();
                foreach (PropertyInfo pi in oProps)
                {
                    Type colType = pi.PropertyType;

                    if ((colType.IsGenericType) && (colType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                    {
                        colType = colType.GetGenericArguments()[0];
                    }

                    dtReturn.Columns.Add(new DataColumn(pi.Name, colType));
                }
            }

            if (varlist == null) return dtReturn;

            foreach (T rec in varlist)
            {
                DataRow dr = dtReturn.NewRow();
                foreach (PropertyInfo pi in oProps)
                {
                    dr[pi.Name] = pi.GetValue(rec, null) ?? DBNull.Value;
                }

                dtReturn.Rows.Add(dr);
            }
            return dtReturn;
        }

        /// <summary>
        /// 特定List数据转换成table,可自动生成表列名，也可传入list定义列名
        /// </summary>
        /// <param name="varlist"></param>
        /// <param name="tableName"></param>
        /// <param name="listTitleColumn"></param>
        /// <returns></returns>
        public static DataTable ListToDataTable(List<List<string>> varlist, string tableName, List<string> listTitleColumn = null)
        {
            DataTable dtReturn = string.IsNullOrEmpty(tableName) ? new DataTable() : new DataTable(tableName);

            // column names
            if (varlist == null) return dtReturn;
            if (listTitleColumn == null || listTitleColumn.Count == 0)
            {
                if (varlist.Count > 0)
                {
                    List<string> list = varlist[0];
                    for (int i = 0; i < list.Count; i++)
                    {
                        dtReturn.Columns.Add(new DataColumn(i.ToString()));
                    }
                }
            }
            else
            {
                for (int i = 0; i < listTitleColumn.Count; i++)
                {
                    dtReturn.Columns.Add(new DataColumn(listTitleColumn[i]));
                }
            }
            foreach (var list in varlist)
            {
                DataRow dr = dtReturn.NewRow();
                for (int i = 0; i < list.Count; i++)
                {
                    dr[i.ToString()] = list[i];
                }
                dtReturn.Rows.Add(dr);
            }
            return dtReturn;
        }

        /// <summary>
        /// 将泛型类转换成DataTable
        /// </summary>
        /// <typeparam name="T">目标实体泛型</typeparam>
        /// <param name="dt">DataTable</param>
        /// <returns>IList{``0}.</returns>
        public static IList<T> DataTableToList<T>(DataTable dt) where T : class, new()
        {
            IList<T> list = new List<T>();
            //T model = default(T);
            for (int iRow = 0; iRow < dt.Rows.Count; iRow++)
            {
                DataRow dr = dt.Rows[iRow];
                T t = DataRowToModel<T>(dr);
                list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// 转换DataRow为给定的实体类.
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="row">The row.</param>
        /// <returns>返回实体类或者Null</returns>
        public static T DataRowToModel<T>(DataRow row) where T : class, new()
        {
            if (row == null) return null;

            var model = new T();
            //PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(model.GetType());
            PropertyInfo[] properties = model.GetType().GetProperties();
            foreach (PropertyInfo p in properties)
            {
                // we can't update value type properties that are read-only
                if (p.PropertyType.IsValueType && !p.CanWrite)
                {
                    continue;
                }
                if (row.Table.Columns.Contains(p.Name))
                {
                    object value = ConvertSimpleType(CultureInfo.CurrentCulture, row[p.Name], p.PropertyType);

                    if (value == null)
                    {
                        if (!TypeAllowsNullValue(p.PropertyType))
                        {
                            continue;
                        }
                    }
                    p.SetValue(model, value, null);
                }
            }
            return model;
        }

        /// <summary>
        /// Types the allows null value.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool TypeAllowsNullValue(Type type)
        {
            // Only reference types and Nullable<> types allow null values
            return (!type.IsValueType ||
                    (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)));
        }

        /// <summary>
        /// Converts the type of the simple.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="value">The value.</param>
        /// <param name="destinationType">Type of the destination.</param>
        /// <returns>System.Object.</returns>
        public static object ConvertSimpleType(CultureInfo culture, object value, Type destinationType)
        {
            if (value == null || destinationType.IsInstanceOfType(value))
            {
                return value;
            }
            if (value == DBNull.Value)
            {
                return null;
            }
            // if this is a user-input value but the user didn't type anything, return no value
            var valueAsString = value as string;
            if (valueAsString != null && valueAsString.Length == 0)
            {
                return null;
            }

            TypeConverter converter = TypeDescriptor.GetConverter(destinationType);
            if (destinationType.IsGenericType && destinationType.GetGenericTypeDefinition().Equals(typeof(Nullable<>))
                && value.GetType() != typeof(string))
            {
                var nulla = new NullableConverter(destinationType);
                destinationType = nulla.UnderlyingType;
                if (destinationType.IsEnum)
                {
                    return System.Enum.ToObject(destinationType, value);
                }
            }

            bool canConvertFrom = converter.CanConvertFrom(value.GetType());
            if (!canConvertFrom)
            {
                converter = TypeDescriptor.GetConverter(value.GetType());
            }

            if (!(canConvertFrom || converter.CanConvertTo(destinationType)))
            {
                if (destinationType.IsEnum)
                {
                    return System.Enum.ToObject(destinationType, value);
                }
            }

            try
            {
                object convertedValue = (canConvertFrom)
                                            ? converter.ConvertFrom(null, culture, value)
                                            : converter.ConvertTo(null, culture, value, destinationType);
                return convertedValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                //string message = String.Format(CultureInfo.CurrentUICulture, value.GetType().FullName,
                //                               destinationType.FullName);
                //throw new InvalidOperationException(message, ex);
            }
            return null;
        }

        /// <summary>
        /// 自动赋值 拷贝from的值到to
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public static void AutoAssign(object from, object to)
        {
            var props = from.GetType().GetProperties();
            foreach (var item in props)
            {
                item.SetValue(to, item.GetValue(from, null), null);
            }
        }

        /// <summary>
        /// 自动赋值 拷贝from的值到to
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="onlyProps">只映射指定的属性</param>
        public static void AutoAssign(Dictionary<string, object> from, object to, string[] onlyProps)
        {
            var props = to.GetType().GetProperties();
            foreach (var item in props)
            {
                if (onlyProps != null && onlyProps.Contains(item.Name, StringComparer.Create(CultureInfo.CurrentCulture, true)))
                {
                    var key = from.Keys.FirstOrDefault(p => p.Equals(item.Name, StringComparison.CurrentCultureIgnoreCase));
                    if (!string.IsNullOrEmpty(key))
                        item.SetValue(to, from[key], null);
                }
            }
        }

        /// <summary>
        /// 自动赋值 拷贝from的值到to
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="excludeProps">排除拷贝的属性</param>
        public static void AutoAssign(object from, object to, List<string> excludeProps)
        {
            var props = from.GetType().GetProperties();
            foreach (var item in props)
            {
                if (excludeProps != null && excludeProps.Any(p => string.Compare(p, item.Name, true) == 0))
                {
                    continue;
                }
                item.SetValue(to, item.GetValue(from, null), null);
            }
        }

        #endregion
    }
}
