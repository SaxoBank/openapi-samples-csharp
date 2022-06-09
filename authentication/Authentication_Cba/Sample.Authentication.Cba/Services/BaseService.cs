using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Sample.Authentication.Cba.Services
{
    public abstract class BaseService
    {
        /// <summary>
        /// Get Authorization header
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        protected static AuthenticationHeaderValue GetAuthorizationHeader(string accessToken, string tokenType)
        {
            return new AuthenticationHeaderValue(tokenType, accessToken);
        }
        
        /// <summary>
        /// Send out token and API requests
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected static T Send<T>(HttpRequestMessage request)
        {
            string content = string.Empty;
#if DEBUG
            Console.WriteLine("Processing request:\n" + request);  // This request contains a token. Only log this for demo and debugging!
#endif
            try
            {
                using (HttpClient httpClient = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false, UseCookies = false }))
                {
                    // Disable Expect: 100 Continue according to https://www.developer.saxo/openapi/learn/openapi-request-response
                    // In our experience the same two-step process has been difficult to get to work reliable, especially as we support clients world wide, 
                    // who connect to us through a multitude of network gateways and proxies.We also find that the actual bandwidth savings for the majority of API requests are limited, 
                    // since most requests are quite small.
                    // We therefore strongly recommend against using the Expect:100 - Continue header, and expect you to make sure your client library does not rely on this mechanism.
                    httpClient.DefaultRequestHeaders.ExpectContinue = false;
                    HttpResponseMessage res = httpClient.SendAsync(request).Result;
                    content = res.Content.ReadAsStringAsync().Result;
                    res.EnsureSuccessStatusCode();

                    return JsonConvert.DeserializeObject<T>(content);
                }
            }
            catch (Exception ex)
            {
                throw new HttpRequestException(ex.Message + content, ex);
            }
        }
    }
}
