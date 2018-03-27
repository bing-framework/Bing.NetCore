using System;
using System.Collections.Generic;
using System.Text;
using Bing.Encryption.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Encryption.Tests.Asymmetric
{
    public class RSAEncryptionProviderTest:TestBase
    {
        public RSAEncryptionProviderTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_Encrypt()
        {
            var key = RSAEncryptionProvider.CreateKey();
            var source = "装逼打脸2018";
            var cliphertext = RSAEncryptionProvider.Encrypt(source, key.PublickKey,outType:OutType.Hex);
            var plaintext = RSAEncryptionProvider.Decrypt(cliphertext, key.PrivateKey, outType: OutType.Hex);
            var signData = RSAEncryptionProvider.SignData(source, key.PrivateKey);
            var verifyData = RSAEncryptionProvider.VerifyData(source, signData, key.PublickKey);
            Output.WriteLine("密文:\n"+cliphertext);
            Output.WriteLine("明文:\n" + plaintext);
            Output.WriteLine("签名:\n" + signData);
            Output.WriteLine("验签:\n" + verifyData);
        }
    }
}
