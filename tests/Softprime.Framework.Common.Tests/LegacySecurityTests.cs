using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Softprime.Framework.Common.Security;
using Xunit;

namespace Softprime.Framework.Common.Tests
{
    public class LegacySecurityTests
    {
        private const string UnicodeText = "ação – teste";
        private const string FileKey = "FileKey1";

        [Theory]
        [InlineData(UnicodeText, "HC1BbXjDQGkZKiSHjD/yLX3p0vWjFMfJ")]
        [InlineData("", "Yq04jCyj168=")]
        public void CryptoString_LegacyFixture_CanBeReadAndReproduced(string text, string ciphertext)
        {
            CryptoString.Decrypt(ciphertext).Should().Be(text);
            CryptoString.Encrypt(text).Should().Be(ciphertext);
        }

        [Fact]
        public void CryptoString_InvalidInputs_ThrowsLegacyExceptions()
        {
            Action encryptNull = () => CryptoString.Encrypt(null);
            Action decryptMalformed = () => CryptoString.Decrypt("invalid!");
            Action decryptTruncated = () => CryptoString.Decrypt("AQ==");

            encryptNull.Should().Throw<ArgumentNullException>();
            decryptMalformed.Should().Throw<FormatException>();
            decryptTruncated.Should().Throw<System.Security.Cryptography.CryptographicException>();
        }

        [Fact]
        public void CryptoFile_LegacyUtf8Fixture_DecryptsAndRoundTrips()
        {
            using var directory = new TemporaryDirectory();
            var ciphertext = directory.PathFor("fixture.des");
            var plaintext = directory.PathFor("plain.txt");
            var encrypted = directory.PathFor("roundtrip.des");
            var decrypted = directory.PathFor("roundtrip.txt");
            File.WriteAllBytes(ciphertext, Convert.FromBase64String("j+Q0ybAEnd1cj+2RKyXMteavGiOQBndu"));

            CryptoFile.DecryptFile(ciphertext, plaintext, FileKey);
            CryptoFile.EncryptFile(plaintext, encrypted, FileKey);
            CryptoFile.DecryptFile(encrypted, decrypted, FileKey);

            File.ReadAllText(plaintext, Encoding.UTF8).Should().Be(UnicodeText);
            File.ReadAllText(decrypted, Encoding.UTF8).Should().Be(UnicodeText);
        }

        [Fact]
        public void CryptoFile_InvalidKeyLength_ThrowsBeforeReadingTheFile()
        {
            Action decrypt = () => CryptoFile.DecryptFile("unused-input", "unused-output", "short");

            decrypt.Should().ThrowExactly<ArgumentException>();
        }

        [Theory]
        [InlineData(HashAlgorithmType.Md5, "AB2E840815BD76605DF23F1FBC4E17E2", "ZuxAi23oJgh8HCIljBelJAMRHfo=")]
        [InlineData(HashAlgorithmType.Sha1, "12E7D455E070012244A834393DC2778960D59DFE", "7EvTbS6YZ7up+vns51Ni+SMfrwkDER36")]
        [InlineData(HashAlgorithmType.Sha256, "07C6CA0F5162A728589967756402D39D90D615E9E3F272DA9A9165EBF514E7A6", "VrMI8ZpGOdCAno/G+nGri8SbCchfJ5EPaxT3JiXW1cMDER36")]
        [InlineData(HashAlgorithmType.Sha384, "4B6A4E9D76E3BF457A4A6D1B91933182E7D1843C3FEC1D0687F88F12E6D19A88973127AAA5F2313A2FE50EC39F7B70AF", "GLmZeIVbcZymGPp5ldaiRf5w71q3RLjRQUc7pOvitxtvzPmOzvfuIh1xrkkatvLCAxEd+g==")]
        [InlineData(HashAlgorithmType.Sha512, "2F105373DEB82EFB82584F3FAEB67075FD71DD31BB0C5E3C69E2DA676EBB6F412C607858C645F6F038B8C80D7C4B0E0C7021CAD6F5DBCB09ACE10B6BB7667FA4", "SEzp0nFYocsCjHNsFBSntfvDi2+oHW4g/33tzCW6ieuC2TkE9eY57oAtz8mH3ULqabsDcLvlCBewnfJEgPJrbAMRHfo=")]
        public void Hash_LegacyFixtures_ProduceHexAndValidateSaltedValues(
            HashAlgorithmType algorithm, string hex, string salted)
        {
            var salt = new byte[] { 3, 17, 29, 250 };
            var legacyHex = algorithm switch
            {
                HashAlgorithmType.Md5 => Crypto.Md5Encrypt(UnicodeText),
                HashAlgorithmType.Sha1 => Crypto.Sha1Encrypt(UnicodeText),
                HashAlgorithmType.Sha256 => Crypto.Sha256Encrypt(UnicodeText),
                HashAlgorithmType.Sha384 => Crypto.Sha384Encrypt(UnicodeText),
                HashAlgorithmType.Sha512 => Crypto.Sha512Encrypt(UnicodeText),
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
            };

            legacyHex.Should().Be(hex);
            SimpleHash.ComputeHash(UnicodeText, algorithm, salt).Should().Be(salted);
            SimpleHash.VerifyHash(UnicodeText, algorithm, salted).Should().BeTrue();
            SimpleHash.VerifyHash(UnicodeText + "!", algorithm, salted).Should().BeFalse();
        }

        [Theory]
        [InlineData(UnicodeText)]
        [InlineData("")]
        public void ComputeHash_GeneratedSalt_VerifiesOriginalText(string plaintext)
        {
            const HashAlgorithmType algorithm = HashAlgorithmType.Sha256;

            var generated = SimpleHash.ComputeHash(plaintext, algorithm, null);

            Convert.FromBase64String(generated).Length.Should().BeInRange(36, 39);
            SimpleHash.VerifyHash(plaintext, algorithm, generated).Should().BeTrue();
        }

        [Fact]
        public void VerifyHash_MalformedAndShortFixtures_ThrowOrReturnFalse()
        {
            Action verifyMalformed = () => SimpleHash.VerifyHash("a", HashAlgorithmType.Md5, "!");

            verifyMalformed.Should().Throw<FormatException>();
            SimpleHash.VerifyHash("a", HashAlgorithmType.Md5, "AQ==").Should().BeFalse();
        }

        [Fact]
        public void ComputeMd5HashFromFile_AsciiFixture_ReturnsLowercaseHexAndClosesStream()
        {
            var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

            var hash = SimpleHash.ComputeMd5HashFromFile(stream);

            hash.Should().Be("900150983cd24fb0d6963f7d28e17f72");
            stream.CanRead.Should().BeFalse();
            SimpleHash.VerifyMd5HashFromFile(new MemoryStream(Encoding.ASCII.GetBytes("abc")), hash).Should().BeTrue();
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            private readonly string path = Path.Combine(Path.GetTempPath(), "softprime-crypto-" + Guid.NewGuid());

            public TemporaryDirectory() => Directory.CreateDirectory(path);

            public string PathFor(string name) => Path.Combine(path, name);

            public void Dispose()
            {
                // DecryptFile leaves the input stream to finalization.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
