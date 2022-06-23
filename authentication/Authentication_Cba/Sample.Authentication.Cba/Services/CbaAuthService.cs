using Microsoft.IdentityModel.Tokens;
using Sample.Authentication.Cba.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace Sample.Authentication.Cba.Services
{
    public class CbaAuthService : BaseService
    {
        /// <summary>
        /// Get token by presenting a properly signed JWT
        /// where the user can be any user within a hierarchy under the owner of the certificate
        /// </summary>
        /// <returns></returns>
        public Token GetTokenByOAuthCba(App app, Certificate certificate)
        {
            if (certificate.ClientCertificate == null)
                throw new InvalidOperationException($"Invalid or unknown Certificate {certificate.ClientCertSerialNumber} - did you import the certificate to the Personal store?");

            string assertion = CreateAssertion(app, certificate);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, app.TokenEndpoint)
            {
                Version = new Version(2, 0),  // Make sure HTTP/2 is used, if available
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "assertion", assertion },
                    { "grant_type", "urn:saxobank:oauth:grant-type:personal-jwt" }
                })
            };
            request.Headers.Authorization = GetBasicAuthHeader(app.AppKey, app.AppSecret);
            try
            {
                return Send<Token>(request);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException($"Error requesting access token (is the app configured correctly in App.json?):\n{ex.Message}", ex);
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
            // Should you refresh the token, or just generate a new one?
            // Well, if you generate a new token, you create a new session and the streaming session must be recreated.
            // And if you refresh the token, the session is extended, keeping up the streaming session.
            // So it is recommended to refresh the token.
            //
            // If you run into a 401 NotAuthenticated, this might be caused by not accepting the terms and conditions.
            // To fix this, you must use this app once with the Authorization Code Flow for your userId and accept the Disclaimer after signing in.
            // You can use this URL, replacing the appKey with yours (add a new redirect URL http://127.0.0.1/):
            // https://sim.logonvalidation.net/authorize?client_id=1234b8587cd146249e13dc4ab2f9f806&response_type=code&redirect_uri=http%3A%2F%2F127.0.0.1%2F
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
            catch (Exception ex)
            {
                throw new HttpRequestException($"Error requesting access token using refresh token: {refreshToken}\n{ex.Message}", ex);
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

        /// <summary>
        /// Create JWT
        /// Refer to https://www.developer.saxo/openapi/learn/oauth-certificate-based-authentication
        /// </summary>
        /// <returns></returns>
        private static string CreateAssertion(App app, Certificate certificate)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim("spurl", app.ServiceProviderUrl),
                new Claim("sub", certificate.UserId),  // The user who has created the certificate (in Chrome)
                new Claim("iss", app.AppKey),
                new Claim("aud", app.TokenEndpoint),
                // Lifetime of assertion - keep this short, the token is generated directly afterwards:
                new Claim("exp", (DateTime.UtcNow.AddMinutes(1) - new DateTime(1970, 1, 1)).TotalSeconds.ToString(CultureInfo.InvariantCulture))
            };
            Console.WriteLine("Assertion claims:\n" + string.Join("\n", claims));
            X509SecurityKey key = new X509SecurityKey(certificate.ClientCertificate);
            JwtHeader header = new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.RsaSha256))
            {
                {"x5t", certificate.ClientCertificate.Thumbprint}
            };

            JwtSecurityToken jsonWebToken = new JwtSecurityToken(header, new JwtPayload(app.AppKey, app.TokenEndpoint, claims, null, null));
            // Retrieve a "Keyset does not exist" exception here? Make sure the operating Windows-user has access to this certificate (on Local Machine)!
            // Check this in the certificate manager with "All Tasks" / "Manage Private Keys" and give your user Full control.
            // Alternatively, you can run Visual Studio as Administrator.
            string assertion = new JwtSecurityTokenHandler().WriteToken(jsonWebToken);
#if DEBUG
            Console.WriteLine("Generated assertion:\n" + assertion);  // You can verify this token on https://jwt.io/
#endif
            return assertion;
        }

    }
}
