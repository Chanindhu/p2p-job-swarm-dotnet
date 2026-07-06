using System;
using System.Security.Cryptography;
using System.Text;

namespace Swarm.Client.Core
{
    public static class Crypto
    {
        public static string Sha256HexOfBase64(string b64)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(b64);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static bool VerifySha256OfBase64(string b64, string expectedHex)
        {
            return string.Equals(Sha256HexOfBase64(b64), expectedHex, StringComparison.OrdinalIgnoreCase);
        }
    }
}
