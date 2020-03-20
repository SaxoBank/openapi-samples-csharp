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
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConsoleBasedBatchingDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                RunSample().Wait();

                Console.WriteLine("End Processing Batch");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Sample terminated with exception:");
                Console.Error.WriteLine(ex);
            }
            Console.ReadLine();
        }

        private static async Task RunSample()
        {

            var accessToken    = ConfigurationManager.AppSettings["AccessToken"];
            var openApiBaseUrl = ConfigurationManager.AppSettings["OpenApiBaseUrl"];

            Console.WriteLine("Started Processing Batch...");

            var batchUrl         = $"{openApiBaseUrl}/ref/batch";
            var getCurrenciesUrl = $"{openApiBaseUrl}/ref/v1/currencies";
            var getTimezonesUrl  = $"{openApiBaseUrl}/ref/v1/timezones";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"BEARER {accessToken}");
                var content = new MultipartContent("mixed", $"batch_{Guid.NewGuid()}");

                using (var batchRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(batchUrl)))
                {
                    // Prepare contents of Urls to be batched
                    var getQuery1Content = new HttpMessageContent(new HttpRequestMessage(HttpMethod.Get, new Uri(getCurrenciesUrl)));
                    var getQuery2Content = new HttpMessageContent(new HttpRequestMessage(HttpMethod.Get, new Uri(getTimezonesUrl)));

                    content.Add(getQuery1Content);
                    content.Add(getQuery2Content);

                    // Add both the Get Request to Batch Request content
                    batchRequest.Content = content;

                    //**************************************************************************
                    // Example format of batch request from Fiddler 
                    //**************************************************************************

                    // POST http://.../../../.. HTTP/1.1
                    // Authorization: BEARER eyJhbG.....
                    // Content - Type: multipart / mixed; boundary = "batch_b94ad337-036a-423d-8524-ec47b0ce857d"
                    // Host: ......
                    // Content - Length: 385
                    // Expect: 100 -continue
                    // Connection: Keep - Alive

                    // --batch_b94ad337 - 036a - 423d - 8524 - ec47b0ce857d
                    // Content - Type: application / http; msgtype = request

                    // GET / openapi /ref/ v1 / currencies HTTP / 1.1
                    // Host: ......

                    // --batch_b94ad337 - 036a - 423d - 8524 - ec47b0ce857d
                    // Content - Type: application / http; msgtype = request

                    // GET / openapi /ref/ v1 / timezones HTTP / 1.1
                    // Host: ........

                    // --batch_b94ad337 - 036a - 423d - 8524 - ec47b0ce857d--

                    var response = await client.SendAsync(batchRequest).ConfigureAwait(false);
                    var responseContents = await response.Content.ReadAsMultipartAsync().ConfigureAwait(false);

                    // Read Responses
                    foreach (var responseContent in responseContents.Contents)
                    {
                        var result = await responseContent.ReadAsHttpResponseMessageAsync().ConfigureAwait(false);
                        var resultString = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Console.WriteLine($"Response String of Request : {resultString}");
                        Console.WriteLine("");
                    }
                }

                //**************************************************************************
                // Example format of response from Fiddler 
                //**************************************************************************

                // HTTP / 1.1 200 OK
                // Cache - Control: no - cache
                // Pragma: no - cache
                // Content - Length: 7827
                // Content - Type: multipart / mixed; boundary = "3810bf29-89fa-4f44-82a4-f02ba887de39"
                // Expires: -1
                // Server: Microsoft - IIS / 8.5
                // X - Correlation: f613d399702f4dcb9a54cd2b157019c9#123#d0970a69-9872-41ba-8ed0-d6c4b1e8075c#31
                // X-Remote - Host: ****
                // Date: Fri, 14 Oct 2016 09:40:02 GMT

                //-3810bf29 - 89fa - 4f44 - 82a4 - f02ba887de39
                // Content - Type: application / http; msgtype = response

                // HTTP / 1.1 200 OK
                // X - Correlation: f613d399702f4dcb9a54cd2b157019c9#123#d0970a69-9872-41ba-8ed0-d6c4b1e8075c#31
                // Content-Type: application / json; charset = utf - 8

                // { "Data":[{"CurrencyCode":"USD","Decimals":2,"Name":"US Dollar"},{"CurrencyCode":"GBP","Decimals":2,"Name":"British Pound"},{"CurrencyCode":"EUR","Decimals":2,"Name":"Euro"},{"CurrencyCode":"CHF","Decimals":2,"Name":"Swiss Franc"},{"CurrencyCode":"AUD","Decimals":2,"Name":"Australian Dollar"},{"CurrencyCode":"CAD","Decimals":2,"Name":"Canadian Dollar"},{"CurrencyCode":"NZD","Decimals":2,"Name":"New Zealand Dollar"},{"CurrencyCode":"JPY","Decimals":0,"Name":"Japanese Yen"},{"CurrencyCode":"DKK","Decimals":2,"Name":"Danish Krone"},{"CurrencyCode":"SEK","Decimals":2,"Name":"Swedish Krona"},{"CurrencyCode":"NOK","Decimals":2,"Name":"Norwegian Krone"},{"CurrencyCode":"ATS","Decimals":2,"Name":"Austrian Schilling"},{"CurrencyCode":"BEF","Decimals":2,"Name":"Belgian Franc"},{"CurrencyCode":"DEM","Decimals":2,"Name":"German Mark"},{"CurrencyCode":"ESP","Decimals":2,"Name":"Spanish Peseta"},{"CurrencyCode":"FIM","Decimals":2,"Name":"Finnish Mark"},{"CurrencyCode":"FRF","Decimals":2,"Name":"French Franc"},{"CurrencyCode":"GRD","Decimals":2,"Name":"Greek Drachma"},{"CurrencyCode":"IEP","Decimals":2,"Name":"Irish Punt"},{"CurrencyCode":"ITL","Decimals":2,"Name":"Italian Lira"},{"CurrencyCode":"LUF","Decimals":2,"Name":"Luxembourg Franc"},{"CurrencyCode":"NLG","Decimals":2,"Name":"Dutch Guilder"},{"CurrencyCode":"PTE","Decimals":2,"Name":"Portugese Escudo"},{"CurrencyCode":"CZK","Decimals":2,"Name":"Czech Koruna"},{"CurrencyCode":"ISK","Decimals":2,"Name":"Iceland Krona"},{"CurrencyCode":"PLN","Decimals":2,"Name":"Polish Zloty"},{"CurrencyCode":"SGD","Decimals":2,"Name":"Singapore Dollar"},{"CurrencyCode":"LTL","Decimals":2,"Name":"Lithuanian Litas"},{"CurrencyCode":"EEK","Decimals":2,"Name":"Estonian Kroon"},{"CurrencyCode":"HRK","Decimals":2,"Name":"Croatian Kuna"},{"CurrencyCode":"LVL","Decimals":2,"Name":"Latvian Lats"},{"CurrencyCode":"SIT","Decimals":2,"Name":"Slovenian Tolar"},{"CurrencyCode":"SKK","Decimals":2,"Name":"Slovak Koruna"},{"CurrencyCode":"AED","Decimals":2,"Name":"UAE Dirham"},{"CurrencyCode":"BHD","Decimals":2,"Name":"Bahrain Dinar"},{"CurrencyCode":"BRL","Decimals":2,"Name":"Brazilian Real"}]}
                // --3810bf29-89fa-4f44-82a4-f02ba887de39
                // Content-Type: application/http; msgtype=response

                // HTTP/1.1 200 OK
                // X-Correlation: f613d399702f4dcb9a54cd2b157019c9#123#d0970a69-9872-41ba-8ed0-d6c4b1e8075c#31
                // Content-Type: application/json; charset=utf-8

                // {"Data":[{"DisplayName":"GMT a.k.a. UTC","TimeZoneId":0,"ZoneName":"Etc/UTC"},{"DisplayName":"British Time","TimeZoneId":1,"ZoneName":"Europe/London"},{"DisplayName":"Singapore Time","TimeZoneId":2,"ZoneName":"Asia/Singapore"},{"DisplayName":"US Eastern Time","TimeZoneId":3,"ZoneName":"America/New_York"},{"DisplayName":"Central European Time","TimeZoneId":4,"ZoneName":"Europe/Paris"},{"DisplayName":"US Central Time","TimeZoneId":5,"ZoneName":"America/Chicago"},{"DisplayName":"US Pacific Time","TimeZoneId":6,"ZoneName":"America/Los_Angeles"},{"DisplayName":"Hong Kong Time","TimeZoneId":7,"ZoneName":"Asia/Hong_Kong"},{"DisplayName":"Sydney Time","TimeZoneId":8,"ZoneName":"Australia/Sydney"},{"DisplayName":"New Zealand Time","TimeZoneId":9,"ZoneName":"Pacific/Auckland"},{"DisplayName":"GMT +9 No Daylight S.","TimeZoneId":10,"ZoneName":"Etc/GMT-9"},{"DisplayName":"GMT +7 No Daylight S.","TimeZoneId":11,"ZoneName":"Etc/GMT-7"},{"DisplayName":"Russia Zone 2","TimeZoneId":12,"ZoneName":"Europe/Moscow"},{"DisplayName":"GMT +8 No Daylight S.","TimeZoneId":13,"ZoneName":"Etc/GMT-8"},{"DisplayName":"Eastern European Time","TimeZoneId":14,"ZoneName":"Europe/Helsinki"},{"DisplayName":"Hawaii Time","TimeZoneId":15,"ZoneName":"Pacific/Honolulu"},{"DisplayName":"South African Time","TimeZoneId":16,"ZoneName":"Africa/Johannesburg"},{"DisplayName":"GMT +10 No Daylight S.","TimeZoneId":17,"ZoneName":"Etc/GMT-10"},{"DisplayName":"GMT+3","TimeZoneId":18,"ZoneName":"Etc/GMT-3"},{"DisplayName":"GMT+4","TimeZoneId":19,"ZoneName":"Etc/GMT-4"},{"DisplayName":"Brazil Sao Paulo","TimeZoneId":20,"ZoneName":"America/Sao_Paulo"},{"DisplayName":"Africa/Cairo","TimeZoneId":33,"ZoneName":"Africa/Cairo"},{"DisplayName":"America/Caracas","TimeZoneId":104,"ZoneName":"America/Caracas"},{"DisplayName":"America/Halifax","TimeZoneId":130,"ZoneName":"America/Halifax"},{"DisplayName":"America/La_Paz","TimeZoneId":147,"ZoneName":"America/La_Paz"},{"DisplayName":"Asia/Kolkata","TimeZoneId":256,"ZoneName":"Asia/Kolkata"},{"DisplayName":"Atlantic/Azores","TimeZoneId":298,"ZoneName":"Atlantic/Azores"}]}
                // --3810bf29-89fa-4f44-82a4-f02ba887de39--
            }
        }
    }
}
