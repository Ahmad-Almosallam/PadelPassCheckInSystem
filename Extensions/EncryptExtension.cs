using System.Security.Cryptography;
using System.Text;

namespace PadelPassCheckInSystem.Extensions;

public static class EncryptExtension
{
    public static string PublicKey { get; set; }

    public static string SaltKey { get; set; }

    static readonly char[] _padding = { '=' };

    public static string Encrypt(this int intToEncrypt)
    {
        return Encrypt(intToEncrypt.ToString());
    }

    public static string Encrypt(this string textToEncrypt)
    {
        try
        {
            var secretkeyByte = Encoding.UTF8.GetBytes(SaltKey);
            var publickeybyte = Encoding.UTF8.GetBytes(PublicKey);
            var inputbyteArray = Encoding.UTF8.GetBytes(textToEncrypt);
            using var aes = Aes.Create();
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(publickeybyte, secretkeyByte), CryptoStreamMode.Write);

            cs.Write(inputbyteArray, 0, inputbyteArray.Length);
            cs.FlushFinalBlock();

            return Convert.ToBase64String(ms.ToArray())
                .TrimEnd(_padding)
                .Replace('+', '-')
                .Replace('/', '_');
        }
        catch (Exception)
        {
            throw;
        }
    }

    public static bool TryDecryptInt(this string textToDecrypt, out int decryptedInt)
    {
        return int.TryParse(textToDecrypt.Decrypt(), out decryptedInt);
    }

    public static string Decrypt(this string textToDecrypt)
    {
        try
        {
            var incoming = textToDecrypt.Replace('_', '/').Replace('-', '+');
            switch (textToDecrypt.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }

            var secretkeyByte = Encoding.UTF8.GetBytes(SaltKey);
            var publickeybyte = Encoding.UTF8.GetBytes(PublicKey);
            var inputbyteArray = new byte[incoming.Length];

            inputbyteArray = Convert.FromBase64String(incoming);

            using var aes = Aes.Create();
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(publickeybyte, secretkeyByte), CryptoStreamMode.Write);

            cs.Write(inputbyteArray, 0, inputbyteArray.Length);
            cs.FlushFinalBlock();

            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (Exception)
        {
            throw;
        }
    }

}