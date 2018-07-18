using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 对象(<see cref="object"/>) 扩展
    /// </summary>
    public static class ObjectExtensions
    {
        #region DeepClone(对象深拷贝)

        /// <summary>
        /// 对象深度拷贝，复制相同数据，但指向内存位置不一样的数据
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">值</param>
        /// <returns></returns>
        public static T DeepClone<T>(this T obj) where T : class
        {
            if (obj == null)
            {
                return default(T);
            }

            if (typeof(T).HasAttribute<SerializableAttribute>(true))
            {
                throw new NotSupportedException($"当前对象未标记特性“{typeof(SerializableAttribute)}”，无法进行DeepClone操作");
            }
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, obj);
                ms.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(ms);
            }
        }

        #endregion

        #region ToDynamic(将对象转换为dynamic)

        /// <summary>
        /// 将对象[主要是匿名对象]转换为dynamic
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static dynamic ToDynamic(this object value)
        {
            IDictionary<string,object> expando=new ExpandoObject();
            Type type = value.GetType();
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(type);
            foreach (PropertyDescriptor property in properties)
            {
                var val = property.GetValue(value);
                if (property.PropertyType.FullName != null &&
                    property.PropertyType.FullName.StartsWith("<>f__AnonymousType"))
                {
                    dynamic dval = val.ToDynamic();
                    expando.Add(property.Name,dval);
                }
                else
                {
                    expando.Add(property.Name, val);
                }
            }

            return (ExpandoObject) expando;
        }

        #endregion
    }
}
