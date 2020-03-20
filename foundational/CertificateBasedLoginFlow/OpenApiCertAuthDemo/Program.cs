// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
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
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace OpenApiCertAuthDemo
{
	class Program
	{
		[STAThread]
		static void Main()
		{
			try
			{
				RunSample().Wait();
				Console.WriteLine("\nPress enter to finish...");
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Sample terminated with exception:");
				Console.Error.WriteLine($"{ex.GetType()}: {ex.Message}");
				Console.Error.WriteLine();
				Console.Error.WriteLine(ex.ToString());
			}
			Console.ReadLine();
		}

		private async static Task RunSample()
		{
			// All settings are stored in web.config
			string authenticationUrl = ConfigurationManager.AppSettings["AuthenticationUrl"] + "/AuthnRequest";
			string appUrl = ConfigurationManager.AppSettings["AppUrl"];
			string partnerIdpUrl = ConfigurationManager.AppSettings["PartnerIdpUrl"];
			string userId = ConfigurationManager.AppSettings["UserId"];
			string appKey = ConfigurationManager.AppSettings["AppKey"];
			string appSecret = ConfigurationManager.AppSettings["AppSecret"];
			string clientCertNumber = ConfigurationManager.AppSettings["ClientCertificateSerialNumber"];
			string saxoCertNumber = ConfigurationManager.AppSettings["SaxoBankCertificateSerialNumber"];
			string clientsMeRequestUrl = ConfigurationManager.AppSettings["OpenApiBaseUrl"] + "/port/v1/clients/me";

			// Get the certificates (assumed installed on the local cert store, serial numbers are set up in the config-file)
			var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
			store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

			X509Certificate2 clientCertificate = store.Certificates.Find(X509FindType.FindBySerialNumber, clientCertNumber, false)[0];
			X509Certificate2 encryptionCertificate = store.Certificates.Find(X509FindType.FindBySerialNumber, saxoCertNumber, false)[0];

			// Parse the saml to get the authorizationCode and fetch the token
			OpenApiOAuth2TokenResponse tokenResponse = OpenApiAuthHelper.GetTokenByClientCertificate(clientCertificate, encryptionCertificate, appUrl, partnerIdpUrl, userId, appKey, appSecret, authenticationUrl).Result;

			//Initialize HTTP client and setup shared cookie container to ensure stickiness.
			InitializeHttpClient(tokenResponse);

			// Use the access token to retrieve OpenApi data from the port/clients/me endpoint
			var openApiTestData = await GetClientsMe(tokenResponse, clientsMeRequestUrl).ConfigureAwait(false);
			Console.WriteLine("The OpenApi Endpoint \"/port/v1/clients/me\" returned the following data:\n\n" + openApiTestData);
		}

		static HttpClient _httpClient;
		static CookieContainer _cookieContainer;

		private static void InitializeHttpClient(OpenApiOAuth2TokenResponse token)
		{
			// Initialize httpClient with cookie container to ensure stickiness and automatic decompression of recieved data.
			// Note that in production code this must be disposed correctly. 
			_cookieContainer = new CookieContainer();
			var clientHandler = new HttpClientHandler
			{
				CookieContainer = _cookieContainer,
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
				UseDefaultCredentials = true
			};

			_httpClient = new HttpClient(clientHandler);
			// Set the Token (and type) directly in the Authorization Header for the request
			_httpClient.DefaultRequestHeaders.Add("Authorization", $"{token.TokenType} {token.AccessToken}");
		}

		private async static Task<string> GetClientsMe(OpenApiOAuth2TokenResponse token, string clientsMeRequestUrl)
		{
			HttpResponseMessage response = await _httpClient.GetAsync(new Uri(clientsMeRequestUrl)).ConfigureAwait(false);
			return await response.Content.ReadAsStringAsync();
		}
	}
}