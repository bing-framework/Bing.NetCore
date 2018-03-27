using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Bing.Utils.IdGenerators.Abstractions;

namespace Bing.Utils.IdGenerators.Core
{
    /// <summary>
    /// 有序Guid 生成器
    /// 代码出自：https://github.com/jhtodd/SequentialGuid/blob/master/SequentialGuid/Classes/SequentialGuid.cs
    /// </summary>
    public class SequentialGuidGenerator : IGuidGenerator
    {
        /// <summary>
        /// 数据库类型
        /// </summary>
        public SequentialGuidDatabaseType DatabaseType { get; set; }

        /// <summary>
        /// 获取<see cref="SequentialGuidGenerator"/>类型的实例
        /// </summary>
        public static SequentialGuidGenerator Current { get; } = new SequentialGuidGenerator();

        /// <summary>
        /// 随机数生成器
        /// </summary>
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        /// <summary>
        /// 初始化一个<see cref="SequentialGuidGenerator"/>类型的实例
        /// </summary>
        private SequentialGuidGenerator()
        {
            DatabaseType = SequentialGuidDatabaseType.SqlServer;
        }

        /// <summary>
        /// 创建有序的 Guid
        /// </summary>
        /// <returns></returns>
        public Guid Create()
        {
            return Create(DatabaseType);
        }

        /// <summary>
        /// 创建有序的 Guid
        /// </summary>
        /// <param name="databaseType">数据库类型</param>
        /// <returns></returns>
        public Guid Create(SequentialGuidDatabaseType databaseType)
        {
            switch (databaseType)
            {
                case SequentialGuidDatabaseType.SqlServer:
                    return Create(SequentialGuidType.SequentialAtEnd);
                case SequentialGuidDatabaseType.Oracle:
                    return Create(SequentialGuidType.SequentialAsBinary);
                case SequentialGuidDatabaseType.MySql:
                    return Create(SequentialGuidType.SequentialAsString);
                case SequentialGuidDatabaseType.PostgreSql:
                    return Create(SequentialGuidType.SequentialAsString);
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// 创建有序的 Guid
        /// </summary>
        /// <param name="guidType">有序 Guid 类型</param>
        /// <returns></returns>
        public Guid Create(SequentialGuidType guidType)
        {
            // We start with 16 bytes of cryptographically strong random data.
            var randomBytes = new byte[10];
            lock (Rng)
            {
                Rng.GetBytes(randomBytes);
            }

            // An alternate method: use a normally-created GUID to get our initial
            // random data:
            // byte[] randomBytes = Guid.NewGuid().ToByteArray();
            // This is faster than using RNGCryptoServiceProvider, but I don't
            // recommend it because the .NET Framework makes no guarantee of the
            // randomness of GUID data, and future versions (or different
            // implementations like Mono) might use a different method.

            // Now we have the random basis for our GUID.  Next, we need to
            // create the six-byte block which will be our timestamp.

            // We start with the number of milliseconds that have elapsed since
            // DateTime.MinValue.  This will form the timestamp.  There's no use
            // being more specific than milliseconds, since DateTime.Now has
            // limited resolution.

            // Using millisecond resolution for our 48-bit timestamp gives us
            // about 5900 years before the timestamp overflows and cycles.
            // Hopefully this should be sufficient for most purposes. :)
            long timestamp = DateTime.UtcNow.Ticks / 10000L;

            // Then get the bytes
            byte[] timestampBytes = BitConverter.GetBytes(timestamp);

            // Since we're converting from an Int64, we have to reverse on
            // little-endian systems.
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(timestampBytes);
            }

            byte[] guidBytes = new byte[16];

            switch (guidType)
            {
                case SequentialGuidType.SequentialAsString:
                case SequentialGuidType.SequentialAsBinary:

                    // For string and byte-array version, we copy the timestamp first, followed
                    // by the random data.
                    Buffer.BlockCopy(timestampBytes, 2, guidBytes, 0, 6);
                    Buffer.BlockCopy(randomBytes, 0, guidBytes, 6, 10);

                    // If formatting as a string, we have to compensate for the fact
                    // that .NET regards the Data1 and Data2 block as an Int32 and an Int16,
                    // respectively.  That means that it switches the order on little-endian
                    // systems.  So again, we have to reverse.
                    if (guidType == SequentialGuidType.SequentialAsString && BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(guidBytes, 0, 4);
                        Array.Reverse(guidBytes, 4, 2);
                    }

                    break;

                case SequentialGuidType.SequentialAtEnd:

                    // For sequential-at-the-end versions, we copy the random data first,
                    // followed by the timestamp.
                    Buffer.BlockCopy(randomBytes, 0, guidBytes, 0, 10);
                    Buffer.BlockCopy(timestampBytes, 2, guidBytes, 10, 6);
                    break;
            }

            return new Guid(guidBytes);
        }
    }

    /// <summary>
    /// 有序Guid数据库类型
    /// </summary>
    public enum SequentialGuidDatabaseType
    {
        /// <summary>
        /// SqlServer
        /// </summary>
        SqlServer,
        /// <summary>
        /// Oracle
        /// </summary>
        Oracle,
        /// <summary>
        /// MySql
        /// </summary>
        MySql,
        /// <summary>
        /// PostgreSql
        /// </summary>
        PostgreSql
    }

    /// <summary>
    /// 有序Guid类型
    /// </summary>
    public enum SequentialGuidType
    {
        /// <summary>
        /// 生成的GUID 按照字符串顺序排列
        /// </summary>
        SequentialAsString,
        /// <summary>
        /// 生成的GUID 按照二进制的顺序排列
        /// </summary>
        SequentialAsBinary,
        /// <summary>
        /// 生成的GUID 像SQL Server, 按照末尾部分排列
        /// </summary>
        SequentialAtEnd
    }
}
