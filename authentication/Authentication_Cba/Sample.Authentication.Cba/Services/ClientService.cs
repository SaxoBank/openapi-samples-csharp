using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

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
            var url = new Uri(new Uri(openApiBaseUrl), "port/v1/clients/me");

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = GetAuthorizationHeader(accessToken, tokenType);

            try
            {
                return Send<dynamic>(request);
            }
            catch (Exception ex)
            {
                throw new HttpRequestException("Error requesting client", ex);
            }
        }
    }
}
