using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Sample.Auth.Pkce.Services
{
    public class RandomStringBuilder
    {
        private static readonly List<int> RandomSet = new List<int>();
        static RandomStringBuilder()
        {
            RandomSet.AddRange(Enumerable.Range('A', 26));
            RandomSet.AddRange(Enumerable.Range('a', 26));
            RandomSet.AddRange(Enumerable.Range('0', 10));
            RandomSet.Add('-');
            RandomSet.Add('.');
            RandomSet.Add('_');
            RandomSet.Add('~');
        }

        /// <summary>
        /// Return a random string having characters [A-Z] / [a-z] / [0-9] / "-" / "." / "_" / "~" with the given length
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public string GetRandomString(int length)
        {
            if (length <= 0)
                return string.Empty;

            StringBuilder randomString = new StringBuilder(length);
            using (var rnd = new RNGCryptoServiceProvider())
            {
                byte[] buf = new byte[length];
                rnd.GetBytes(buf);

                System.Collections.IEnumerator enumerator = buf.GetEnumerator();
                while (randomString.Length < length)
                {
                    enumerator.MoveNext();
                    int index = Convert.ToInt32(enumerator.Current) % RandomSet.Count();
                    randomString.Append((char)RandomSet[index]);
                }

                return randomString.ToString();
            }
        }
    }
}
