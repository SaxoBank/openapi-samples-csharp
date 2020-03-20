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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using mshtml;

namespace OpenApiConsoleDemo
{
    /// <summary>
    /// This window hosts a webbrowser instance, that will send the SAML request and eventually get the SAML reponse, which can be used to get an Open API access token.
    /// </summary>
    internal sealed class LoginWindow : Window
    {
        private readonly string _authenticationUrl;
        private readonly string _applicationUrl;

        private readonly WebBrowser _browser;

        public LoginWindow(string authenticationUrl, string applicationUrl)
        {
            if (authenticationUrl == null) throw new ArgumentNullException(nameof(authenticationUrl));
            if (applicationUrl == null) throw new ArgumentNullException(nameof(applicationUrl));

            _authenticationUrl = authenticationUrl;
            _applicationUrl = applicationUrl;

            Height = 500;
            Width = 600;
            ShowInTaskbar = false;
            Topmost = true;

            _browser = new WebBrowser();
            _browser.LoadCompleted += OnLoadCompleted;
            AddChild(_browser);
        }

        protected override void OnActivated(EventArgs e)
        {
            Activate();
            SendSamlRequest();
            base.OnActivated(e);
        }

        /// <summary>
        /// For each page that the browser loads, we look for one that fulfills all the following criteria:
        ///  - Has a meta-element named "Application-State" (that should consist of a ";"-separated list of "key=val" pairs)
        ///  - The content of the application-state contains "service=IDP" to tell that the response is coming from the identity provider (IDP)
        ///  - The content of the application-state contains "authenticated=true" to tell that the authentication went well 
        ///  - The content of the application-state contains "state=token" to signal that the reponse contains the SAML token response
        /// If the above is true, the response contains the SAML token response as an attribute on the BODY tag named SSO_SAML2_TOKEN.
        /// </summary>
        private void OnLoadCompleted(object sender, NavigationEventArgs args)
        {
            Console.WriteLine("OnLoadCompleted: {0}", args.Uri);
            try
            {
                HTMLDocumentClass dom = (HTMLDocumentClass)(_browser.Document);

                // First, look for the application-state meta element
                IHTMLElementCollection applicationStateElementCollection = dom.getElementsByName("Application-State");

                // If there is no application-state, it is not the right page
                if (applicationStateElementCollection.length < 1)
                    return;

                // Application state looks like this: <meta name="Application-State" content="service=IDP;federated=False;env=Test;state=Ok;authenticated=True;">
                string applicationState = ((HTMLMetaElement)applicationStateElementCollection.item(0)).content;

                // Split at ";" sepearator
                string[] applicationStateElements = applicationState.Split(';');

                // Look for the "state=idp" - this means that the process is finished
                // Without checking this, we don't know whether the authentication failed or just haven't gotten there yet
                if (!applicationStateElements.Any(s => (s.Equals("service=idp", StringComparison.OrdinalIgnoreCase))))
                {
                    // This is not the IDP responding, probably a page earlier in the login flow - just skip it
                    return;
                }

                // This is the IDP response page. Check if the list of values contains authenticated=true
                if (applicationStateElements.Any(s => (s.Equals("authenticated=true", StringComparison.OrdinalIgnoreCase))) &&
                    applicationStateElements.Any(s => (s.Equals("state=token", StringComparison.OrdinalIgnoreCase))))
                {
                    // On the final page, the SAML-Response is on an attribute on the body element, named SSO_SAML2_TOKEN
                    HTMLBody bodyElement = ((HTMLBody)dom.getElementsByTagName("body").item(0));
                    object attribute = bodyElement.getAttribute("SSO_SAML2_TOKEN");

                    SAMLResponse = attribute.ToString();
                    DialogResult = true;
                }
                else
                {
                    DialogResult = false;
                }
                Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Close();
            }
        }

        /// <summary>
        /// Builds and sends the initial SAML authentication request to the single sign on server (SSO).
        /// </summary>
        private void SendSamlRequest()
        {
            // The SAML request must be sent as "x-www-form-urlencoded"
            const string contentTypeHeader = "Content-Type: application/x-www-form-urlencoded";

            // Send the request to the AuthnRequest endpoint of the IDP
            var authenticationUrl = _authenticationUrl + "/AuthnRequest";

            // Build the SAML request
            var samlRequest = BuildSamlRequest(authenticationUrl, _applicationUrl, _applicationUrl);

            // Encode as base 64
            var base64Saml = Convert.ToBase64String(Encoding.UTF8.GetBytes(samlRequest));

            // Send the request as a POST parameter named "SAMLRequest" (and urlencode to comply with "x-www-form-urlencoded")
            byte[] postData = Encoding.ASCII.GetBytes("SAMLRequest=" + HttpUtility.UrlEncode(base64Saml));

            // Send the request
            _browser.Navigate(authenticationUrl, null, postData, contentTypeHeader);
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

        public string SAMLResponse { get; private set; }
    }
}