using OpenAPI.Models;
using Sample.Authentication.Server.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Sample.Authentication.Server.Services
{
    public class AuthService: BaseService
    {
        /// <summary>
        /// Create the login page url
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public string GetAuthenticationRequest(App app)
        {
            var authUrl = app.AuthorizationEndpoint;
            var state = GetRandomString();  
            var redirectUri = Uri.EscapeDataString(app.RedirectUrls[0]);

            return string.Format("{0}?response_type=code&client_id={1}&state={2}&redirect_uri={3}", authUrl, app.AppKey, state, redirectUri);
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
            var tokenUrl = app.TokenEndpoint;
            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            request.Headers.Authorization = GetBasicAuthHeader(app.AppKey, app.AppSecret);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", authCode },
                { "redirect_uri", app.RedirectUrls[0]}
            });

            try
            {
                return Send<Token>(request);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException("Error requesting access token using:" + authCode, ex);
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
            var authenticationUrl = app.TokenEndpoint;
            var appKey = app.AppKey;
            var secret = app.AppSecret;

            var request = new HttpRequestMessage(HttpMethod.Post, authenticationUrl);
            request.Headers.Authorization = GetBasicAuthHeader(appKey, secret);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" }
            });

            try
            {
                return Send<Token>(request);
            }
            catch(Exception ex)
            {
                throw new HttpRequestException("Error requesting access token using refresh token" + refreshToken, ex);
            }
        }

        /// <summary>
        /// Encoding as the Basic method
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="secret"></param>
        /// <returns></returns>
        private AuthenticationHeaderValue GetBasicAuthHeader(string clientId, string secret)
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
            return new AuthenticationHeaderValue("Basic", encoded);
        }
     
        public static string GetRandomString()
        {
            using (var rnd = new RNGCryptoServiceProvider())
            {
                var buf = new byte[8]; 
                rnd.GetBytes(buf);
                return Convert.ToBase64String(buf);
            }
        }
    }
}
