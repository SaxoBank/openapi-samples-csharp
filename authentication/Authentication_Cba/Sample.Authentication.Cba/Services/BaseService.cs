﻿using Newtonsoft.Json;
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
        protected AuthenticationHeaderValue GetAuthorizationHeader(string accessToken, string tokenType)
        {
            return new AuthenticationHeaderValue(tokenType, accessToken);
        }
        
        /// <summary>
        /// Send out token request
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected T Send<T>(HttpRequestMessage request)
        {
            string content = string.Empty;

            try
            {
                using (HttpClient httpClient = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false, UseCookies = false }))
                {
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
