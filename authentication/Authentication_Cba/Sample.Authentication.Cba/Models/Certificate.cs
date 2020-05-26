using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;

namespace Sample.Authentication.Cba.Models
{
    public class Certificate
    {
        public string UserId { get; set; }

        public string ClientCertSerialNumber { get; set; }

        [JsonIgnore]
        public X509Certificate2 ClientCertificate
        {
            get
            {
                X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                X509Certificate2Collection results = store.Certificates.Find(X509FindType.FindBySerialNumber, ClientCertSerialNumber, false);
                return results.Count > 0 ? results[0] : null;
            }
        }
    }
}
