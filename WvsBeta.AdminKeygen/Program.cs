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

        static void SignServerConfig(string serverConfigPath, string privateKeyPath, string signedPath)
        {
            const uint magic = 0x31333337;

            Console.Write("Key Password: ");
            var pass = GetConsolePassword();

            var rsa = ReadPrivateKeyFromFile(privateKeyPath, pass);

            var serverConfigContents = File.ReadAllBytes(serverConfigPath);

            var sign = rsa.SignData(serverConfigContents, SHA256.Create());

            if (signedPath == null)
            {
                signedPath = serverConfigPath + ".signed";
            }
            File.WriteAllBytes(signedPath, serverConfigContents);
            File.AppendAllBytes(signedPath, sign);
            File.AppendAllBytes(signedPath, BitConverter.GetBytes((uint)sign.Length));
            File.AppendAllBytes(signedPath, BitConverter.GetBytes((uint)magic));

            Console.WriteLine("Signed file, stored at {0}", signedPath);
            Console.WriteLine("Press OK to exit");
            Console.ReadLine();
        }

        static void GenerateKeys()
        {
            Console.Write("Key name: ");
            var keyname = Console.ReadLine();
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

        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "help")
            {
                Console.WriteLine(@"
Usage:
 help (or no arguments)
    - show this message
 generate
    - Generate RSA private/public keypair
 sign <path to ServerConfig.img> <path to server private.key>
    - Sign the ServerConfig.img with the given private key
");
                return;
            }

            switch (args[0])
            {
                case "generate":
                    GenerateKeys();
                    return;
                case "sign":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Missing path to ServerConfig.img, and path to Server private key.");
                        return;
                    }
                    string signedPath = null;
                    if (args.Length > 3) signedPath = args[3];

                    SignServerConfig(args[1], args[2], signedPath);
                    return;
                default:
                    Console.WriteLine("Not sure what this is: {0}", args[0]);
                    return;
            }
        }
    }
}
