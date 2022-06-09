using System;
using System.Security.Cryptography;

namespace Sample.Authentication.Server.Models
{
    public static class Constants
    {
        private static string state = GetRandomString(8);

        public static string State { get => state; set => state = value; }

        /// <summary>
        /// Create a random Base64Encoded string
        /// </summary>
        /// <returns></returns>
        private static string GetRandomString(int length)
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(length);
            return Convert.ToBase64String(bytes);
        }
    }
}
