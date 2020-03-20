// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenApiConsoleDemo
{
    public class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                RunSample().Wait();
                Console.WriteLine("\nPress enter to finish...");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Sample terminated with exception:");
                Console.Error.WriteLine($"  {ex.GetType()}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"  {ex.InnerException.GetType()}: {ex.InnerException.Message}");
            }
            Console.ReadLine();
        }

        private static async Task RunSample()
        {
            // Read and validate configuration
            var applicationUrl      = ConfigurationManager.AppSettings["AppUrl"];
            var authenticationUrl   = ConfigurationManager.AppSettings["AuthenticationUrl"];
            var appKey              = ConfigurationManager.AppSettings["AppKey"];
            var appSecret           = ConfigurationManager.AppSettings["AppSecret"];
            var apiBaseUrl          = ConfigurationManager.AppSettings["OpenApiBaseUrl"];

            var clientsMeRequestUrl = $"{apiBaseUrl}/port/v1/clients/me";

            if ((applicationUrl + authenticationUrl + appKey + appSecret + apiBaseUrl).IndexOf("#", StringComparison.CurrentCulture) >= 0)
            {
                Console.Error.WriteLine("Invalid configuration - please go to app.config and insert valid configuration values");
                return;
            }

            // Show login window with embedded browser instance to authenticate user
            // This can be avoided using the certificate based authentication feature (see CertificateBasedLoginFlow sample application)
            var window = new LoginWindow(authenticationUrl, applicationUrl);
            window.ShowDialog();

            // If the authentication process succeeds, we now have a SAML response
            if (window.SAMLResponse == null)
            {
                Console.Error.WriteLine("Authentication failed - No SAML response was received");
                return;
            }

            // Response is base 64 encoded - decode it
            var samlResponse = Encoding.UTF8.GetString(Convert.FromBase64String(window.SAMLResponse));

            // Exchange the SAML token to an Open API access token
            var tokenResponse = await OpenApiAuthHelper.GetAccessToken(authenticationUrl, appKey, appSecret, samlResponse).ConfigureAwait(false);

            // Use the access token to retrieve data from Open API
            var accessToken = $"{tokenResponse.TokenType} {tokenResponse.AccessToken}";
            var response = await GetClientsMe(accessToken, clientsMeRequestUrl).ConfigureAwait(false);
            Console.WriteLine(response);
        }

        private static async Task<string> GetClientsMe(string accessToken, string clientsMeRequestUrl)
        {
            var client = new HttpClient(new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseDefaultCredentials = true
            });
            client.DefaultRequestHeaders.Add("Authorization", accessToken);

            var response = await client.GetAsync(clientsMeRequestUrl).ConfigureAwait(false);
            Console.WriteLine($"GET {clientsMeRequestUrl} responded with {response.StatusCode}");

            return await response.Content.ReadAsStringAsync();
        }
    }
}
