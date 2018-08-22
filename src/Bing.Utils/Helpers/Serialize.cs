using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml.Serialization;
using Bing.Utils.Extensions;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 序列化操作
    /// </summary>
    public static class Serialize
    {
        /// <summary>
        /// 将数据序列化为二进制数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        public static byte[] ToBinary(object data)
        {
            data.CheckNotNull(nameof(data));
            using (MemoryStream ms=new MemoryStream())
            {
                BinaryFormatter formatter=new BinaryFormatter();
                formatter.Serialize(ms, data);
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 将二进制数组反序列化为强类型数据
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="bytes">二进制数组</param>
        /// <returns></returns>
        public static T FromBinary<T>(byte[] bytes)
        {
            bytes.CheckNotNullOrEmpty(nameof(bytes));
            using (MemoryStream ms=new MemoryStream())
            {
                BinaryFormatter formatter=new BinaryFormatter();
                return (T) formatter.Deserialize(ms);
            }
        }

        /// <summary>
        /// 将数据序列化为二进制数组并写入文件中
        /// </summary>
        /// <param name="fileName">文件路径</param>
        /// <param name="data">数据</param>
        public static void ToBinaryFile(string fileName, object data)
        {
            fileName.CheckNotNull(nameof(fileName));
            data.CheckNotNull(nameof(data));
            using (FileStream fs=new FileStream(fileName,FileMode.Create))
            {
                BinaryFormatter formatter=new BinaryFormatter();
                formatter.Serialize(fs, data);
            }
        }

        /// <summary>
        /// 将指定二进制数据文件还原为强类型数据
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="fileName">文件路径</param>
        /// <returns></returns>
        public static T FromBinaryFile<T>(string fileName)
        {
            fileName.CheckFileExists(nameof(fileName));
            using (FileStream fs=new FileStream(fileName,FileMode.Open))
            {
                BinaryFormatter formatter=new BinaryFormatter();
                return (T) formatter.Deserialize(fs);
            }
        }

        /// <summary>
        /// 将数据序列化为Xml形式
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        public static string ToXml(object data)
        {
            data.CheckNotNull(nameof(data));
            using (MemoryStream ms=new MemoryStream())
            {
                XmlSerializer serializer=new XmlSerializer(data.GetType());
                serializer.Serialize(ms, data);
                ms.Seek(0, SeekOrigin.Begin);
                return Encoding.Default.GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// 将Xml序列化为强类型
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="xml">Xml字符串</param>
        /// <returns></returns>
        public static T FromXml<T>(string xml)
        {
            xml.CheckNotNull(nameof(xml));
            byte[] bytes = Encoding.Default.GetBytes(xml);
            using (MemoryStream ms=new MemoryStream())
            {
                XmlSerializer serializer=new XmlSerializer(typeof(T));
                return (T) serializer.Deserialize(ms);
            }
        }

        /// <summary>
        /// 将数据序列化为Xml并写入文件
        /// </summary>
        /// <param name="fileName">文件路径</param>
        /// <param name="data">数据</param>
        public static void ToXmlFile(string fileName, object data)
        {
            fileName.CheckNotNull(nameof(fileName));
            data.CheckNotNull(nameof(data));
            using (FileStream fs=new FileStream(fileName,FileMode.Create))
            {
                XmlSerializer serializer=new XmlSerializer(data.GetType());
                serializer.Serialize(fs,data);
            }
        }

        /// <summary>
        /// 将指定Xml数据文件还原为强类型数据
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="fileName">文件路径</param>
        /// <returns></returns>
        public static T FromXmlFile<T>(string fileName)
        {
            fileName.CheckFileExists(nameof(fileName));
            using (FileStream fs=new FileStream(fileName,FileMode.Open))
            {
                XmlSerializer serializer=new XmlSerializer(typeof(T));
                return (T) serializer.Deserialize(fs);
            }
        }
    }
}
