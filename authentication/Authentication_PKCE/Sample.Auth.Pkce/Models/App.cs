using System;
using System.Collections.Generic;
using System.Text;

namespace Sample.Auth.Pkce.Models
{
    public class App
    {
        public string AppName { get; set; }

        public string AppKey { get; set; }

        public string CodeVerifier { get; set; }

        public string AuthorizationEndpoint { get; set; }

        public string TokenEndpoint { get; set; }

        public string GrantType { get; set; }

        public string OpenApiBaseUrl { get; set; }

        public string[] RedirectUrls { get; set; }
    }
}
