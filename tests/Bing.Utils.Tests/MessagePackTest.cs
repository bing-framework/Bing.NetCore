using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using Bing.Utils.IO;
using Bing.Utils.Json;
using MessagePack;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests
{
    public class MessagePackTest:TestBase
    {
        public MessagePackTest(ITestOutputHelper output) : base(output)
        {
            
        }

        [Fact]
        public void Test_JsonToBytes()
        {
            MessagePackSerializer.SetDefaultResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
            var json = FileUtil.Read("D:\\iTestRunner_R1.txt");
            var bytes = MessagePackSerializer.FromJson(json);
            FileUtil.Write("D:\\compression_result.txt", bytes);
        }

        [Fact]
        public void Test_StringFormat()
        {
            var json = FileUtil.Read("D:\\iTestRunner_R1.txt");
            var bytes = JsonUtil.ToObject(json).ToJson();
            FileUtil.Write("D:\\iTestRunner_R1_format.txt", bytes);
        }

        [Fact]
        public void Test_StringCompress()
        {
            var json = FileUtil.Read("D:\\iTestRunner_R1_format.txt");
            var bytes = CompressString(json);
            FileUtil.Write("D:\\compression_result.txt", bytes);
        }

        [Fact]
        public void Test_StringDecompress()
        {
            var json = FileUtil.Read("D:\\compression_result.txt");
            var bytes = DecompressString(json);
            FileUtil.Write("D:\\decompression_result.txt", bytes);
        }

        [Fact]
        public void Test_JsonH()
        {
            string s = "[{\"name\":\"Andrea\",age:31,\"gender\":\"Male\",\"skilled\":true},{\"name\":\"Eva\",\"age\":27,\"gender\":\"Female\",\"skilled\":true},{\"name\":\"Daniele\",\"age\":26,\"gender\":\"Male\",\"skilled\":false}]";
            List<Dictionary<string, object>> unpacked =s.ToObject<List<Dictionary<string, object>>>();
            Output.WriteLine(JSONH.pack(unpacked, 0).ToJson());
            Output.WriteLine(JSONH.pack(unpacked, 1).ToJson());
            Output.WriteLine(JSONH.pack(unpacked, 2).ToJson());
            Output.WriteLine(JSONH.pack(unpacked, 3).ToJson());
            Output.WriteLine(JSONH.pack(unpacked, 4).ToJson());
            Output.WriteLine(JSONH.pack(unpacked, 0).ToJson().Length.ToString());
            Output.WriteLine(JSONH.pack(unpacked, 1).ToJson().Length.ToString());
            Output.WriteLine(JSONH.pack(unpacked, 2).ToJson().Length.ToString());
            Output.WriteLine(JSONH.pack(unpacked, 3).ToJson().Length.ToString());
            Output.WriteLine(JSONH.pack(unpacked, 4).ToJson().Length.ToString());
            Output.WriteLine(JSONH.unpack(JSONH.pack(unpacked, 0).ToJson().ToObject<List<List<object>>>()).ToJson());
        }

        [Fact]
        public void Test_JsonH_StringCompress()
        {
            var json = FileUtil.Read("D:\\iTestRunner_R1.txt");
            List<Dictionary<string, object>> unpacked = json.ToObject<List<Dictionary<string, object>>>();
            Output.WriteLine(JSONH.pack(unpacked, 0).ToJson());
        }

        [Fact]
        public void Test_LzString_StringCompress()
        {
            var json = FileUtil.Read("D:\\iTestRunner_R1_format.txt");
            var bytes = LZString.Compress(json);
            FileUtil.Write("D:\\compression_result.txt", bytes);
        }

        /// <summary>
        /// 对字符串进行压缩
        /// </summary>
        /// <param name="str">待压缩的字符串</param>
        /// <returns>压缩后的字符串</returns>
        public static string CompressString(string str)
        {
            string compressString = "";
            byte[] compressBeforeByte = Encoding.GetEncoding("UTF-8").GetBytes(str);
            byte[] compressAfterByte = Compress(compressBeforeByte);
            compressString = Convert.ToBase64String(compressAfterByte);
            return compressString;
        }

        /// <summary>
        /// 对byte数组进行压缩
        /// </summary>
        /// <param name="data">待压缩的byte数组</param>
        /// <returns>压缩后的byte数组</returns>
        public static byte[] Compress(byte[] data)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (var zip = new BrotliStream(ms,CompressionMode.Compress,true))
                    //using (GZipStream zip = new GZipStream(ms, CompressionLevel.Optimal,true))
                    {
                        zip.Write(data, 0, data.Length);
                        byte[] buffer = new byte[ms.Length];
                        ms.Position = 0;
                        ms.Read(buffer, 0, buffer.Length);
                        return buffer;
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        /// <summary>
        /// 对字符串进行解压缩
        /// </summary>
        /// <param name="str">待解压缩的字符串</param>
        /// <returns>解压缩后的字符串</returns>
        public static string DecompressString(string str)
        {
            string compressString = "";
            //byte[] compressBeforeByte = Encoding.GetEncoding("UTF-8").GetBytes(str);
            byte[] compressBeforeByte = Convert.FromBase64String(str);
            byte[] compressAfterByte = Decompress(compressBeforeByte);
            compressString = Encoding.GetEncoding("UTF-8").GetString(compressAfterByte);
            return compressString;
        }

        public static byte[] Decompress(byte[] data)
        {
            try
            {
                MemoryStream ms = new MemoryStream(data);
                GZipStream zip = new GZipStream(ms, CompressionMode.Decompress, true);
                MemoryStream msreader = new MemoryStream();
                byte[] buffer = new byte[0x1000];
                while (true)
                {
                    int reader = zip.Read(buffer, 0, buffer.Length);
                    if (reader <= 0)
                    {
                        break;
                    }
                    msreader.Write(buffer, 0, reader);
                }
                zip.Close();
                ms.Close();
                msreader.Position = 0;
                buffer = msreader.ToArray();
                msreader.Close();
                return buffer;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
    }


    public class JSONH
    {
        protected static List<List<List<object>>> _cache;

        static public Dictionary<string, object> unpackCreateRow(List<string> keys, List<object> values)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            for (int i = 0, len = keys.Count; i < len; i++)
                result[keys[i]] = values[i];
            return result;
        }

        static public int best(List<Dictionary<string, object>> collection)
        {            
            int j = 0;
            _cache = new List<List<List<object>>>();
            for (int i = 0, len = 0, length = 0; i < 4; i++)
            {
                _cache.Add(pack(collection, i));
                len = _cache[i].ToJson().Length;
                if (length == 0)
                    length = len;
                else if (len < length)
                {
                    length = len;
                    j = i;
                }
            }
            return j;
        }

        static public List<List<object>> pack(List<Dictionary<string, object>> collection)
        {
            return pack(collection, 0);
        }

        static public List<List<object>> pack(List<Dictionary<string, object>> collection, int compression)
        {
            List<List<object>> r = new List<List<object>>();
            if (3 < compression)
            {
                int i = best(collection);
                r = _cache[i];
                _cache.Clear();
            }
            else
            {
                List<List<object>> result = new List<List<object>>();
                List<object> header = new List<object>();
                Dictionary<string, object> first = collection[0];
                int length = collection.Count,
                    len = first.Keys.Count,
                    index;
                r.Add(header);
                foreach (string key in first.Keys)
                    header.Add(key);
                for (int i = 0; i < length; ++i)
                {
                    Dictionary<string, object> item = collection[i];
                    List<object> row = new List<object>();
                    for (int j = 0; j < len; ++j)
                        row.Add(item[(string)header[j]]);
                    r.Add(row);
                }
                index = r.Count;
                if (0 < compression)
                {
                    List<object> row = r[1];
                    for (int j = 0; j < len; ++j)
                    {
                        if (!(row[j] is int) && !(row[j] is float) && !(row[j] is double))
                        {
                            List<object> cache = new List<object>(),
                                         current = new List<object>()
                            ;
                            current.Add(header[j]);
                            current.Add(cache);
                            header.RemoveAt(j);
                            header.Insert(j, current);
                            for (int i = 1, k = 0; i < index; ++i)
                            {
                                object value = r[i][j];
                                int l = cache.IndexOf(value);
                                if (l < 0)
                                {
                                    cache.Add(value);
                                    r[i][j] = k++;
                                }
                                else
                                    r[i][j] = l;
                            }
                        }
                    }
                }
                if (2 < compression)
                {
                    for (int j = 0; j < len; ++j)
                    {
                        if (header[j] is List<object>)
                        {
                            List<object> values = new List<object>();
                            List<object> indexes = new List<object>();
                            List<object> cache = (List<object>)header[j];
                            string key = (string)cache[0];
                            cache = (List<object>)cache[1];
                            for (int i = 1; i < index; ++i)
                            {
                                object value = r[i][j];
                                indexes.Add(value);
                                values.Add(cache[(int)value]);
                            }
                            indexes.AddRange(cache);
                            if (values.ToJson().Length < indexes.ToJson().Length)
                            {
                                for (int k = 0, i = 1; i < index; ++i)
                                {
                                    r[i][j] = values[k];
                                    ++k;
                                }
                                header[j] = key;
                            }
                        }
                    }
                }
                else if (1 < compression)
                {
                    length -= (int)Math.Floor((double)(length / 2));
                    for (int j = 0; j < len; ++j)
                    {
                        if (header[j] is List<object>)
                        {
                            List<object> cache = (List<object>)header[j];
                            string key = (string)cache[0];
                            cache = (List<object>)cache[1];
                            if (length < cache.Count)
                            {
                                for (int i = 1; i < index; ++i)
                                {
                                    object value = r[i][j];
                                    r[i][j] = cache[(int)value];
                                }
                                header[j] = key;
                            }
                        }
                    }
                }
                if (0 < compression)
                {
                    for (int j = 0; j < len; ++j)
                    {
                        if (header[j] is List<object>)
                        {
                            List<object> cache = (List<object>)header[j];
                            string key = (string)cache[0];
                            header[j] = key;
                            header.Insert(j + 1, cache[1]);
                            ++len;
                            ++j;
                        }
                    }
                }
            }
            return r;
        }

        static public List<Dictionary<string, object>> unpack(List<List<object>> collection)
        {
            int length = collection.Count;
            List<object> header = collection[0];
            List<string> keys = new List<string>();
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            for (int i = 0, k = 0, l = 0, len = header.Count; i < len; ++i)
            {
                keys.Add((string)header[i]);
                k = i + 1;
                if (k < len && header[k] is object[])
                {
                    ++i;
                    for (int j = 1; j < length; ++j)
                    {
                        List<object> row = collection[j];
                        object[] head = (object[])header[k];
                        row[l] = head[(int)row[l]];
                    }
                }
                ++l;
            }
            for (int j = 1; j < length; ++j)
                result.Add(unpackCreateRow(keys, collection[j]));
            return result;
        }
    }


    /// <summary>
    /// Converted from lz-string 1.4.4
    /// https://github.com/pieroxy/lz-string/blob/c58a22021000ac2d99377cc0bf9ac193a12563c5/libs/lz-string.js
    /// </summary>
    public class LZString
    {
        private const string KeyStrBase64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
        private const string KeyStrUriSafe = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+-$";
        private static readonly IDictionary<char, char> KeyStrBase64Dict = CreateBaseDict(KeyStrBase64);
        private static readonly IDictionary<char, char> KeyStrUriSafeDict = CreateBaseDict(KeyStrUriSafe);

        private static IDictionary<char, char> CreateBaseDict(string alphabet)
        {
            var dict = new Dictionary<char, char>();
            for (var i = 0; i < alphabet.Length; i++)
            {
                dict[alphabet[i]] = (char)i;
            }
            return dict;
        }

        public static string CompressToBase64(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var res = Compress(input, 6, code => KeyStrBase64[code]);
            switch (res.Length % 4)
            {
                default: throw new InvalidOperationException("When could this happen ?");
                case 0: return res;
                case 1: return res + "===";
                case 2: return res + "==";
                case 3: return res + "=";
            }
        }

        public static string DecompressFromBase64(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            return Decompress(input.Length, 32, index => KeyStrBase64Dict[input[index]]);
        }

        public static string CompressToUTF16(string input)
        {
            return Compress(input, 15, code => (char)(code + 32));
        }

        public static string DecompressFromUTF16(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            return Decompress(input.Length, 16384, index => (char)(input[index] - 32));
        }

        public static string CompressToEncodedURIComponent(string input)
        {
            if (input == null) return "";

            return Compress(input, 6, code => KeyStrUriSafe[code]);
        }

        public static string DecompressFromEncodedURIComponent(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            input = input.Replace(" ", "+");
            return Decompress(input.Length, 32, index => KeyStrUriSafeDict[input[index]]);
        }

        public static string Compress(string uncompressed)
        {
            return Compress(uncompressed, 16, code => (char)code);
        }

        private static string Compress(string uncompressed, int bitsPerChar, Func<int, char> getCharFromInt)
        {
            if (uncompressed == null) throw new ArgumentNullException(nameof(uncompressed));

            int i, value;
            var context_dictionary = new Dictionary<string, int>();
            var context_dictionaryToCreate = new Dictionary<string, bool>();
            var context_wc = "";
            var context_w = "";
            var context_enlargeIn = 2; // Compensate for the first entry which should not count
            var context_dictSize = 3;
            var context_numBits = 2;
            var context_data = new StringBuilder();
            var context_data_val = 0;
            var context_data_position = 0;

            foreach (var context_c in uncompressed)
            {
                if (!context_dictionary.ContainsKey(context_c.ToString()))
                {
                    context_dictionary[context_c.ToString()] = context_dictSize++;
                    context_dictionaryToCreate[context_c.ToString()] = true;
                }

                context_wc = context_w + context_c;
                if (context_dictionary.ContainsKey(context_wc))
                {
                    context_w = context_wc;
                }
                else
                {
                    if (context_dictionaryToCreate.ContainsKey(context_w))
                    {
                        if (context_w.FirstOrDefault() < 256)
                        {
                            for (i = 0; i < context_numBits; i++)
                            {
                                context_data_val = (context_data_val << 1);
                                if (context_data_position == bitsPerChar - 1)
                                {
                                    context_data_position = 0;
                                    context_data.Append(getCharFromInt(context_data_val));
                                    context_data_val = 0;
                                }
                                else
                                {
                                    context_data_position++;
                                }
                            }
                            value = context_w.FirstOrDefault();
                            for (i = 0; i < 8; i++)
                            {
                                context_data_val = (context_data_val << 1) | (value & 1);
                                if (context_data_position == bitsPerChar - 1)
                                {
                                    context_data_position = 0;
                                    context_data.Append(getCharFromInt(context_data_val));
                                    context_data_val = 0;
                                }
                                else
                                {
                                    context_data_position++;
                                }
                                value = value >> 1;
                            }
                        }
                        else
                        {
                            value = 1;
                            for (i = 0; i < context_numBits; i++)
                            {
                                context_data_val = (context_data_val << 1) | value;
                                if (context_data_position == bitsPerChar - 1)
                                {
                                    context_data_position = 0;
                                    context_data.Append(getCharFromInt(context_data_val));
                                    context_data_val = 0;
                                }
                                else
                                {
                                    context_data_position++;
                                }
                                value = 0;
                            }
                            value = context_w.FirstOrDefault();
                            for (i = 0; i < 16; i++)
                            {
                                context_data_val = (context_data_val << 1) | (value & 1);
                                if (context_data_position == bitsPerChar - 1)
                                {
                                    context_data_position = 0;
                                    context_data.Append(getCharFromInt(context_data_val));
                                    context_data_val = 0;
                                }
                                else
                                {
                                    context_data_position++;
                                }
                                value = value >> 1;
                            }
                        }
                        context_enlargeIn--;
                        if (context_enlargeIn == 0)
                        {
                            context_enlargeIn = (int)Math.Pow(2, context_numBits);
                            context_numBits++;
                        }
                        context_dictionaryToCreate.Remove(context_w);
                    }
                    else
                    {
                        value = context_dictionary[context_w];
                        for (i = 0; i < context_numBits; i++)
                        {
                            context_data_val = (context_data_val << 1) | (value & 1);
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                            value = value >> 1;
                        }


                    }
                    context_enlargeIn--;
                    if (context_enlargeIn == 0)
                    {
                        context_enlargeIn = (int)Math.Pow(2, context_numBits);
                        context_numBits++;
                    }
                    // Add wc to the dictionary.
                    context_dictionary[context_wc] = context_dictSize++;
                    context_w = context_c.ToString();
                }
            }

            // Output the code for w.
            if (context_w != "")
            {
                if (context_dictionaryToCreate.ContainsKey(context_w))
                {
                    if (context_w.FirstOrDefault() < 256)
                    {
                        for (i = 0; i < context_numBits; i++)
                        {
                            context_data_val = (context_data_val << 1);
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                        }
                        value = context_w.FirstOrDefault();
                        for (i = 0; i < 8; i++)
                        {
                            context_data_val = (context_data_val << 1) | (value & 1);
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                            value = value >> 1;
                        }
                    }
                    else
                    {
                        value = 1;
                        for (i = 0; i < context_numBits; i++)
                        {
                            context_data_val = (context_data_val << 1) | value;
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                            value = 0;
                        }
                        value = context_w.FirstOrDefault();
                        for (i = 0; i < 16; i++)
                        {
                            context_data_val = (context_data_val << 1) | (value & 1);
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                            value = value >> 1;
                        }
                    }
                    context_enlargeIn--;
                    if (context_enlargeIn == 0)
                    {
                        context_enlargeIn = (int)Math.Pow(2, context_numBits);
                        context_numBits++;
                    }
                    context_dictionaryToCreate.Remove(context_w);
                }
                else
                {
                    value = context_dictionary[context_w];
                    for (i = 0; i < context_numBits; i++)
                    {
                        context_data_val = (context_data_val << 1) | (value & 1);
                        if (context_data_position == bitsPerChar - 1)
                        {
                            context_data_position = 0;
                            context_data.Append(getCharFromInt(context_data_val));
                            context_data_val = 0;
                        }
                        else
                        {
                            context_data_position++;
                        }
                        value = value >> 1;
                    }


                }
                context_enlargeIn--;
                if (context_enlargeIn == 0)
                {
                    context_enlargeIn = (int)Math.Pow(2, context_numBits);
                    context_numBits++;
                }
            }

            // Mark the end of the stream
            value = 2;
            for (i = 0; i < context_numBits; i++)
            {
                context_data_val = (context_data_val << 1) | (value & 1);
                if (context_data_position == bitsPerChar - 1)
                {
                    context_data_position = 0;
                    context_data.Append(getCharFromInt(context_data_val));
                    context_data_val = 0;
                }
                else
                {
                    context_data_position++;
                }
                value = value >> 1;
            }

            // Flush the last char
            while (true)
            {
                context_data_val = (context_data_val << 1);
                if (context_data_position == bitsPerChar - 1)
                {
                    context_data.Append(getCharFromInt(context_data_val));
                    break;
                }
                else context_data_position++;
            }
            return context_data.ToString();
        }

        public static string Decompress(string compressed)
        {
            if (compressed == null) throw new ArgumentNullException(nameof(compressed));

            //TODO: Use an enumerator
            return Decompress(compressed.Length, 32768, index => compressed[index]);
        }

        private static string Decompress(int length, int resetValue, Func<int, char> getNextValue)
        {
            var dictionary = new List<string>();
            var enlargeIn = 4;
            var numBits = 3;
            string entry;
            var result = new StringBuilder();
            int i;
            string w;
            int bits = 0, resb, maxpower, power;
            var c = '\0';

            var data_val = getNextValue(0);
            var data_position = resetValue;
            var data_index = 1;

            for (i = 0; i < 3; i += 1)
            {
                dictionary.Add(((char)i).ToString());
            }

            maxpower = (int)Math.Pow(2, 2);
            power = 1;
            while (power != maxpower)
            {
                resb = data_val & data_position;
                data_position >>= 1;
                if (data_position == 0)
                {
                    data_position = resetValue;
                    data_val = getNextValue(data_index++);
                }
                bits |= (resb > 0 ? 1 : 0) * power;
                power <<= 1;
            }

            switch (bits)
            {
                case 0:
                    bits = 0;
                    maxpower = (int)Math.Pow(2, 8);
                    power = 1;
                    while (power != maxpower)
                    {
                        resb = data_val & data_position;
                        data_position >>= 1;
                        if (data_position == 0)
                        {
                            data_position = resetValue;
                            data_val = getNextValue(data_index++);
                        }
                        bits |= (resb > 0 ? 1 : 0) * power;
                        power <<= 1;
                    }
                    c = (char)bits;
                    break;
                case 1:
                    bits = 0;
                    maxpower = (int)Math.Pow(2, 16);
                    power = 1;
                    while (power != maxpower)
                    {
                        resb = data_val & data_position;
                        data_position >>= 1;
                        if (data_position == 0)
                        {
                            data_position = resetValue;
                            data_val = getNextValue(data_index++);
                        }
                        bits |= (resb > 0 ? 1 : 0) * power;
                        power <<= 1;
                    }
                    c = (char)bits;
                    break;
                case 2:
                    return "";
            }
            w = c.ToString();
            dictionary.Add(w);
            result.Append(c);
            while (true)
            {
                if (data_index > length)
                {
                    return "";
                }

                bits = 0;
                maxpower = (int)Math.Pow(2, numBits);
                power = 1;
                while (power != maxpower)
                {
                    resb = data_val & data_position;
                    data_position >>= 1;
                    if (data_position == 0)
                    {
                        data_position = resetValue;
                        data_val = getNextValue(data_index++);
                    }
                    bits |= (resb > 0 ? 1 : 0) * power;
                    power <<= 1;
                }

                int c2;
                switch (c2 = bits)
                {
                    case (char)0:
                        bits = 0;
                        maxpower = (int)Math.Pow(2, 8);
                        power = 1;
                        while (power != maxpower)
                        {
                            resb = data_val & data_position;
                            data_position >>= 1;
                            if (data_position == 0)
                            {
                                data_position = resetValue;
                                data_val = getNextValue(data_index++);
                            }
                            bits |= (resb > 0 ? 1 : 0) * power;
                            power <<= 1;
                        }

                        c2 = dictionary.Count;
                        dictionary.Add(((char)bits).ToString());
                        enlargeIn--;
                        break;
                    case (char)1:
                        bits = 0;
                        maxpower = (int)Math.Pow(2, 16);
                        power = 1;
                        while (power != maxpower)
                        {
                            resb = data_val & data_position;
                            data_position >>= 1;
                            if (data_position == 0)
                            {
                                data_position = resetValue;
                                data_val = getNextValue(data_index++);
                            }
                            bits |= (resb > 0 ? 1 : 0) * power;
                            power <<= 1;
                        }
                        c2 = dictionary.Count;
                        dictionary.Add(((char)bits).ToString());
                        enlargeIn--;
                        break;
                    case (char)2:
                        return result.ToString();
                }

                if (enlargeIn == 0)
                {
                    enlargeIn = (int)Math.Pow(2, numBits);
                    numBits++;
                }

                if (dictionary.Count - 1 >= c2)
                {
                    entry = dictionary[c2];
                }
                else
                {
                    if (c2 == dictionary.Count)
                    {
                        entry = w + w[0];
                    }
                    else
                    {
                        return null;
                    }
                }
                result.Append(entry);

                // Add w+entry[0] to the dictionary.
                dictionary.Add(w + entry[0]);
                enlargeIn--;

                w = entry;

                if (enlargeIn == 0)
                {
                    enlargeIn = (int)Math.Pow(2, numBits);
                    numBits++;
                }
            }
        }
    }    
}
