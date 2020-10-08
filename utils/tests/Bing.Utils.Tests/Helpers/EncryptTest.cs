using System.Text;
using Bing.Extensions;
using Bing.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// 加密操作测试
    /// </summary>
    public class EncryptTest : TestBase
    {
        /// <summary>
        /// 初始化一个<see cref="EncryptTest"/>类型的实例
        /// </summary>
        public EncryptTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 测试Md5加密，返回16位结果
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "C0F1B6A831C399E2")]
        [InlineData("中国", "CB143ACD6C929826")]
        [InlineData("123456", "49BA59ABBE56E057")]
        public void Test_Md5By16(string input, string result)
        {
            Output.WriteLine($"input:{input},result:{Encrypt.Md5By16(input)}");
            Assert.Equal(result, Encrypt.Md5By16(input));
        }

        /// <summary>
        /// 测试Md5加密，返回32位结果
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "0CC175B9C0F1B6A831C399E269772661")]
        [InlineData("中国", "C13DCEABCB143ACD6C9298265D618A9F")]
        [InlineData("123456", "E10ADC3949BA59ABBE56E057F20F883E")]
        public void Test_Md5By32(string input, string result)
        {
            Output.WriteLine($"input:{input},result:{Encrypt.Md5By32(input)}");
            Assert.Equal(result, Encrypt.Md5By32(input));
        }

        /// <summary>
        /// 测试DES加密验证
        /// </summary>
        [Theory]
        [InlineData(null, "", "")]
        [InlineData("", "", "")]
        [InlineData("1", "", "")]
        [InlineData("1", "2", "")]
        public void Test_Des_Validate(string input, string key, string result)
        {
            Assert.Equal(result, Encrypt.DesEncrypt(input, key, Encoding.UTF8));
            Assert.Equal(result, Encrypt.DesDecrypt(input, key, Encoding.UTF8));
        }

        /// <summary>
        /// 测试DES加密
        /// </summary>
        [Fact]
        public void Test_Des_1()
        {
            const double value = 100.123;
            var encode = Encrypt.DesEncrypt(value);
            Output.WriteLine(encode);
            Assert.Equal(value.SafeString(), Encrypt.DesDecrypt(encode));
        }

        /// <summary>
        /// 测试DES加密
        /// </summary>
        [Fact]
        public void Test_Des_2()
        {
            const string value = @"~!@#$%^&*()_+|,./;[]'{}""}{?>:<> \\ qwe测 *试rtyuiopadE15R3JrMnByS3c9sdfghjklzxcvbnm1234567890-=\";
            var encode = Encrypt.DesEncrypt(value);
            Output.WriteLine(encode);
            Assert.Equal(value, Encrypt.DesDecrypt(encode));
        }

        /// <summary>
        /// 测试AES加密验证
        /// </summary>
        [Theory]
        [InlineData(null, "", "")]
        [InlineData("", "", "")]
        [InlineData("1", "", "")]
        public void Test_Aes_Validate(string input, string key, string result)
        {
            Assert.Equal(result, Encrypt.AesEncrypt(input, key, Encoding.UTF8));
            Assert.Equal(result, Encrypt.AesDecrypt(input, key, Encoding.UTF8));
        }

        /// <summary>
        /// 测试AES加密
        /// </summary>
        [Fact]
        public void Test_Aes_1()
        {
            const string value = "a";
            var encode = Encrypt.AesEncrypt(value);
            Output.WriteLine(encode);
            Assert.Equal(value, Encrypt.AesDecrypt(encode));
        }

        /// <summary>
        /// 测试AES加密
        /// </summary>
        [Fact]
        public void Test_Aes_2()
        {
            const string value = "中国";
            var encode = Encrypt.AesEncrypt(value);
            Output.WriteLine(encode);
            Assert.Equal(value, Encrypt.AesDecrypt(encode));
        }

        /// <summary>
        /// 测试RSA签名验证
        /// </summary>
        [Theory]
        [InlineData(null, "", "")]
        [InlineData("", "", "")]
        [InlineData("1", "", "")]
        public void Test_RsaSign_Validate(string input, string key, string result)
        {
            Assert.Equal(result, Encrypt.RsaSign(input, key, Encoding.UTF8));
            Assert.Equal(result, Encrypt.Rsa2Sign(input, key, Encoding.UTF8));
        }

        /// <summary>
        /// RSA私钥
        /// </summary>
        public const string RsaKey = "MIIEogIBAAKCAQEAuLbs8Jugb3qhzDu4rvMqQ8n1RS8TQCpJ3+Cg9qR/RgMcpBx8+0tUiYkfOOnzxGlBuIwGF7Hqyho2E1ICNoIeNY4GkUhxBk7/wz4M6/tbfKSmWp1PAi9gVOxT0Io1kNBAV0it+uiDA176qk2tIKPxQ7UBPRB6qVELHuM7Y9AVoOQbHe56+rEoTiRo13NTx01yg0xiZDzS5gAe/vu+rDAKBczm7ZQ0A4U//modw1/rV+GKiqJ8CIDHe7a8oW2rthDNTZ2C/CHug4QMEmhaNazvhzjyAE1rfvYLF0o92qEfkip3IQRJnFM4rrr9QvWjkSPO7sPu5rMyE4oeUZHJ8luhIwIDAQABAoIBAGE8ytaO1pJY+DvPZJWUpLcy5c8ZzQSGPoWAdrvgNK/ii31JEfIn4cTVTn5jilPnJRXFgJ+QpYzm53icP1X6gXSn44UvoXA0vidFzv+bProK4xfon+MCla+fCTBK0Y/+USChvhTLucxYf5SPd4grRaLi8lf3CNuBMl18OZN9wyUCicgcqOp2mwi46daqqqvNLJwzmiKVCMb82JKEVShkmRDp8+ST7imwtXypUzLnwRt00xobiO8Gi1B1jYE4xM0hmkVgEHHLMGUOlfECH/VcWFLRkwosM3P6PYwb/mfBiDrIzN2mbueKZxMZureUqll9uLWRwPEvTosqc+tl+a4jCkECgYEA784SWiUGoSBDuKu9wm2D5Almz0gZavMv15cYsTfbu6UzsLcIRLEqLG1Rv9bFnwR39Gd1Cl3JAKDlxhs8qxJhy5zi0TJUowF9/QAg7TgBkGZVU4p+6SKF4mPKKsC4tnpENM+FnylncpCjFs45+MNruUIq1OVkcfxGBaBxDB/SM6kCgYEAxTBozzAyfH+9HHNisCQ2x7iNWEuaydY39V8vAlGPRZSbbo8OoV3wlidZm2wAhRybUCg7wefdbMAJlH8Uq/HnwuwanEkMO4IH/t5WI/MKk4Wc55iwC1abqQZrAxNCfjG+fShr2AZHO1jTj4oyd3BuQFTQBdRtiB+g8ibrGYG1resCgYA/sJ2TL45JMQaLf6GQiAGliRGzL9UAYMJuIgU+3DUR61iFMLeTdvJahlZV+zbVexxY3zlonWwLLLCaIxXD4cfziiF7qkBsYrMRhP05w8w2i9dRrtDyHmcsr5A8Np9YZ7TByfQVR6vf86Y9IlynQ0/TDk3N6Xb6BySZzfj4XWM4sQKBgBKeW4cUme+/b++7xVm0UafR+SaZHOhp3abBcgLaCJkdSv/Jaiw6XnkPBhryu6nV5aRP6DSK3BFkoILw7Na/ZI63FFwlWY5U3MRn4eJLFHiRaRtFA3pOlywCeyAzNVgNAlt28ZfYH+munWs0NUepyf8xAuNKB32O3vd+TTx/TtQ5AoGAGgD1Rc2hTopjXI8P5K1wuEMrvUp2uo0apguHu2uzIUpFyQYgjp80hBsqDj6e22R0LDzQTPrj8i+KvLZ4Xu8iCCQrUMqOvE6oZbQ7ukJ+wZebLOnrI6/AzD/zza3LZMXt/lKFgbaiYFLOSPEwxg+VBxxe10aQ3ddp5NuBDjQXJW0=";

        /// <summary>
        /// 测试RSA签名算法
        /// </summary>
        [Fact]
        public void Test_RsaSign()
        {
            const string value = "sign_type=RSA";
            const string result = "b5jaOTwneLBLTXmemp2BvThFqpmgBsM60GX28MUERYx0vRGiWLw31sif7mlt6Sz063p9zo3quQ8hCwDy6Qssz7i50pqHaJuobpayQZJNHiYpzH07ZxkvJhqsG8IrXj4Q2rmyxpDayU3kg9lhT14VadnLflaypwTpvuy3nGpIoY62d5ciObwxJBDYeilIJag5iY/xTWqv1z4TAr5E6u4zo2aY4rGGKruIX4vsI58EmxzI82clz11uK964Eco/RXCZrha3Vthx5sa6yIPvr2xG95Va4UZglgX7c/wXyMyFp1t/MtKb/0IdQocuRtBmyJw5n0CdmTfw7WxcdNEecgwqjQ==";
            var encode = Encrypt.RsaSign(value, RsaKey);
            Output.WriteLine(encode);
            Assert.Equal(result, encode);
        }

        /// <summary>
        /// 测试RSA2签名算法
        /// </summary>
        [Fact]
        public void Test_Rsa2Sign()
        {
            const string value = "sign_type=RSA2";
            const string result = "cFIjAWDAuNzRYzGOr65ux4e5GEOUvKUT0mLTpAJ89vem70IsdKCrs0IY2TANw3I6pBqdeG0Lz6kNeWHkurN+tj1+C/7ZpRgHIilV+sUU5Dv0Nw/cDVjvs4fyKJ4CEr8zcs1MB1ek0COuQ/kfHxbAr9sWE9a0nqxnZ/FnsDy5ogFP1LQStkms+e7Ph9CC/dyl6JRlpgZx7/NwnN9kF3zEnVwdPxxLq5as1EV7FmlpLcuI/tkCpL8G+vPJcB3xktM9EBBRMR+peDbusZ1fOAuxE7zbW3XVsgz7JzKUcHE5KNS3zzcov404zKT/8i/ezyCxRCWRHDy3O3zHg5bUUOluIQ==";
            var encode = Encrypt.Rsa2Sign(value, RsaKey);
            Output.WriteLine(encode);
            Assert.Equal(result, encode);
        }

        /// <summary>
        /// 测试HMACMD5加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "80ea44ccea14cc2263889567cfacccd7")]
        [InlineData("中国", "03ec5797fd88b9f57007436234b95893")]
        [InlineData("123456", "0abf6bacd23c55fa6ab14eb44a7f5720")]
        public void Test_HmacMd5(string input, string result)
        {
            var key = "key";
            Output.WriteLine($"input:{input},result:{Encrypt.HmacMd5(input, key)}");
            Assert.Equal(result, Encrypt.HmacMd5(input, key));
        }

        /// <summary>
        /// 测试HMACSHA1加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "0db21f05052f323e714ef9bf1f7b000ffe97e8a0")]
        [InlineData("中国", "2f20d47bac4a38122788bd825daeed122cc3968a")]
        [InlineData("123456", "4fc32f51f214211618a9598893823519b829ee74")]
        public void Test_HmacSha1(string input, string result)
        {
            var key = "key";
            Output.WriteLine($"input:{input},result:{Encrypt.HmacSha1(input, key)}");
            Assert.Equal(result, Encrypt.HmacSha1(input, key));
        }

        /// <summary>
        /// 测试HMACSHA256加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "780c3db4ce3de5b9e55816fba98f590631d96c075271b26976238d5f4444219b")]
        [InlineData("中国", "dde7619d5465b73d94c18e6d979ab3dd9e478cb91b00d312ece776b282b7e0a9")]
        [InlineData("123456", "4df81f55d708ae1720d5f65ef42f3475dc168fa23fde424ac5944f87c309b05f")]
        public void Test_HmacSha256(string input, string result)
        {
            var key = "key";
            Output.WriteLine($"input:{input},result:{Encrypt.HmacSha256(input, key)}");
            Assert.Equal(result, Encrypt.HmacSha256(input, key));
        }

        /// <summary>
        /// 测试HMACSHA384加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        //[InlineData("a", "c1463cb1e254b7c79403a9b32ed5f8f1b1f570f0abea2da4d1cf6daa0e61632659a3e84a333d82645f5fc2dae9bfbd8b")]
        //[InlineData("中国", "326debfeb264b262deb75437a79fd44210429c3267fdec09182d373f44fa680f10f18a783137bdc934fb2c3c13086d41")]
        //[InlineData("123456", "65bdca7c6081e605ca9a7fa7a249332b6f372fa95d8fd0a4341beed0231d8c03d8e1ae66165de0a87ca2b718f3459584")]
        [InlineData("a", "5d3b9a2b2b166c987b009e5a9946f7525e05f2f91adf65ba5ad135da5d0630296665ac14ca0f30b17d8a7f6674a13083")]
        [InlineData("中国", "bb872d1ffa93ec5868a3488d2913e05d2b69d1d441e677457b2eee7e336a789edfd1f15b13b42e1d49865aba45942ca0")]
        [InlineData("123456", "27d471b4ba3ed3d53920336eef1ccde82ee1b1f6c65f1f81cb3851b3b17033f76157465404d6652fedbeecf77b975529")]
        public void Test_HmacSha384(string input, string result)
        {
            var key = "key";
            Output.WriteLine($"input:{input},result:{Encrypt.HmacSha384(input, key)}");
            Assert.Equal(result, Encrypt.HmacSha384(input, key));
        }

        /// <summary>
        /// 测试HMACSHA512加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "1d51a72107e0cc8efc886683ad30ea10134600bfb147d0d5faf3e95648f3d05459ba8fb541904fd1c5963730e3c2a5cd3a30ee431e225295f345fc5662487a48")]
        [InlineData("中国", "28799f55a1e894166f03755bfb79ab6a1a3c0f7fa2f2642a666675208e75da02448d45fd6f37c95e0703ce4d79c6ff0d7347c67ad3b0134e86a5e0216bc70091")]
        [InlineData("123456", "cc2f51259b61903f6b50ea2cc3653340f1e8c0ae780c927bc1c7f09dcd1d606f7cb3347117f75fa19d9f760f4d538709a969c11036d194b972bf3232f34f30a8")]
        public void Test_HmacSha512(string input, string result)
        {
            var key = "key";
            Output.WriteLine($"input:{input},result:{Encrypt.HmacSha512(input, key)}");
            Assert.Equal(result, Encrypt.HmacSha512(input, key));
        }

        /// <summary>
        /// 测试SHA1加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "86f7e437faa5a7fce15d1ddcb9eaeaea377667b8")]
        [InlineData("中国", "101806f57c322fb403a9788c4c24b79650d02e77")]
        [InlineData("123456", "7c4a8d09ca3762af61e59520943dc26494f8941b")]
        public void Test_Sha1(string input, string result)
        {
            Output.WriteLine($"input:{input},result:{Encrypt.Sha1(input)}");
            Assert.Equal(result, Encrypt.Sha1(input));
        }

        /// <summary>
        /// 测试SHA256加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb")]
        [InlineData("中国", "f0e9521611bb290d7b09b8cd14a63c3fe7cbf9a2f4e0090d8238d22403d35182")]
        [InlineData("123456", "8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92")]
        public void Test_Sha256(string input, string result)
        {
            Output.WriteLine($"input:{input},result:{Encrypt.Sha256(input)}");
            Assert.Equal(result, Encrypt.Sha256(input));
        }

        /// <summary>
        /// 测试SHA384加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "54a59b9f22b0b80880d8427e548b7c23abd873486e1f035dce9cd697e85175033caa88e6d57bc35efae0b5afd3145f31")]
        [InlineData("中国", "ebe1c5966f14a75396a6b2b31395fc3bcc01d3d3c43b7d135e72c8e3d9bbe6461d8aeac37c208e1312e2f278074d7e29")]
        [InlineData("123456", "0a989ebc4a77b56a6e2bb7b19d995d185ce44090c13e2984b7ecc6d446d4b61ea9991b76a4c2f04b1b4d244841449454")]
        public void Test_Sha384(string input, string result)
        {
            Output.WriteLine($"input:{input},result:{Encrypt.Sha384(input)}");
            Assert.Equal(result, Encrypt.Sha384(input));
        }

        /// <summary>
        /// 测试SHA512加密
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData("a", "1f40fc92da241694750979ee6cf582f2d5d7d28e18335de05abc54d0560e0f5302860c652bf08d560252aa5e74210546f369fbbbce8c12cfc7957b2652fe9a75")]
        [InlineData("中国", "6a169e7d5b7526651086d0d37d6e7686c7e75ff7039d063ad100aefab1057a4c1db1f1e5d088c9585db1d7531a461ab3f4490cc63809c08cc074574b3fff759a")]
        [InlineData("123456", "ba3253876aed6bc22d4a6ff53d8406c6ad864195ed144ab5c87621b6c233b548baeae6956df346ec8c17f5ea10f35ee3cbc514797ed7ddd3145464e2a0bab413")]
        public void Test_Sha512(string input, string result)
        {
            Output.WriteLine($"input:{input},result:{Encrypt.Sha512(input)}");
            Assert.Equal(result, Encrypt.Sha512(input));
        }

        /// <summary>
        /// 测试Base64加密
        /// </summary>
        [Fact]
        public void Test_Base64()
        {
            const string value = "123456";
            var encode = Encrypt.Base64Encrypt(value);
            Output.WriteLine(encode);
            Assert.Equal(value, Encrypt.Base64Decrypt(encode));
        }
    }
}
