using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Bing.Payments.Attributes;
using Bing.Payments.Utils;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace Bing.Payments.Gateways
{
    /// <summary>
    /// 网关数据
    /// </summary>
    public class GatewayData
    {
        #region 属性

        /// <summary>
        /// 字典
        /// </summary>
        private readonly SortedDictionary<string, object> _values;

        /// <summary>
        /// 获取或设置 网关数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns></returns>
        public object this[string key]
        {
            get => _values[key];
            set => _values[key] = value;
        }

        /// <summary>
        /// 键集合
        /// </summary>
        public SortedDictionary<string, object>.KeyCollection Keys => _values.Keys;

        /// <summary>
        /// 值集合
        /// </summary>
        public SortedDictionary<string, object>.ValueCollection Values => _values.Values;

        /// <summary>
        /// 获取指定索引 键值对
        /// </summary>
        /// <param name="index">索引值</param>
        /// <returns></returns>
        public KeyValuePair<string, object> this[int index] => _values.ElementAt(index);

        /// <summary>
        /// 数量
        /// </summary>
        public int Count => _values.Count;

        /// <summary>
        /// 原始值
        /// </summary>
        public string Raw { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="GatewayData"/>类型的实例
        /// </summary>
        public GatewayData()
        {
            _values=new SortedDictionary<string, object>();
        }

        /// <summary>
        /// 初始化一个<see cref="GatewayData"/>类型的实例
        /// </summary>
        /// <param name="comparer">排序策略</param>
        public GatewayData(IComparer<string> comparer)
        {
            _values = new SortedDictionary<string, object>(comparer);
        }

        #endregion

        #region Add(添加参数)

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="key">参数名</param>
        /// <param name="value">参数值</param>
        /// <returns></returns>
        public bool Add(string key, object value)
        {
            Raw = null;
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key),"参数名不能为空");
            }

            if (value is null || string.IsNullOrEmpty(value.ToString()))
            {
                return false;
            }

            if (Exists(key))
            {
                _values[key] = value;
            }
            else
            {
                _values.Add(key,value);
            }

            return true;
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="obj">对象</param>
        /// <param name="stringCase">字符串策略</param>
        /// <returns></returns>
        public bool Add(object obj, StringCase stringCase)
        {
            ValidateUtil.Validate(obj,null);

            Raw = null;
            var type = obj.GetType();
            var properties = type.GetProperties();
            var fields = type.GetFields();

            Add(properties);
            Add(fields);
            return true;

            void Add(MemberInfo[] info)
            {
                foreach (var item in info)
                {
                    var ignoreAttributes = item.GetCustomAttributes(typeof(IgnoreAttribute), true);
                    if (ignoreAttributes.Length > 0)
                    {
                        continue;
                    }
                    string key;
                    object value;
                    var renameAttribute = item.GetCustomAttributes(typeof(RenameAttribute), true);
                    if (renameAttribute.Length > 0)
                    {
                        key = ((RenameAttribute) renameAttribute[0]).Name;
                    }
                    else
                    {
                        if (stringCase is StringCase.Camel)
                        {
                            key = item.Name.ToCamelCase();
                        }
                        else if (stringCase is StringCase.Snake)
                        {
                            key = item.Name.ToSnakeCase();
                        }
                        else
                        {
                            key = item.Name;
                        }
                    }

                    switch (item.MemberType)
                    {
                        case MemberTypes.Field:
                            value = ((FieldInfo) item).GetValue(obj);
                            break;
                        case MemberTypes.Property:
                            value = ((PropertyInfo) item).GetValue(obj);
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    if (value is null || string.IsNullOrEmpty(value.ToString()))
                    {
                        continue;
                    }

                    if (Exists(key))
                    {
                        _values[key] = value;
                    }
                    else
                    {
                        _values.Add(key, value);
                    }
                }
            }
        }

        #endregion

        #region GetValue(获取参数值)

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public object GetValue(string key)
        {
            _values.TryGetValue(key, out var value);
            return value;
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public string GetStringValue(string key)
        {
            return GetValue(key).SafeString();
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public double GetDoubleValue(string key)
        {
            double.TryParse(GetStringValue(key), out var value);
            return value;
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public int GetIntValue(string key)
        {
            int.TryParse(GetStringValue(key), out var value);
            return value;
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public DateTime GetDateTimeValue(string key)
        {
            DateTime.TryParse(GetStringValue(key), out var value);
            return value;
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public float GetFloatValue(string key)
        {
            float.TryParse(GetStringValue(key), out var value);
            return value;
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public decimal GetDecimalValue(string key)
        {
            decimal.TryParse(GetStringValue(key), out var value);
            return value;
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public byte GetByteValue(string key)
        {
            byte.TryParse(GetStringValue(key), out var value);
            return value;
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public char GetCharValue(string key)
        {
            char.TryParse(GetStringValue(key), out var value);
            return value;
        }

        /// <summary>
        /// 根据参数名获取参数值
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public bool GetBoolValue(string key)
        {
            bool.TryParse(GetStringValue(key), out var value);
            return value;
        }

        #endregion

        #region Exists(是否存在指定参数名)

        /// <summary>
        /// 是否存在指定参数名
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public bool Exists(string key) => _values.ContainsKey(key);

        #endregion

        #region ToXml(将网关数据转换成Xml格式数据)

        /// <summary>
        /// 将网关数据转换成Xml格式数据
        /// </summary>
        /// <returns></returns>
        public string ToXml()
        {
            if (_values.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append("<xml>");
            foreach (var item in _values)
            {
                sb.AppendFormat(item.Value is string ? "<{0}><![CDATA[{1}]]></{0}>" : "<{0}>{1}</{0}>", item.Key,item.Value);
            }
            sb.Append("</xml>");

            return sb.ToString();
        }

        #endregion

        #region FromXml(将Xml格式数据转换为网关数据)

        /// <summary>
        /// 将Xml格式数据转换为网关数据
        /// </summary>
        /// <param name="xml">Xml数据</param>
        public void FromXml(string xml)
        {
            try
            {
                Clear();
                if (!string.IsNullOrEmpty(xml))
                {
                    var xmlDoc = new XmlDocument()
                    {
                        XmlResolver = null
                    };
                    xmlDoc.LoadXml(xml);
                    var xmlElement = xmlDoc.DocumentElement;
                    var nodes = xmlElement.ChildNodes;
                    foreach (var item in nodes)
                    {
                        var xe = (XmlElement) item;
                        Add(xe.Name, xe.InnerText);
                    }
                }
            }
            finally
            {
                Raw = xml;
            }
        }

        #endregion

        #region ToUrl(将网关数据转换为Url格式数据)

        /// <summary>
        /// 将网关数据转换为Url格式数据
        /// </summary>
        /// <param name="isUrlEncode">是否需要url编码</param>
        /// <returns></returns>
        public string ToUrl(bool isUrlEncode = true)
        {
            return string.Join("&",
                _values.Select(x =>
                    $"{x.Key}={(isUrlEncode ? WebUtility.UrlEncode(x.Value.ToString()) : x.Value.ToString())}"));
        }

        #endregion

        #region FromUrl(将Url格式数据转换为网关数据)

        /// <summary>
        /// 将Url格式数据转换为网关数据
        /// </summary>
        /// <param name="url">url数据</param>
        /// <param name="isUrlDecode">是否需要url解码</param>
        public void FromUrl(string url, bool isUrlDecode = true)
        {
            try
            {
                Clear();
                if (!string.IsNullOrEmpty(url))
                {
                    int index = url.IndexOf('?');
                    if (index == 0)
                    {
                        url = url.Substring(index + 1);
                    }
                    var regex = new Regex(@"(^|&)?(\w+)=([^&]+)(&|$)?", RegexOptions.Compiled);
                    var mc = regex.Matches(url);

                    foreach (Match item in mc)
                    {
                        string value = item.Result("$3");
                        Add(item.Result("$2"), isUrlDecode ? WebUtility.UrlDecode(value) : value);
                    }
                }
            }
            finally
            {
                Raw = url;
            }
        }

        #endregion

        #region FromForm(将表单数据转换为网关数据)

        /// <summary>
        /// 将表单数据转换为网关数据
        /// </summary>
        /// <param name="form">表单</param>
        public void FromForm(IFormCollection form)
        {
            try
            {
                Clear();
                var allKeys = form.Keys;
                foreach (var item in allKeys)
                {
                    Add(item, form[item]);
                }
            }
            catch { }
        }

        #endregion

        #region FromNameValueCollection(将键值对转换为网关数据)

        /// <summary>
        /// 将键值对转换为网关数据
        /// </summary>
        /// <param name="nvc">键值对</param>
        public void FromNameValueCollection(NameValueCollection nvc)
        {
            foreach (var item in nvc.AllKeys)
            {
                Add(item, nvc[item]);
            }
        }

        #endregion

        #region ToForm(将网关数据转换为表单数据)

        /// <summary>
        /// 将网关数据转换为表单数据
        /// </summary>
        /// <param name="url">请求地址</param>
        /// <returns></returns>
        public string ToForm(string url)
        {
            var html = new StringBuilder();
            html.AppendLine("<body>");
            html.AppendLine($"<form name='gateway' method='post' action ='{url}'>");
            foreach (var item in _values)
            {
                html.AppendLine($"<input type='hidden' name='{item.Key}' value='{item.Value}'>");
            }
            html.AppendLine("</form>");
            html.AppendLine("<script language='javascript' type='text/javascript'>");
            html.AppendLine("document.gateway.submit();");
            html.AppendLine("</script>");
            html.AppendLine("</body>");

            return html.ToString();
        }

        #endregion

        #region ToJson(将网关数据转换为Json格式数据)

        /// <summary>
        /// 将网关数据转换为Json格式数据
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(_values);
        }

        #endregion

        #region FromJson(将Json格式数据转换为网关数据)

        /// <summary>
        /// 将Json格式数据转换为网关数据
        /// </summary>
        /// <param name="json">json数据</param>
        public void FromJson(string json)
        {
            try
            {
                Clear();
                if (!string.IsNullOrEmpty(json))
                {
                    var jObject = JObject.Parse(json);
                    foreach (var item in jObject)
                    {
                        if (item.Value.Type == JTokenType.Object)
                        {
                            Add(item.Key, item.Value.ToString(Formatting.None));
                        }
                        else
                        {
                            Add(item.Key, item.Value.ToString());
                        }
                    }
                }
            }
            finally
            {
                Raw = json;
            }
        }

        #endregion

        #region ToObject(将网关参数转换为对象)

        /// <summary>
        /// 将网关参数转换为对象
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="stringCase">字符串策略</param>
        /// <returns></returns>
        public T ToObject<T>(StringCase stringCase)
        {
            var type = typeof(T);
            var obj = Activator.CreateInstance(type);
            var properties = type.GetProperties();
            foreach (var item in properties)
            {
                var renameAttribute = item.GetCustomAttributes(typeof(RenameAttribute), true);
                string key;
                if (renameAttribute.Length > 0)
                {
                    key = ((RenameAttribute) renameAttribute[0]).Name;
                }
                else
                {
                    if (stringCase is StringCase.Camel)
                    {
                        key = item.Name.ToCamelCase();
                    }
                    else if(stringCase is StringCase.Snake)
                    {
                        key = item.Name.ToSnakeCase();
                    }
                    else
                    {
                        key = item.Name;
                    }
                }

                var value = GetStringValue(key);
                if (value != null)
                {
                    item.SetValue(obj, Convert.ChangeType(value, item.PropertyType));
                }
            }

            return (T)obj;
        }

        /// <summary>
        /// 将网关参数转换为对象
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="stringCase">字符串策略</param>
        /// <returns></returns>
        public async Task<T> ToObjectAsync<T>(StringCase stringCase)
        {
            return await Task.Run(() => ToObject<T>(stringCase));
        }

        #endregion

        #region Clear(清空网关数据)

        /// <summary>
        /// 清空网关数据
        /// </summary>
        public void Clear()
        {
            _values.Clear();
        }

        #endregion

        #region Remove(移除指定参数)

        /// <summary>
        /// 移除指定参数
        /// </summary>
        /// <param name="key">参数名</param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            return _values.Remove(key);
        }

        #endregion
    }
}
