using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using WvsBeta.Common.Crypto;
using static WvsBeta.Common.Crypto.EncryptedRSA;

namespace WvsBeta.AdminKeygen
{
    class Program
    {
        private static SecureString GetConsoleSecurePassword()
        {
            SecureString pwd = new SecureString();
            while (true)
            {
                ConsoleKeyInfo i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (i.Key == ConsoleKey.Backspace)
                {
                    pwd.RemoveAt(pwd.Length - 1);
                    Console.Write("\b \b");
                }
                else
                {
                    pwd.AppendChar(i.KeyChar);
                    Console.Write('*');
                }
            }
            return pwd;
        }

        private static string GetConsolePassword()
        {
            string password = null;
            while (true)
            {
                var key = System.Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                password += key.KeyChar;
                Console.Write('*');
            }

            return password;
        }

        static void Main(string[] args)
        {
            var keyname = "";
            if (args.Length == 1)
            {
                keyname = args[0];
            }
            else
            {
                Console.Write("Key name: ");
                keyname = Console.ReadLine();
            }

            if (keyname == "") return;

            Console.Write("Password: ");
            var pass = GetConsolePassword();

            Console.WriteLine();
            Console.WriteLine("Generating...");
            var keys = CreatePasswordProtectedKeys(pass);
            var privateKeyFile = WritePrivateKeyToFile(keys, keyname);
            var publicKeyFile = WritePublicKeyToFile(keys, keyname);
            Console.WriteLine("Done.");

            Console.WriteLine();
            Console.WriteLine("Testing Challenge...");
            Console.Write("Enter Password: ");
            pass = GetConsolePassword();
            var rsa1 = ReadPublicKeyFromFile(keyname);
            var challenge = new RSAAuthChallenge(rsa1);
            var encrypted = challenge.GetEncryptedChallenge();
            var rsa2 = ReadPrivateKeyFromFile(keyname, pass);
            var decrypted = rsa2.Decrypt(encrypted, false);
            var solved = RSAAuthChallenge.WvsGlobalChallengeTransform(decrypted);
            Console.WriteLine();
            Console.WriteLine($"Challenge solved: {challenge.CheckChallengeResponse(solved)}.");

            Console.WriteLine();
            Console.WriteLine("Public key @ {0}", publicKeyFile);
            Console.WriteLine("Private key @ {0}", privateKeyFile);
            Console.WriteLine();
            Console.WriteLine("Press OK to exit");
            Console.ReadLine();
        }
    }
}
