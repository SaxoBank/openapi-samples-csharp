using System;
using System.Collections.Generic;
using System.Text;

namespace Sample.Authentication.Cba
{
    public class App
    {
        public string AppName { get; set; }

        public string AppKey { get; set; }

        public string AppSecret { get; set; }

        public string AuthorizationEndpoint { get; set; }

        public string TokenEndpoint { get; set; }

        public string OpenApiBaseUrl { get; set; }

        public string[] RedirectUrls { get; set; }

        public string ServiceProviderUrl { get; set; }
    }
}
