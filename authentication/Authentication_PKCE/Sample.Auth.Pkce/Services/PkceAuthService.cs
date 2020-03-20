
using Microsoft.IdentityModel.Tokens;
using Sample.Auth.Pkce.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

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
            var authUrl = app.AuthorizationEndpoint;
            var redirectUri = Uri.EscapeDataString(app.RedirectUrls[0]);
            var state = _randomService.GetRandomString(8);
            app.CodeVerifier = _randomService.GetRandomString(43);
            var codeChallengeMethod = "S256";
            var codeChallenge = GetCodeChallenge(app.CodeVerifier);

            return string.Format("{0}?response_type=code&client_id={1}&code_verifier={2}&redirect_uri={3}&code_challenge_method={4}&code_challenge={5}&state={6}", 
                            authUrl, app.AppKey, app.CodeVerifier, redirectUri, codeChallengeMethod, codeChallenge, state);
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
            var tokenUrl = app.TokenEndpoint;
            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);

            // https://www.developer.saxo/openapi/learn/oauth-authorization-code-grant-pkce
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", authCode },
                { "client_id", app.AppKey},
                { "code_verifier", app.CodeVerifier},
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
            var codeVerifier = app.CodeVerifier;

            var request = new HttpRequestMessage(HttpMethod.Post, authenticationUrl);
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
        /// BASE64URL-ENCODE(SHA256(ASCII(code_verifier)))
        /// </summary>
        /// <param name="codeVerifier"></param>
        /// <returns></returns>
        private string GetCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.ASCII.GetBytes(codeVerifier);
                var challengeBytes = sha256.ComputeHash(bytes);
                return Base64UrlEncoder.Encode(challengeBytes);
            }
        }

    }
}
