using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Bing.Serialization.Binary
{
    /// <summary>
    /// 二进制序列化工具
    /// </summary>
    public static class BinarySerializationUtil
    {
        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="obj">对象</param>
        /// <returns></returns>
        public static byte[] Serialize(object obj)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serialize(obj, memoryStream);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="obj">对象</param>
        /// <param name="stream">流</param>
        public static void Serialize(object obj, Stream stream)
        {
            CreateBinaryFormatter().Serialize(stream, obj);
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        public static object Deserialize(byte[] bytes)
        {
            using (var memoryStream = new MemoryStream(bytes))
            {
                return Deserialize(memoryStream);
            }
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="stream">流</param>
        /// <returns></returns>
        public static object Deserialize(Stream stream)
        {
            return CreateBinaryFormatter().Deserialize(stream);
        }

        /// <summary>
        /// 反序列化。允许反序列化运行时加载的程序集定义的对象
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        public static object DeserializeExtended(byte[] bytes)
        {
            using (var memoryStream = new MemoryStream(bytes))
            {
                return DeserializeExtended(memoryStream);
            }
        }

        /// <summary>
        /// 反序列化。允许反序列化运行时加载的程序集定义的对象
        /// </summary>
        /// <param name="stream">流</param>
        /// <returns></returns>
        public static object DeserializeExtended(Stream stream)
        {
            return CreateBinaryFormatter(true).Deserialize(stream);
        }

        /// <summary>
        /// 创建二进制格式化程序
        /// </summary>
        /// <param name="extended">是否允许序列化运行时对象</param>
        /// <returns></returns>
        private static BinaryFormatter CreateBinaryFormatter(bool extended = false)
        {
            if (extended)
            {
                return new BinaryFormatter()
                {
                    // TODO: AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
                    Binder = new ExtendedSerializationBinder()
                };
            }
            return new BinaryFormatter();
        }

        /// <summary>
        /// 扩展序列化绑定器。允许序列化在运行时加载的程序集中定义的对象
        /// </summary>
        private sealed class ExtendedSerializationBinder : SerializationBinder
        {
            /// <summary>
            /// 绑定类型
            /// </summary>
            /// <param name="assemblyName">程序集名称</param>
            /// <param name="typeName">类型名称</param>
            /// <returns></returns>
            public override Type BindToType(string assemblyName, string typeName)
            {
                var toAssemblyName = assemblyName.Split(',')[0];
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Split(',')[0] == toAssemblyName)
                    {
                        return assembly.GetType(typeName);
                    }
                }
                return Type.GetType($"{typeName}, {assemblyName}");
            }
        }
    }
}
