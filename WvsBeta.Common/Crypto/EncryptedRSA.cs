using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace WvsBeta.Common.Crypto
{
    public class EncryptedRSA
    {
        public class EncryptedKeyCreationResult
        {
            public byte[] passwordSalt;
            public byte[] privateKey;
            public byte[] publicKey;
        }

        private static string GetKeyFileName(string keyPrefix, bool privKey)
        {
            if (File.Exists(keyPrefix)) return keyPrefix;
            var ext = privKey ? "_private.key.xml" : "_public.key.xml";
            return keyPrefix + ext;
        }

        public static EncryptedKeyCreationResult CreatePasswordProtectedKeys(string password)
        {
            using var rsa = new RSACryptoServiceProvider();

            var xmlPrivate = rsa.ToXmlString(true);
            var xmlPublic = rsa.ToXmlString(false);

            var salt = new byte[8];
            RandomNumberGenerator.Fill(salt);
            using var k = new Rfc2898DeriveBytes(password, salt, 2000);
            var encryption = Aes.Create();
            encryption.IV = k.GetBytes(16);
            encryption.Key = k.GetBytes(32);
            using var encryptionStream = new MemoryStream();
            using (var encryptor = new CryptoStream(encryptionStream, encryption.CreateEncryptor(), CryptoStreamMode.Write))
            {
                var utfPrivate = new UTF8Encoding().GetBytes(xmlPrivate);
                encryptor.Write(utfPrivate, 0, utfPrivate.Length);
                encryptor.FlushFinalBlock();
            }

            var utfPublic = new UTF8Encoding().GetBytes(xmlPublic);
            return new EncryptedKeyCreationResult
            {
                passwordSalt = salt,
                privateKey = encryptionStream.ToArray(),
                publicKey = utfPublic
            };
        }

        public static string WritePrivateKeyToFile(EncryptedKeyCreationResult keys, string keyname)
        {
            var filename = GetKeyFileName(keyname, true);
            var header = new byte[1];
            header[0] = (byte)keys.passwordSalt.Length;
            var saltAndKey = header.Concat(keys.passwordSalt).Concat(keys.privateKey).ToArray();
            File.WriteAllBytes(filename, saltAndKey);
            return filename;
        }

        static byte[] Slice(byte[] source, int start, int finish)
        {
            var length = finish - start;
            var destfoo = new byte[length];
            Array.Copy(source, start, destfoo, 0, length);
            return destfoo;
        }

        public static RSACryptoServiceProvider ReadPrivateKeyFromFile(string keyname, string password)
        {
            var filename = GetKeyFileName(keyname, true);
            var everything = File.ReadAllBytes(filename);
            var saltLength = everything[0];
            var salt = Slice(everything, 1, 1 + saltLength);
            var privateKey = Slice(everything, 1 + saltLength, everything.Length);

            using var k = new Rfc2898DeriveBytes(password, salt, 2000);
            using var encryption = Aes.Create();
            encryption.IV = k.GetBytes(16);
            encryption.Key = k.GetBytes(32);

            using var decryptionStream = new MemoryStream();
            using (var decrypt = new CryptoStream(decryptionStream, encryption.CreateDecryptor(), CryptoStreamMode.Write))
            {
                decrypt.Write(privateKey, 0, privateKey.Length);
                decrypt.Flush();
            }

            var decryptedXml = new UTF8Encoding(false).GetString(decryptionStream.ToArray());

            var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(decryptedXml);
            return rsa;
        }

        public static string WritePublicKeyToFile(EncryptedKeyCreationResult keys, string keyname)
        {
            var filename = GetKeyFileName(keyname, false);
            File.WriteAllBytes(filename, keys.publicKey);
            return filename;
        }

        public static RSACryptoServiceProvider ReadPublicKeyFromFile(string keyname)
        {
            var filename = GetKeyFileName(keyname, false);
            Console.WriteLine($"Reading Public Key for {filename}");
            var publicXml = new UTF8Encoding(false).GetString(File.ReadAllBytes(filename));
            var result = new RSACryptoServiceProvider();
            result.FromXmlString(publicXml);
            return result;
        }

        public class RSAAuthChallenge
        {
            private byte[] Challenge;
            private RSACryptoServiceProvider RSA;

            public static byte[] WvsGlobalChallengeTransform(byte[] challengeBytes)
            {
                var result = new byte[challengeBytes.Length];
                var x = new byte[7] { 133, 221, 3, 3, 3, 123, 111 };
                var xLen = x.Length;

                for (var i = 0; i < challengeBytes.Length; i++)
                {
                    result[i] = (byte)(challengeBytes[i] ^ x[i % xLen]);
                }

                return result;
            }

            public RSAAuthChallenge(RSACryptoServiceProvider pRSA)
            {
                RSA = pRSA;
                Challenge = new byte[64];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(Challenge);
            }

            public byte[] GetEncryptedChallenge()
            {
                return RSA.Encrypt(Challenge, false);
            }

            public bool CheckChallengeResponse(byte[] response)
            {
                Console.WriteLine("Checking Challenge...");
                var correct = WvsGlobalChallengeTransform(Challenge);

                return correct.SequenceEqual(response);
            }
        }
    }
}
