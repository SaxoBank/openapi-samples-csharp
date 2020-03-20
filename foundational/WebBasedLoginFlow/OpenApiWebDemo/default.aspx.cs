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
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace OpenApiWebDemo
{
    public partial class DefaultPage : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Script = "";

            OpenApiOAuth2TokenResponse token;

            // The response from single sign on (SSO) is returned in the SAMLResponse form variable
            var samlResponse = Request.Form["SAMLResponse"];

            if (samlResponse != null)
            {
                // If we have a response, base64 decode it, and get a token from the response
                var url = AppCache.AuthenticationUrl;
                var appKey = AppCache.AppKey;
                var appSecret = AppCache.AppSecret;
                var samlToken = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));
                token = OpenApiAuthHelper.GetAccessToken(url, appKey, appSecret, samlToken).Result;

                AppCache.AccessToken = token;
            }
            else
            {
                token = AppCache.AccessToken;
                if (token == null)
                {
                    // If we have no token, send a SAML authentication request to SSO
                    // This is a POST request, so in this example it is done using a hidden form on the page, that auto-submits
                    SetupAuthenticationForm();
                    return;
                }
            }

            try
            {
                // Use the token to request data from OpenApi and set the data & token values for rendering into the page
                var accessToken = $"{token.TokenType} {token.AccessToken}";
                OpenApiResponseData = ApiClient.GetClientsMe(accessToken, AppCache.OpenApiBaseUrl).Result;
                TokenValue = token.AccessToken;
                TokenType = token.TokenType;
            }
            catch (WebException webException)
            {
                // In case of an error, just show info about the error in the Response panel of the page
                ShowWebException(webException);
                TokenValue = token.AccessToken;
                TokenType = token.TokenType;
                AppCache.AccessToken = null;
            }
        }

        private void ShowWebException(WebException webException)
        {
            OpenApiResponseData = "Error: " + webException.Message;
            using (var responseStream = webException.Response.GetResponseStream())
            {
                if (responseStream != null)
                {
                    using (var reader = new StreamReader(responseStream))
                    {
                        OpenApiResponseData += "\n" + reader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// Sets up the hidden form on the page, to POST an authentication SAML request
        /// </summary>
        private void SetupAuthenticationForm()
        {
            AuthenticationUrl = AppCache.AuthenticationUrl + "/AuthnRequest";

            // Make the SAML request that attempts to authenticate (and redirects to the login-form if necessary)
            var samlRequest = OpenApiAuthHelper.BuildSamlRequest(HttpContext.Current.Request.Url.AbsoluteUri, Request.Url.AbsoluteUri, AuthenticationUrl);
            var base64Saml = Convert.ToBase64String(Encoding.UTF8.GetBytes(samlRequest));

            SamlRequest = base64Saml; // send the request as a base 64 encoded post-parameter (as a hidden field in the form)
            Script = "document.forms['samlForm'].submit();";
            // Setup the auto-submit by inserting this into a script-tag right after the form
        }

        // Model for UI
        protected string OpenApiResponseData { get; set; }
        
        protected string AuthenticationUrl { get; private set; }

        protected string SamlRequest { get; private set; }

        protected string Script { get; private set; }

        protected string OpenApiBaseUrl => AppCache.OpenApiBaseUrl;

        protected string TokenType { get; private set; }

        protected string TokenValue { get; private set; }
    }
}