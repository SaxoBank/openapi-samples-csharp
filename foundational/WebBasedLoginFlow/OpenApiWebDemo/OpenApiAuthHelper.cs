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
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace OpenApiWebDemo
{
    public static class OpenApiAuthHelper
    {
        /// <summary>
        /// Retrieve the authorization code from the SAML response, and use that to get the OAuth token
        /// </summary>
        /// <param name="authenticationUrl">The URL of the authentication server.</param>
        /// <param name="appKey">The application key.</param>
        /// <param name="appSecret">The application secret.</param>
        /// <param name="samlToken">The SAML token.</param>
        public static async Task<OpenApiOAuth2TokenResponse> GetAccessToken(string authenticationUrl, string appKey, string appSecret, string samlToken)
        {
            var authorizationUrl = authenticationUrl + "/token";

            // In case you want to use the SAML token for anything other than retrieving the authorization code you also need to:
            // 1) Validate that the issuer is either https://sim.logonvalidation.net or https://live.logonvalidation.net depending on environment
            // 2) That the token's status code is "Success"
            // 3) That the token has not expired
            // 4) That the signature matches the content using the public key provided in the token
            // if (!IsValidSamlToken(saml))
            //   throw new Exception("Provided SAML token is invalid");

            // Extract the autorization code from the SAML response
            var authorizationCode = ParseAndGetAuthorizationCode(samlToken);

            // Setup the grant type & authorization code for the token service
            var requestPayload = "grant_type=authorization_code&code=" + authorizationCode;

            // Request a token, using the appKey, secret & the payload
            return await SendAuthorizationRequest(authorizationUrl, appKey, appSecret, requestPayload).ConfigureAwait(false);
        }

        public static Task<OpenApiOAuth2TokenResponse> RefreshToken(string authenticationUrl, string appKey, string appSecret, string refreshToken)
        {
            var authorizationUrl = authenticationUrl + "/token";

            // Setup the grant type & refresh token for the token service
            var requestPayload = "grant_type=refresh_token&refresh_token=" + refreshToken;
            
            // Request the new token, using the appKey, secret & the payload
            return SendAuthorizationRequest(authorizationUrl, appKey, appSecret, requestPayload);
        }

        private static async Task<OpenApiOAuth2TokenResponse> SendAuthorizationRequest(string authenticationUrl, string appKey, string appSecret, string requestPayload)
        {
            var credentials = $"{appKey}:{appSecret}";
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));

            var client = new HttpClient(new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseDefaultCredentials = true
            });
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

            using (var content = new StringContent(requestPayload))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var response = await client.PostAsync(authenticationUrl, content).ConfigureAwait(false);
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                var serializer = new DataContractJsonSerializer(typeof(OpenApiOAuth2TokenResponse));
                var tokenResponse = serializer.ReadObject(stream) as OpenApiOAuth2TokenResponse;
                return tokenResponse;
            }
        }

        private static string ParseAndGetAuthorizationCode(string saml)
        {
            var xml = new XmlDocument();
            xml.LoadXml(saml); // if the SAML is malformed, this will throw an exception

            var nsmgr = new XmlNamespaceManager(xml.NameTable);
            nsmgr.AddNamespace("samlp", "urn:oasis:names:tc:SAML:2.0:protocol");
            nsmgr.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
            var attribute = xml.SelectSingleNode("/samlp:Response/saml:Assertion/saml:AttributeStatement/saml:Attribute[@Name='AuthorizationCode']/saml:AttributeValue", nsmgr);
            return attribute?.InnerText;
        }

        /// <summary>
        /// Builds the SAML authentication request.
        /// </summary>
        /// <param name="authenticationUrl">The URL for the authentication endpoint.</param>
        /// <param name="applicationUrl">The URL for the application.</param>
        /// <param name="issuerUrl">The URL issuing the request</param>
        public static string BuildSamlRequest(string authenticationUrl, string applicationUrl, string issuerUrl)
        {
            var timestamp = $"{DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)}Z";

            return $@"
                    <samlp:AuthnRequest ID=""_{Guid.NewGuid()}"" Version=""2.0"" ForceAuthn=""false"" IsPassive=""false""
                    ProtocolBinding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" xmlns:samlp=""urn:oasis:names:tc:SAML:2.0:protocol""
                    IssueInstant=""{timestamp}"" Destination=""{authenticationUrl}"" AssertionConsumerServiceURL=""{applicationUrl}"">
                    <samlp:NameIDPolicy AllowCreate=""false"" />
                    <saml:Issuer xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion"">{issuerUrl}</saml:Issuer>
                    </samlp:AuthnRequest>";
        }
    }
}