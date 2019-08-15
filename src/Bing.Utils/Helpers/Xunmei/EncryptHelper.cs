using System;
using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 加密类.
    /// </summary>
    public class EncryptHelper
    {
        /// <summary>
        /// 生成加密密码
        /// </summary>
        /// <param name="password">密码</param>
        /// <param name="salt">加密盐</param>
        /// <returns>加密以后的密码字符串，如果密码或盐为空或者0长度这返回空字符串。</returns>
        public static string EncodePassword(string password, string salt)
        {
            if (string.IsNullOrEmpty(salt))
            {
                return EncryptMD5(password);
            }

            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(salt))
                return string.Empty;

            byte[] passwordBuffer = Encoding.Unicode.GetBytes(password);
            byte[] saltBuffer = Convert.FromBase64String(salt);
            byte[] encodeData = new byte[saltBuffer.Length + passwordBuffer.Length];

            Buffer.BlockCopy(saltBuffer, 0, encodeData, 0, saltBuffer.Length);
            Buffer.BlockCopy(passwordBuffer, 0, encodeData, saltBuffer.Length, passwordBuffer.Length);
            using (var coder = HashAlgorithm.Create("SHA"))
            {
                if (coder != null)
                {
                    byte[] encodePassword = coder.ComputeHash(encodeData);
                    string result = Convert.ToBase64String(encodePassword);
                    return result;
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取一个新的盐
        /// </summary>
        /// <returns>System.string.</returns>
        public static string GetSalt()
        {
            byte[] saltBuffer = BitConverter.GetBytes(DateTime.Now.Ticks);
            return Convert.ToBase64String(saltBuffer);
        }

        #region Base64

        /// <summary>
        /// Base64加密
        /// </summary>
        /// <param name="codeType">Type of the code.</param>
        /// <param name="code">The code.</param>
        /// <returns>System.string.</returns>
        public static string EncodeBase64(string codeType, string code)
        {
            string encode;
            byte[] bytes = Encoding.GetEncoding(codeType).GetBytes(code);
            try
            {
                encode = Convert.ToBase64String(bytes);
            }
            catch
            {
                encode = code;
            }
            return encode;
        }

        /// <summary>
        /// Base64加密
        /// </summary>
        /// <param name="code">The code.</param>
        /// <returns>System.string.</returns>
        public static string EncodeBase64(string code)
        {
            return EncodeBase64("UTF-8", code);
        }

        /// <summary>
        /// Base64解码
        /// </summary>
        /// <param name="codeType">Type of the code.</param>
        /// <param name="code">The code.</param>
        /// <returns>System.string.</returns>
        public static string DecodeBase64(string codeType, string code)
        {
            string decode;
            byte[] bytes = Convert.FromBase64String(code);
            try
            {
                decode = Encoding.GetEncoding(codeType).GetString(bytes);
            }
            catch
            {
                decode = code;
            }
            return decode;
        }

        /// <summary>
        /// Base64解码
        /// </summary>
        /// <param name="code">The code.</param>
        /// <returns>System.string.</returns>
        public static string DecodeBase64(string code)
        {
            return DecodeBase64("UTF-8", code);
        }

        #endregion

        #region DES

        /// <summary>
        /// DES加密
        /// </summary>
        /// <param name="plainStr">加密字符串(明文)</param>
        /// <param name="key">密钥</param>
        /// <param name="iv">初始化向量</param>
        /// <returns>System.string.</returns>
        public static string EncryptDES(string plainStr, string key, string iv)
        {
            using (var des = new DESCryptoServiceProvider())
            {
                byte[] inputByteArray = Encoding.Default.GetBytes(plainStr);
                des.Key = Encoding.Default.GetBytes(key);
                des.IV = Encoding.Default.GetBytes(iv);

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        try
                        {
                            cs.Write(inputByteArray, 0, inputByteArray.Length);
                            cs.FlushFinalBlock();
                            return Convert.ToBase64String(ms.ToArray());
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                        return string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// DES解密
        /// </summary>
        /// <param name="encryptStr">解密字符串(密文)</param>
        /// <param name="key">密钥</param>
        /// <param name="iv">初始化向量</param>
        /// <returns>System.string.</returns>
        public static string DecryptDES(string encryptStr, string key, string iv)
        {
            byte[] inputByteArray = Convert.FromBase64String(encryptStr);

            using (var des = new DESCryptoServiceProvider())
            {
                des.Key = Encoding.Default.GetBytes(key);
                des.IV = Encoding.Default.GetBytes(iv);
                using (var mStream = new MemoryStream())
                {
                    using (var cStream = new CryptoStream(mStream, des.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cStream.Write(inputByteArray, 0, inputByteArray.Length);
                        cStream.FlushFinalBlock();
                        return Encoding.Default.GetString(mStream.ToArray());
                    }
                }
            }
        }

        #endregion

        #region 3DES

        /// <summary>
        /// 3DES加密
        /// </summary>
        /// <param name="plainStr">明文.</param>
        /// <param name="key">密钥</param>
        /// <param name="iv">初始向量.</param>
        /// <returns>System.string.</returns>
        public static string Encrypt3DES(string plainStr, string key, string iv)
        {
            using (var des = new TripleDESCryptoServiceProvider())
            {
                des.Key = Encoding.Default.GetBytes(key);
                des.Mode = CipherMode.ECB;
                des.IV = Encoding.Default.GetBytes(iv);
                des.Padding = PaddingMode.PKCS7;

                using (var desEncrypt = des.CreateEncryptor())
                {
                    byte[] buffer = Encoding.Default.GetBytes(plainStr);
                    return Convert.ToBase64String(desEncrypt.TransformFinalBlock(buffer, 0, buffer.Length));
                }
            }
        }

        /// <summary>
        /// 3DES解密
        /// </summary>
        /// <param name="encryptStr">密文.</param>
        /// <param name="key">密钥</param>
        /// <param name="iv">初始向量.</param>
        /// <returns>明文</returns>
        public static string Decrypt3DES(string encryptStr, string key, string iv)
        {
            using (var des = new TripleDESCryptoServiceProvider())
            {
                des.Key = Encoding.Default.GetBytes(key);
                des.Mode = CipherMode.ECB;
                des.IV = Encoding.Default.GetBytes(iv);
                des.Padding = PaddingMode.PKCS7;

                using (var desDecrypt = des.CreateDecryptor())
                {
                    string result = string.Empty;
                    try
                    {
                        byte[] buffer = Convert.FromBase64String(encryptStr);
                        result = Encoding.ASCII.GetString(desDecrypt.TransformFinalBlock(buffer, 0, buffer.Length));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                    return result;
                }
            }
        }

        #endregion

        #region AES

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="plainStr">明文字符串</param>
        /// <param name="key">密钥.</param>
        /// <param name="iv">初始向量.</param>
        /// <returns>密文</returns>
        public static string EncryptAES(string plainStr, string key, string iv)
        {
            if (key.Length < 32)
                key = key.PadRight(32);
            else if (key.Length > 32)
                key = key.Substring(0, 32);

            if (iv.Length < 16)
                iv = iv.PadRight(16);
            else if (iv.Length > 16)
                iv = iv.Substring(0, 16);

            byte[] bKey = Encoding.Default.GetBytes(key);
            byte[] bIV = Encoding.Default.GetBytes(iv);
            byte[] byteArray = Encoding.Default.GetBytes(plainStr);

            string encrypt = null;
            using (var aes = Rijndael.Create())
            {
                try
                {
                    using (var mStream = new MemoryStream())
                    {
                        using (var cStream = new CryptoStream(mStream, aes.CreateEncryptor(bKey, bIV), CryptoStreamMode.Write))
                        {
                            cStream.Write(byteArray, 0, byteArray.Length);
                            cStream.FlushFinalBlock();
                            encrypt = Convert.ToBase64String(mStream.ToArray());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                return encrypt;
            }
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="encryptStr">密文字符串</param>
        /// <param name="key">密钥.</param>
        /// <param name="iv">初始向量.</param>
        /// <returns>明文</returns>
        public static string DecryptAES(string encryptStr, string key, string iv)
        {
            if (key.Length < 32)
                key = key.PadRight(32);
            else if (key.Length > 32)
                key = key.Substring(0, 32);

            if (iv.Length < 16)
                iv = iv.PadRight(16);
            else if (iv.Length > 16)
                iv = iv.Substring(0, 16);

            byte[] bKey = Encoding.Default.GetBytes(key);
            byte[] bIV = Encoding.Default.GetBytes(iv);
            byte[] byteArray = Convert.FromBase64String(encryptStr);

            string decrypt = null;
            using (var aes = Rijndael.Create())
            {
                try
                {
                    using (var mStream = new MemoryStream())
                    {
                        using (var cStream = new CryptoStream(mStream, aes.CreateDecryptor(bKey, bIV), CryptoStreamMode.Write))
                        {
                            cStream.Write(byteArray, 0, byteArray.Length);
                            cStream.FlushFinalBlock();
                            decrypt = Encoding.Default.GetString(mStream.ToArray());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                return decrypt;
            }
        }

        #endregion

        /// <summary>
        /// MD5加密
        /// </summary>
        /// <param name="strSource">字符源</param>
        /// <returns>加密字符串</returns>
        public static string EncryptMD5(string strSource)
        {
            byte[] code = Encoding.Default.GetBytes(strSource);
            code = new MD5CryptoServiceProvider().ComputeHash(code);
            string strResult = "";
            for (int i = 0; i < code.Length; i++)
            {
                strResult += code[i].ToString("x").PadLeft(2, '0');
            }
            return strResult;
        }

        #region ===可逆加密=====

        /// <summary>
        /// 加密数据
        /// </summary>
        /// <param name="text">明文</param>
        /// <param name="strKey">密匙</param>
        /// <returns>密文</returns>
        public static string EncryptByKey(string text, string strKey)
        {
            using (var des = new DESCryptoServiceProvider())
            {
                byte[] inputByteArry = Encoding.Default.GetBytes(text);
                if (strKey.Length < 32)
                    strKey = strKey.PadRight(32);
                des.Key = Encoding.ASCII.GetBytes(strKey);
                des.IV = Encoding.ASCII.GetBytes(strKey);
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(inputByteArry, 0, inputByteArry.Length);
                    cs.FlushFinalBlock();
                    StringBuilder ret = new StringBuilder();
                    foreach (byte b in ms.ToArray())
                    {
                        ret.AppendFormat("{0:X2}", b);
                    }
                    return ret.ToString();
                }
            }
        }

        #endregion

        #region ===可逆解密=====

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="text">密文</param>
        /// <param name="strKey">密匙</param>
        /// <returns>明文</returns>
        public static string DecryptByKey(string text, string strKey)
        {
            using (var des = new DESCryptoServiceProvider())
            {
                if (strKey.Length < 32)
                    strKey = strKey.PadRight(32);

                int len = text.Length / 2;
                byte[] inputByteArry = new byte[len];
                int x;
                for (x = 0; x < len; x++)
                {
                    int i = Convert.ToInt32(text.Substring(x * 2, 2), 16);
                    inputByteArry[x] = (byte)i;
                }
                des.Key = Encoding.ASCII.GetBytes(strKey);
                des.IV = Encoding.ASCII.GetBytes(strKey);

                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(inputByteArry, 0, inputByteArry.Length);
                    cs.FlushFinalBlock();
                    string sq = Encoding.Default.GetString(ms.ToArray());
                    return sq;
                }
            }
        }

        #endregion
    }
}
