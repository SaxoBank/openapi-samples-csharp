// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpenApiWebDemo
{
    public class ApiClient
    {
        public static async Task<string> GetClientsMe(string accessToken, string baseUrl)
        {
            var uri = $"{baseUrl}/port/v1/clients/me";

            var client = new HttpClient(new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseDefaultCredentials = true
            });
            client.DefaultRequestHeaders.Add("Authorization", accessToken);

            var response = await client.GetAsync(uri).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
    }
}