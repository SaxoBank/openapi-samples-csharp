using OpenAPI.Models;
using Sample.Authentication.Server.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Sample.Authentication.Server.Services
{
    public class AuthService: BaseService
    {
        /// <summary>
        /// Create the login page URL
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public string GetAuthenticationRequest(App app)
        {
            string authUrl = app.AuthorizationEndpoint;
            string redirectUri = Uri.EscapeDataString(app.RedirectUrls[0]);
            return string.Format("{0}?response_type=code&client_id={1}&state={2}&redirect_uri={3}", 
                authUrl, app.AppKey, Constants.State, redirectUri);
        }

        /// <summary>
        /// Get access token by authentication code
        /// </summary>
        /// <param name="app"></param>
        /// <param name="authCode"></param>
        /// <returns></returns>
        public Token GetToken(App app, string authCode)
        {
            // Create request
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, app.TokenEndpoint)
            {
                Version = new Version(2, 0),  // Make sure HTTP/2 is used, if available
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", authCode },
                    { "redirect_uri", app.RedirectUrls[0]}
                })
            };
            request.Headers.Authorization = GetBasicAuthHeader(app.AppKey, app.AppSecret);
            try
            {
                return Send<Token>(request);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException("Error requesting access token using: " + authCode, ex);
            }
        }

        /// <summary>
        /// Refresh token before expiration
        /// </summary>
        /// <param name="app"></param>
        /// <param name="refreshToken"></param>
        /// <returns></returns>
        public Token RefreshToken(App app, string refreshToken)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, app.TokenEndpoint)
            {
                Version = new Version(2, 0),  // Make sure HTTP/2 is used, if available
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "refresh_token", refreshToken },
                    { "grant_type", "refresh_token" }
                })
            };
            request.Headers.Authorization = GetBasicAuthHeader(app.AppKey, app.AppSecret);
            try
            {
                return Send<Token>(request);
            }
            catch(Exception ex)
            {
                throw new HttpRequestException("Error requesting access token using refresh token " + refreshToken, ex);
            }
        }

        /// <summary>
        /// Encoding as the Basic method
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="secret"></param>
        /// <returns></returns>
        private static AuthenticationHeaderValue GetBasicAuthHeader(string clientId, string secret)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
            return new AuthenticationHeaderValue("Basic", encoded);
        }
    }
}
