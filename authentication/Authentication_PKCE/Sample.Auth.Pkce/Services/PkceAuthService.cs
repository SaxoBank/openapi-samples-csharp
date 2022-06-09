using Microsoft.IdentityModel.Tokens;
using Sample.Auth.Pkce.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Sample.Auth.Pkce.Services
{
    public class PkceAuthService: BaseService
    {
        private RandomStringBuilder _randomService = new RandomStringBuilder();

        /// <summary>
        /// Create the login page url for PKCE
        /// Refer to the "Your OpenAPI Application" section of https://www.developer.saxo/openapi/learn/oauth-authorization-code-grant-pkce
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public string GetAuthenticationRequest(App app)
        {
            string authUrl = app.AuthorizationEndpoint;
            string redirectUri = Uri.EscapeDataString(app.RedirectUrls[0]);
            string state = _randomService.GetRandomString(8);
            app.CodeVerifier = _randomService.GetRandomString(43);
            string codeChallengeMethod = "S256";
            string codeChallenge = GetCodeChallenge(app.CodeVerifier);

            return string.Format("{0}?response_type=code&client_id={1}&redirect_uri={2}&code_challenge_method={3}&code_challenge={4}&state={5}", 
                            authUrl, app.AppKey, redirectUri, codeChallengeMethod, codeChallenge, state);
        }

        /// <summary>
        /// Get access token by authentication code
        /// Refer to the "Access Token Request" section of https://www.developer.saxo/openapi/learn/oauth-authorization-code-grant-pkce
        /// </summary>
        /// <param name="app"></param>
        /// <param name="authCode"></param>
        /// <returns></returns>
        public Token GetToken(App app, string authCode)
        {
            // Create request
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, app.TokenEndpoint)
            {
                Version = new Version(1, 1),  // C# Framework 4 doesn't support HTTP/2, so downgrade to 1.1.
                                              // Use HTTP/2 when you app supports .NET Core!
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                            {
                                // https://www.developer.saxo/openapi/learn/oauth-authorization-code-grant-pkce
                                { "grant_type", "authorization_code" },
                                { "code", authCode },
                                { "client_id", app.AppKey},
                                { "code_verifier", app.CodeVerifier},
                                { "redirect_uri", app.RedirectUrls[0]}
                            })
            };

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
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, app.TokenEndpoint);
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
                throw new HttpRequestException("Error requesting access token using refresh token " + refreshToken, ex);
            }
        }

        /// <summary>
        /// BASE64URL-ENCODE(SHA256(ASCII(code_verifier)))
        /// </summary>
        /// <param name="codeVerifier"></param>
        /// <returns></returns>
        private string GetCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.ASCII.GetBytes(codeVerifier);
                byte[] challengeBytes = sha256.ComputeHash(bytes);
                return Base64UrlEncoder.Encode(challengeBytes);
            }
        }

    }
}
