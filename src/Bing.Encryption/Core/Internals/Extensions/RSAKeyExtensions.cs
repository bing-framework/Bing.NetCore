using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Newtonsoft.Json;

namespace Bing.Encryption.Core.Internals.Extensions
{
    /// <summary>
    /// RSA 密钥扩展
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal static class RSAKeyExtensions
    {
        /// <summary>
        /// 获取RSA key序列化Json
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="includePrivateParameters">是否包含私钥</param>
        internal static string ToJsonString(this RSA rsa, bool includePrivateParameters)
        {
            var parameters = rsa.ExportParameters(includePrivateParameters);

            var parasJson=new RSAParametersJson()
            {
                Modulus = parameters.Modulus!=null?Convert.ToBase64String(parameters.Modulus):null,
                Exponent = parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null,
                P = parameters.P != null ? Convert.ToBase64String(parameters.P) : null,
                Q = parameters.Q != null ? Convert.ToBase64String(parameters.Q) : null,
                DP = parameters.DP != null ? Convert.ToBase64String(parameters.DP) : null,
                DQ = parameters.DQ != null ? Convert.ToBase64String(parameters.DQ) : null,
                InverseQ = parameters.InverseQ != null ? Convert.ToBase64String(parameters.InverseQ) : null,
                D = parameters.D != null ? Convert.ToBase64String(parameters.D) : null,
            };

            return JsonConvert.SerializeObject(parasJson);
        }

        /// <summary>
        /// 从Json字符串中获取RSA
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="jsonString">Json字符串</param>
        internal static void FromJsonString(this RSA rsa, string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                throw new ArgumentNullException(nameof(jsonString));
            }

            var parameters=new RSAParameters();

            try
            {
                var paramsJson = JsonConvert.DeserializeObject<RSAParametersJson>(jsonString);
                parameters.Modulus = paramsJson.Modulus != null ? Convert.FromBase64String(paramsJson.Modulus) : null;
                parameters.Exponent = paramsJson.Exponent != null ? Convert.FromBase64String(paramsJson.Exponent) : null;
                parameters.P = paramsJson.P != null ? Convert.FromBase64String(paramsJson.P) : null;
                parameters.Q = paramsJson.Q != null ? Convert.FromBase64String(paramsJson.Q) : null;
                parameters.DP = paramsJson.DP != null ? Convert.FromBase64String(paramsJson.DP) : null;
                parameters.DQ = paramsJson.DQ != null ? Convert.FromBase64String(paramsJson.DQ) : null;
                parameters.InverseQ = paramsJson.InverseQ != null ? Convert.FromBase64String(paramsJson.InverseQ) : null;
                parameters.D = paramsJson.D != null ? Convert.FromBase64String(paramsJson.D) : null;
            }
            catch
            {
                throw new Exception("Invalid Json RSA key.");
            }
            rsa.ImportParameters(parameters);
        }

        /// <summary>
        /// 从Xml字符串中获取RSA
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="xmlString">Xml字符串</param>
        public static void FromExtXmlString(this RSA rsa, string xmlString)
        {
            RSAParameters parameters = new RSAParameters();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlString);
            if (doc.DocumentElement.Name.Equals("RSAKeyValue"))
            {
                foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                {
                    switch (node.Name)
                    {
                        case "Modulus":
                            parameters.Modulus = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "Exponent":
                            parameters.Exponent = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "P":
                            parameters.P = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "Q":
                            parameters.Q = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "DP":
                            parameters.DP = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "DQ":
                            parameters.DQ = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "InverseQ":
                            parameters.InverseQ = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                        case "D":
                            parameters.D = string.IsNullOrWhiteSpace(node.InnerText)
                                ? null
                                : Convert.FromBase64String(node.InnerText);
                            break;
                    }
                }
            }
            else
            {
                throw new Exception("Invalid Xml RSA Key");
            }
            rsa.ImportParameters(parameters);
        }

        /// <summary>
        /// 获取RSA key序列化Xml
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="includePrivateParameters">是否包含私钥</param>
        /// <returns></returns>
        public static string ToExtXmlString(this RSA rsa, bool includePrivateParameters)
        {
            RSAParameters parameters = rsa.ExportParameters(includePrivateParameters);

            if (includePrivateParameters)
            {
                return string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent><P>{2}</P><Q>{3}</Q><DP>{4}</DP><DQ>{5}</DQ><InverseQ>{6}</InverseQ><D>{7}</D></RSAKeyValue>",
                    parameters.Modulus != null ? Convert.ToBase64String(parameters.Modulus) : null,
                    parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null,
                    parameters.P != null ? Convert.ToBase64String(parameters.P) : null,
                    parameters.Q != null ? Convert.ToBase64String(parameters.Q) : null,
                    parameters.DP != null ? Convert.ToBase64String(parameters.DP) : null,
                    parameters.DQ != null ? Convert.ToBase64String(parameters.DQ) : null,
                    parameters.InverseQ != null ? Convert.ToBase64String(parameters.InverseQ) : null,
                    parameters.D != null ? Convert.ToBase64String(parameters.D) : null);
            }
            return string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent></RSAKeyValue>",
                parameters.Modulus != null ? Convert.ToBase64String(parameters.Modulus) : null,
                parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null);
        }

        /// <summary>
        /// 从私钥Base64字符串中获取RSA
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="base64String">Base64字符串</param>
        public static void FromBase64StringByPrivateKey(this RSA rsa, string base64String)
        {
            var base64Bytes = Convert.FromBase64String(base64String);

            var rsaParameters=new RSAParameters();

            using (BinaryReader binr = new BinaryReader(new MemoryStream(base64Bytes)))
            {
                byte bt = 0;
                ushort twobytes = 0;
                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130)
                    binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    throw new Exception("Unexpected value read binr.ReadUInt16()");

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102)
                    throw new Exception("Unexpected version");

                bt = binr.ReadByte();
                if (bt != 0x00)
                    throw new Exception("Unexpected value read binr.ReadByte()");

                rsaParameters.Modulus = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.Exponent = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.D = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.P = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.Q = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.DP = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.DQ = binr.ReadBytes(GetIntegerSize(binr));
                rsaParameters.InverseQ = binr.ReadBytes(GetIntegerSize(binr));
            }

            rsa.ImportParameters(rsaParameters);
        }

        /// <summary>
        /// 从公钥Base64字符串中获取RSA
        /// </summary>
        /// <param name="rsa">RSA</param>
        /// <param name="base64String">Base64字符串</param>
        public static void FromBase64StringByPublicKey(this RSA rsa, string base64String)
        {
            // encoded OID sequence for  PKCS #1 rsaEncryption szOID_RSA_RSA = "1.2.840.113549.1.1.1"
            byte[] seqOid = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            byte[] seq = new byte[15];

            var x509Key = System.Convert.FromBase64String(base64String);

            // ---------  Set up stream to read the asn.1 encoded SubjectPublicKeyInfo blob  ------
            using (MemoryStream mem = new MemoryStream(x509Key))
            {
                using (BinaryReader binr = new BinaryReader(mem))  //wrap Memory Stream with BinaryReader for easy reading
                {
                    byte bt = 0;
                    ushort twobytes = 0;

                    twobytes = binr.ReadUInt16();
                    if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
                        binr.ReadByte();    //advance 1 byte
                    else if (twobytes == 0x8230)
                        binr.ReadInt16();   //advance 2 bytes
                    else
                        return;

                    seq = binr.ReadBytes(15);       //read the Sequence OID
                    if (!CompareBytearrays(seq, seqOid))    //make sure Sequence for OID is correct
                        return;

                    twobytes = binr.ReadUInt16();
                    if (twobytes == 0x8103) //data read as little endian order (actual data order for Bit String is 03 81)
                        binr.ReadByte();    //advance 1 byte
                    else if (twobytes == 0x8203)
                        binr.ReadInt16();   //advance 2 bytes
                    else
                        return;

                    bt = binr.ReadByte();
                    if (bt != 0x00)     //expect null byte next
                        return;

                    twobytes = binr.ReadUInt16();
                    if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
                        binr.ReadByte();    //advance 1 byte
                    else if (twobytes == 0x8230)
                        binr.ReadInt16();   //advance 2 bytes
                    else
                        return;

                    twobytes = binr.ReadUInt16();
                    byte lowbyte = 0x00;
                    byte highbyte = 0x00;

                    if (twobytes == 0x8102) //data read as little endian order (actual data order for Integer is 02 81)
                        lowbyte = binr.ReadByte();  // read next bytes which is bytes in modulus
                    else if (twobytes == 0x8202)
                    {
                        highbyte = binr.ReadByte(); //advance 2 bytes
                        lowbyte = binr.ReadByte();
                    }
                    else
                        return;
                    byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };   //reverse byte order since asn.1 key uses big endian order
                    int modsize = BitConverter.ToInt32(modint, 0);

                    int firstbyte = binr.PeekChar();
                    if (firstbyte == 0x00)
                    {   //if first byte (highest order) of modulus is zero, don't include it
                        binr.ReadByte();    //skip this null byte
                        modsize -= 1;   //reduce modulus buffer size by 1
                    }

                    byte[] modulus = binr.ReadBytes(modsize);   //read the modulus bytes

                    if (binr.ReadByte() != 0x02)            //expect an Integer for the exponent data
                        return;
                    int expbytes = (int)binr.ReadByte();        // should only need one byte for actual exponent data (for all useful values)
                    byte[] exponent = binr.ReadBytes(expbytes);

                    // ------- create RSACryptoServiceProvider instance and initialize with public key -----
                    RSAParameters rsaKeyInfo = new RSAParameters
                    {
                        Modulus = modulus,
                        Exponent = exponent
                    };
                    rsa.ImportParameters(rsaKeyInfo);
                }

            }
        }

        #region 导入密钥算法

        private static int GetIntegerSize(BinaryReader binr)
        {
            byte bt = 0;
            int count = 0;
            bt = binr.ReadByte();
            if (bt != 0x02)
                return 0;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();
            else
            if (bt == 0x82)
            {
                var highbyte = binr.ReadByte();
                var lowbyte = binr.ReadByte();
                byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                count = BitConverter.ToInt32(modint, 0);
            }
            else
            {
                count = bt;
            }

            while (binr.ReadByte() == 0x00)
            {
                count -= 1;
            }
            binr.BaseStream.Seek(-1, SeekOrigin.Current);
            return count;
        }

        private static bool CompareBytearrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            int i = 0;
            foreach (byte c in a)
            {
                if (c != b[i])
                    return false;
                i++;
            }
            return true;
        }

        #endregion
    }
}
