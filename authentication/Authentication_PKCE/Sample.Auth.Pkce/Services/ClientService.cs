using System;
using System.IO;
using System.Net.Http;

namespace Sample.Auth.Pkce.Services
{
    public class ClientService : BaseService
    {

        /// <summary>
        /// Get client info
        /// </summary>
        /// <param name="openApiBaseUrl"></param>
        /// <param name="accessToken"></param>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        public dynamic GetClient(string openApiBaseUrl, string accessToken, string tokenType)
        {
            Uri url = new Uri(Path.Combine(openApiBaseUrl, "port/v1/clients/me"));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Version = new Version(1, 1)  // C# Framework 4 doesn't support HTTP/2, so downgrade to 1.1.
                                             // Use HTTP/2 when you app supports .NET Core!
            };
            request.Headers.Authorization = GetAuthorizationHeader(accessToken, tokenType);

            try
            {
                return Send<dynamic>(request);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException("Error requesting data from the OpenApi: " + ex.Message, ex);
            }
        }
    }
}
