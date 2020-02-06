using System;

namespace Bing.Utils.IO
{
    /// <summary>
    /// 字节缓冲区
    /// </summary>
    public class ByteBuffer
    {
        /// <summary>
        /// 字节缓存区
        /// </summary>
        private byte[] _buffer;

        /// <summary>
        /// 读取索引
        /// </summary>
        private int _readIndex = 0;

        /// <summary>
        /// 写入索引
        /// </summary>
        private int _writeIndex = 0;

        /// <summary>
        /// 读取索引标记
        /// </summary>
        private int _markReadIndex = 0;

        /// <summary>
        /// 写入索引标记
        /// </summary>
        private int _markWriteIndex = 0;

        /// <summary>
        /// 缓存区字节数组长度
        /// </summary>
        private int _capacity;

        /// <summary>
        /// 初始化一个<see cref="ByteBuffer"/>类型的实例
        /// </summary>
        /// <param name="capacity">容量</param>
        private ByteBuffer(int capacity)
        {
            _buffer = new byte[capacity];
            this._capacity = capacity;
        }

        /// <summary>
        /// 初始化一个<see cref="ByteBuffer"/>类型的实例
        /// </summary>
        /// <param name="bytes">字节数组</param>
        private ByteBuffer(byte[] bytes)
        {
            _buffer = bytes;
            this._capacity = bytes.Length;
        }

        /// <summary>
        /// 构建一个指定长度的字节缓冲区对象
        /// </summary>
        /// <param name="capacity">容量</param>
        public static ByteBuffer Allocate(int capacity) => new ByteBuffer(capacity);

        /// <summary>
        /// 构建一个指定字节数组的字节缓冲区对象
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static ByteBuffer Allocate(byte[] bytes) => new ByteBuffer(bytes);

        /// <summary>
        /// 修正长度。
        /// 根据length的长度，确定大于此length的最近的2次方数，如length=7，则返回值为8
        /// </summary>
        /// <param name="length">长度</param>
        private int FixLength(int length)
        {
            var n = 2;
            var b = 2;
            while (b < length)
            {
                b = 2 << n;
                n++;
            }

            return b;
        }

        /// <summary>
        /// 翻转字节数组。如果本地字节序列为低字节序列，则进行翻转以转换为高字节序列（大小端）
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="isLittleEndian">是否小端</param>
        private byte[] Flip(byte[] bytes, bool isLittleEndian = false)
        {
            if (BitConverter.IsLittleEndian)
            {
                // 系统为小端，这时候需要的是大段则转换
                if (!isLittleEndian)
                    Array.Reverse(bytes);
            }
            else
            {
                // 系统为大端，这时候需要的是小端则转换
                if (isLittleEndian)
                    Array.Reverse(bytes);
            }

            return bytes;
        }

        /// <summary>
        /// 确定内部字节缓存数组的大小
        /// </summary>
        /// <param name="currLen">当前长度</param>
        /// <param name="futureLen">将来长度</param>
        /// <returns></returns>
        private int FixSizeAndReset(int currLen, int futureLen)
        {
            if (futureLen > currLen)
            {
                // 以原大小的2次方的两倍确定内部字节缓存区大小
                var size = FixLength(currLen) * 2;
                if (futureLen > size)
                {
                    // 以将来的大小的2次方的两倍确定内部字节缓存区大小
                    size = FixLength(futureLen) * 2;
                }

                byte[] newBuffer = new byte[size];
                Array.Copy(_buffer, 0, newBuffer, 0, currLen);
                _buffer = newBuffer;
                _capacity = newBuffer.Length;
            }

            return futureLen;
        }

        /// <summary>
        /// 写入字节数组。将bytes字节数组从指定起始索引到指定长度的元素写入到缓冲区
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="startIndex">起始索引</param>
        /// <param name="length">写入长度</param>
        public void WriteBytes(byte[] bytes, int startIndex, int length)
        {
            lock (this)
            {
                var offset = length - startIndex;
                if (offset <= 0)
                    return;
                var total = offset + _writeIndex;
                var len = _buffer.Length;
                FixSizeAndReset(len, total);
                for (int i = _writeIndex, j = startIndex; i < total; i++, j++)
                {
                    _buffer[i] = bytes[j];
                }

                _writeIndex = total;
            }
        }

        /// <summary>
        /// 写入字节数组。将bytes字节数组从0到指定长度的元素写入缓冲区
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="length">写入长度</param>
        public void WriteBytes(byte[] bytes, int length)
        {
        }
    }
}
