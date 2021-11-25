using System;
using System.Net.Http;

namespace Sample.Authentication.Cba.Services
{
    public class ClientService: BaseService
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
            Uri url = new Uri(new Uri(openApiBaseUrl), "port/v1/clients/me");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Version = new Version(2, 0)  // Make sure HTTP/2 is used, if available
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
