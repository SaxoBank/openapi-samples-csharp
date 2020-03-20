using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Sample.Authentication.Cba
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
                var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                var results = store.Certificates.Find(X509FindType.FindBySerialNumber, ClientCertSerialNumber, false);
                return results.Count > 0 ? results[0] : null;
            }
        }
    }
}
