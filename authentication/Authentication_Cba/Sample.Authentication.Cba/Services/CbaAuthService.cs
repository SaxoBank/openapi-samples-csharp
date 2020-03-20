using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Sample.Authentication.Cba.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Sample.Authentication.Cba
{
    public class CbaAuthService : BaseService
    {
        public const string JwtOAuthPersonalCertificate = "urn:saxobank:oauth:grant-type:personal-jwt";

        /// <summary>
        /// Get token by presenting a properly signed JWT
        /// </summary>
        /// <param name="configuration">General OpenAPI configuration data.</param>
        /// <param name="userId">Id of the user for which a token should be generated.</param>
        /// where the user can be any user within a hierarchy under the owner of the certificate</param>
        /// <returns></returns>
        public Token GetTokenByOAuthCba(App app, Certificate certificate)
        {
            if (certificate.ClientCertificate == null)
                throw new InvalidOperationException($"Invalid Certificate {certificate.ClientCertSerialNumber}");

            var grant_type = JwtOAuthPersonalCertificate;
            var assertion = CreateAssertion(app, certificate);
            var request = new HttpRequestMessage(HttpMethod.Post, app.TokenEndpoint);
            request.Headers.Authorization = GetBasicAuthHeader(app.AppKey, app.AppSecret);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "assertion", assertion },
                { "grant_type", grant_type }
            });

            try
            {
                return Send<Token>(request);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException($"Error requesting access token using: {ex.Message}", ex);
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
            catch (Exception ex)
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

        /// <summary>
        /// Create JWT
        /// Refer to https://www.developer.saxo/openapi/learn/oauth-certificate-based-authentication
        /// </summary>
        /// <param name="spUrl"></param>
        /// <param name="userId"></param>
        /// <param name="appKey"></param>
        /// <param name="idpUrl"></param>
        /// <param name="signCert"></param>
        /// <returns></returns>
        private string CreateAssertion(App app, Certificate certificate)
        {
            var issuer = app.AppKey;
            var audience = app.AuthorizationEndpoint;
            var appUrl = app.ServiceProviderUrl;

            var claims = new List<Claim>();
            claims.Add(new Claim("spurl", appUrl));
            claims.Add(new Claim("sub", certificate.UserId));
            claims.Add(new Claim("iss", issuer));
            claims.Add(new Claim("aud", audience));
            claims.Add(new Claim("exp", (DateTime.UtcNow.AddHours(5) - new DateTime(1970, 1, 1)).TotalSeconds.ToString()));

            var key = new X509SecurityKey(certificate.ClientCertificate);
            var header = new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
            header.Add("x5t", certificate.ClientCertificate.Thumbprint);

            var jsonWebToken = new JwtSecurityToken(header, new JwtPayload(issuer, audience, claims, null, null));
            return new JwtSecurityTokenHandler().WriteToken(jsonWebToken);
        }

    }
}
