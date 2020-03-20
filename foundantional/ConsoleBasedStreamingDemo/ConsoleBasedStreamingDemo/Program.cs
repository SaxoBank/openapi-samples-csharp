// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using Microsoft.AspNet.SignalR.Client;
using System;
using System.Threading;
using Timer = System.Timers.Timer;
using static System.FormattableString;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Configuration;
using System.Text;
using Newtonsoft.Json;
using System.Globalization;
using Microsoft.AspNet.SignalR.Client.Http;
using Microsoft.AspNet.SignalR.Client.Transports;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace ConsoleBasedStreamingDemo
{
    // -----------------------------------------------------------------------------------------
    // This sample demonstrates how to
    //
    // - ensure server stickiness when using Open API
    // - create a streaming connection using the SignalR .NET client library
    // - set up streaming subscriptions
    // - wait for the data snapshot before applying streaming updates
    // - apply streaming updates to snapshots
    // - handle partial messages
    // - handle subscription reset messages
    // - handle subscriptions heartbeats
    // - monitor subscription activity and react correctly to inactivity
    // -----------------------------------------------------------------------------------------
    class Program : IDisposable
    {
        string _contextId;

        #region Subscription management

        /// <summary>Maps reference ids to subscriptions.</summary>
        ConcurrentDictionary<string, InfoPriceSubscription> _subscriptions = new ConcurrentDictionary<string, InfoPriceSubscription>();

        /// <summary>Sequence number for generating new reference ids.</summary>
        int _nextSubscriptionReferenceId;

        /// <summary>
        /// Creates a new reference id.
        /// </summary>
        string GetReferenceId(string prefix)
        {
            var sequenceNo = Interlocked.Increment(ref _nextSubscriptionReferenceId);
            return Invariant($"{prefix}{sequenceNo}");
        }

        InfoPriceSubscription FindSubscription(string referenceId)
        {
            InfoPriceSubscription subscription;
            if (!_subscriptions.TryGetValue(referenceId, out subscription))
                return null;
            return subscription;
        }

        /// <summary>
        /// Creates a number of info price subscriptions.
        /// </summary>
        Task CreateSubscriptions()
        {
            return Task.WhenAll(
                CreateSubscription(GetReferenceId("infoprice"), "FxSpot", new[] { 21 }),
                CreateSubscription(GetReferenceId("infoprice"), "FxSpot", new[] { 22 })
                /* More subscriptions can be added here */);
        }

        /// <summary>
        /// Creates a single info price subscription.
        /// </summary>
        async Task CreateSubscription(string referenceId, string assetType, IEnumerable<int> uics)
        {
            // ----------------------------------------------------------------------------
            // The message handler functions look up subscriptions when they receive
            // streaming messages. This may happen before the  snapshot is downloaded so
            // it is necessary to add the subscription before requesting the snapshot.
            // ----------------------------------------------------------------------------
            var subscription = new InfoPriceSubscription(referenceId, uics, assetType);
            if (!_subscriptions.TryAdd(referenceId, subscription))
            {
                Console.Error.WriteLine($"Subscription already exists: {referenceId}");
                return;
            }

            // Create subscription on server
            var requestData = new
            {
                // Arguments that are specific to info price subscriptions
                Arguments = new
                {
                    AssetType = assetType,
                    Uics = string.Join(",", uics)
                },

                ContextId = _contextId,
                ReferenceId = referenceId,
                RefreshRate = 1000
            };

            var requestBody = JsonConvert.SerializeObject(requestData);
            using (var content = new StringContent(requestBody, Encoding.UTF8, "application/json"))
            {
                using (var response = await _httpClient.PostAsync(SubscriptionUrl, content))
                {
                    response.EnsureSuccessStatusCode();

                    // Parse the JSON response
                    var responseString = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseString);

                    // Extract inactivity timeout, i.e. the client's tolerance for lack of data or heartbeats
                    var inactivityTimeout = (int)json["InactivityTimeout"];
                    subscription.InactivityTimeout = TimeSpan.FromSeconds(inactivityTimeout);

                    // Extract the data snapshot and set it on the subscription
                    var snapshot = json["Snapshot"]["Data"];
                    subscription.SetSnapshot(snapshot);

                    // Update the subscription's last activity time
                    subscription.UpdateActivity();

                    Console.WriteLine($"[{referenceId}]: Snapshot received");
                }
            }
        }

        /// <summary>
        /// Deletes a specific subscription specified by a reference id.
        /// </summary>
        /// <remarks>
        /// The subscription object is returned in order to create a new subscription with the same parameters in a reset scenario.
        /// </remarks>
        async Task<InfoPriceSubscription> DeleteSubscription(string referenceId)
        {
            // Remove subscription from local collection. 
            // It may already have been removed by a subscription reset message.
            InfoPriceSubscription subscription;
            if (!_subscriptions.TryRemove(referenceId, out subscription))
                return null;

            // Remove subscription from server
            var deleteUri = $"{DeleteSubscriptionUrl}/{_contextId}/{referenceId}";
            using (var response = await _httpClient.DeleteAsync(deleteUri))
            {
                response.EnsureSuccessStatusCode();
            }
            return subscription;
        }

        /// <summary>
        /// Deletes all the subscriptions.
        /// </summary>
        Task<InfoPriceSubscription[]> DeleteSubscriptions()
        {
            // ----------------------------------------------------------------------------
            // Here the deletion endpoint is called for each subscription. Alternatively,
            // the deletion endpoint can be called using only the context id to delete
            // all info price subscriptions, or the root service can be called to delete
            // all subscriptions for the streaming session.
            // ----------------------------------------------------------------------------
            return Task.WhenAll(_subscriptions.Values.Select(s => DeleteSubscription(s.ReferenceId)));
        }

        /// <summary>
        /// Resets a specified subscription.
        /// </summary>
        async Task ResetSubscription(string referenceId)
        {
            // Delete the existing subscription
            var subscription = await DeleteSubscription(referenceId);
            if (subscription != null)
            {
                // Create a new subscription with the same parameters
                await CreateSubscription(GetReferenceId("infoprice"), subscription.AssetType, subscription.Uics);
            }
        }

        #endregion

        #region Message handling

        /// <summary>
        /// Handles a message bundle from the streaming connection.
        /// </summary>
        /// <remarks>
        /// All streaming messages, including control messages, come in bundles.
        /// </remarks>
        void HandleMessageBundle(JArray jsonMessages)
        {
            foreach (var jsonMessage in jsonMessages)
                HandleMessage((JObject)jsonMessage);
        }

        /// <summary>
        /// Handles a single message from the streaming connection.
        /// </summary>
        void HandleMessage(JObject jsonMessage)
        {
            // ----------------------------------------------------------------------------
            // The reference id is used for determining the kind of message. If it is a 
            // data message the reference id identifies the subscription whose data must
            // be updated.
            // ----------------------------------------------------------------------------
            var referenceId = (string)jsonMessage["ReferenceId"];

            // Heartbeat control message
            if (referenceId.Equals("_heartbeat"))
            {
                HandleSubscriptionHeartbeatMessage(jsonMessage);
            }
            // Subscription reset control message
            else if (referenceId.Equals("_resetsubscriptions"))
            {
                HandleResetSubscriptionsMessage(jsonMessage);
            }
            // Unknown control messages should be ignored
            else if (referenceId.StartsWith("_"))
            {
                Console.WriteLine($"Unknown control message type received: {referenceId}");
            }
            // All other messages are data messages, i.e. streaming updates for subscriptions
            else
            {
                HandleDataMessage(referenceId, jsonMessage);
            }
        }

        /// <summary>
        /// Handles a subscription heartbeat message by updating the activity on the subscriptions
        /// specified in the message.
        /// </summary>
        /// <remarks>
        /// Example message:
        /// 
        ///   {
        ///     "ReferenceId": "_heartbeat",
        ///     "Heartbeats": [
        ///       {
        ///         "OriginatingReferenceId": "infoprice1",
        ///         "Reason": "NoNewData"
        ///       }
        ///     ]
        ///   }
        /// </remarks>
        void HandleSubscriptionHeartbeatMessage(JObject jsonMessage)
        {
            var jsonHeartbeats = (JArray)jsonMessage["Heartbeats"];
            foreach (var jsonHeartbeat in jsonHeartbeats)
            {
                var referenceId = (string)jsonHeartbeat["OriginatingReferenceId"];
                var subscription = FindSubscription(referenceId);
                if (subscription != null)
                {
                    subscription.UpdateActivity();
                }
            }
        }

        /// <summary>
        /// Handles a subscription reset message by replacing subscriptions that are invalid.
        /// </summary>
        /// <remarks>
        /// Subscriptions may become invalid for several reasons, the most common of
        /// which is when the streaming connection is temporarily lost.
        /// 
        /// Example message for resetting specific subscriptions:
        /// 
        ///   {
        ///     "ReferenceId": "_resetsubscriptions",
        ///     "TargetReferenceIds": [ "infoprice1", "infoprice2" ]
        ///   }
        ///
        /// Example message for resetting all subscriptions:
        /// 
        ///   {
        ///     "ReferenceId": "_resetsubscriptions"
        ///   }
        /// </remarks>
        void HandleResetSubscriptionsMessage(JObject jsonMessage)
        {
            string[] targetReferenceIds;
            if (jsonMessage["TargetReferenceIds"] != null)
            {
                // Reference ids are specified
                targetReferenceIds = ((JArray)jsonMessage["TargetReferenceIds"]).Select(r => (string)r).ToArray();

                // An empty reference id array indicates that all subscriptions should be reset
                if (targetReferenceIds.Length == 0)
                {
                    targetReferenceIds = _subscriptions.Values.Select(s => s.ReferenceId).ToArray();
                }
            }
            else
            {
                // Reference ids are unspecified so all subscriptions must be reset
                targetReferenceIds = _subscriptions.Values.Select(s => s.ReferenceId).ToArray();
            }

            // Reset subscriptions
            Task.WhenAll(targetReferenceIds.Select(ResetSubscription)).Wait();
        }

        /// <summary>
        /// Handles a data message.
        /// </summary>
        /// <remarks>
        /// 
        /// Example message:
        /// {
        ///   "ReferenceId": "infoprice1",
        ///   "__pn": 1,
        ///   "__pc": 2,
        ///   "Data" [
        ///     {
        ///       "Uic": 21,
        ///       "Quote": {
        ///         "Ask": 133.0,
        ///         "Bid": 53.0,
        ///         "Mid": 95.0,
        ///       }
        ///     }
        ///   ]
        /// }
        /// </remarks>
        void HandleDataMessage(string referenceId, JObject jsonMessage)
        {
            // Find the target subscription of the message
            var subscription = FindSubscription(referenceId);
            
            // ----------------------------------------------------------------------------
            // Messages that are sent to unknown or expired subscriptions must be ignored
            // This can happen when a subscription is removed while a message is being sent
            // ----------------------------------------------------------------------------
            if (subscription == null)
            {
                Console.WriteLine($"[{referenceId}]: Message for unknown subscription discarded");
            }
            else
            {
                subscription.UpdateActivity();
                subscription.HandleUpdate(jsonMessage);
                Console.WriteLine($"[{referenceId}]: Update received, hit SPACE to see current snapshot");
            }
        }

        #endregion

        #region Activity monitoring
        
        Timer _activityMonitorTimer;

        /// <summary>
        /// Verifies that subscriptions are active and resets those subscription that are not.
        /// </summary>
        async Task MonitorActivity()
        {
            // Find inactive subscriptions
            var inactiveSubscriptionReferenceIds = _subscriptions.Values
                .Where(s => s.Inactive)
                .Select(s => s.ReferenceId)
                .ToArray();

            if (inactiveSubscriptionReferenceIds.Length > 0)
            {
                Console.WriteLine($"[Activity monitor]: Inactive subscriptions found: {string.Join(", ", inactiveSubscriptionReferenceIds)}");

                // Reset the inactive subscriptions
                await Task.WhenAll(inactiveSubscriptionReferenceIds.Select(ResetSubscription));
            }
            else
            {
                Console.WriteLine("[Activity monitor]: No inactive subscriptions");
            }
        }

        /// <summary>
        /// Starts period monitoring of subscription activity.
        /// </summary>
        void StartActivityMonitor()
        {
            _activityMonitorTimer = new Timer { AutoReset = true, Interval = 10000 };
            _activityMonitorTimer.Elapsed += (sender, eventArgs) => MonitorActivity().Wait();
            _activityMonitorTimer.Start();
        }

        /// <summary>
        /// Starts period monitoring of subscription activity.
        /// </summary>
        void StopActivityMonitor()
        {
            _activityMonitorTimer.Stop();
        }

        #endregion

        #region SignalR connection management

        Connection _streamingConnection;

        void Connection_StateChanged(StateChange stateChange)
        {
            Console.WriteLine($"[Connection]: {stateChange.NewState}");
        }

        void Connection_Received(string message)
        {
            Console.WriteLine("[Connection]: Message received");
            HandleMessageBundle(JArray.Parse(message));
        }

        void Connection_Error(Exception exception)
        {
            Console.Error.WriteLine($"[Connection]: Error: {exception.Message}");
        }
        
        /// <summary>
        /// Sets up the SignalR streaming connection.
        /// </summary>
        Task CreateStreamingConnection()
        {
            // Establish connection
            var transport = new AutoTransport(new DefaultHttpClient());

            // Add context id and access token to the connection URL
            var queryStringData = new Dictionary<string, string>
            {
                { "authorization",HttpUtility.UrlEncode($"Bearer {AccessToken}") },

                { "context", HttpUtility.UrlEncode(_contextId) }
            };

            _streamingConnection = new Connection(StreamingConnectionUrl, queryStringData)
            {
                // Use the shared cookie container to ensure stickiness
                CookieContainer = _cookieContainer
            };

            _streamingConnection.StateChanged += Connection_StateChanged;
            _streamingConnection.Received += Connection_Received;
            _streamingConnection.Error += Connection_Error;

            return _streamingConnection.Start(transport);
        }
        
        #endregion

        #region HTTP client

        HttpClient _httpClient;
        CookieContainer _cookieContainer;

        void InitializeHttpClient()
        {
            // Set up shared cookie container for storing stickiness cookies
            _cookieContainer = new CookieContainer();
            var clientHandler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseDefaultCredentials = true
            };
            _httpClient = new HttpClient(clientHandler);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {AccessToken}");
        }

        /// <summary>
        /// Calls the isalive endpoints for the trade service and streaming service in order to get stickiness cookies.
        /// </summary>
        /// <remarks>
        /// The stickiness cookies are written to a shared cookie container which is used by all subsequent requests.
        /// </remarks>
        async Task EnsureStickiness()
        {
            using (var response = await _httpClient.GetAsync(ServiceStickinessUri))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Console.Error.WriteLine("Please insert a valid access token in App.config.");
                }
                response.EnsureSuccessStatusCode();
            }
            using (var response = await _httpClient.GetAsync(StreamingStickinessUrl))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Console.Error.WriteLine("Please insert a valid access token in App.config.");
                }
                response.EnsureSuccessStatusCode();
            }
        }

        #endregion

        #region Configuration

        // NOTE: The access token is only stored in the configuration file for purposes of demonstration
        static string AccessToken = ConfigurationManager.AppSettings["AccessToken"];
        static string OpenApiBaseUrl = ConfigurationManager.AppSettings["OpenApiBaseUrl"];
        static string StreamingBaseUrl = ConfigurationManager.AppSettings["StreamingBaseUrl"];
        static string SubscriptionUrl = OpenApiBaseUrl + "/trade/v1/infoprices/subscriptions/active";
        static string DeleteSubscriptionUrl = OpenApiBaseUrl + "/trade/v1/infoprices/subscriptions";
        static string StreamingConnectionUrl = StreamingBaseUrl + "/streaming/connection";
        static string StreamingStickinessUrl = StreamingBaseUrl + "/streaming/isalive";
        static string ServiceStickinessUri = OpenApiBaseUrl + "/trade/isalive";

        #endregion

        /// <summary>
        /// Writes the current data snapshots to the console.
        /// </summary>
        void PrintSnapshots()
        {
            foreach (var subscription in _subscriptions.Values)
            {
                string snapshot;
                lock (subscription.DataLock)
                {
                    snapshot = subscription.Data == null ? "Not received" : JsonConvert.SerializeObject(subscription.Data, Formatting.Indented);
                }
                Console.WriteLine($"[{subscription.ReferenceId}]: {snapshot}");
            }
        }

        async Task RunSample()
        {
            // The context id identifies the streaming session when there are more streaming connections in the same session.
            _contextId = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            InitializeHttpClient();
            await EnsureStickiness();

            // The streaming connection and subscriptions are set up in parallel
            await Task.WhenAll(
                CreateStreamingConnection(),
                CreateSubscriptions());

            StartActivityMonitor();

            Console.WriteLine("Press 'Q' to quit or SPACE to see the current data snapshots");
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.KeyChar == 'Q' || key.KeyChar == 'q')
                    break;
                PrintSnapshots();
            }

            StopActivityMonitor();

            await DeleteSubscriptions();
            _streamingConnection.Stop();
        }

        public void Dispose()
        {
            if (_streamingConnection != null)
            {
                _streamingConnection.Dispose();
                _streamingConnection = null;
            }
            if (_activityMonitorTimer != null)
            {
                _activityMonitorTimer.Dispose();
                _activityMonitorTimer = null;
            }
            if (_httpClient != null)
            {
                _httpClient.Dispose();
                _httpClient = null;
            }
        }

        static void Main(string[] args)
        {
            try
            {
                using (var sample = new Program())
                    sample.RunSample().Wait();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Sample terminated with exception:");
                Console.Error.WriteLine($"  {ex.GetType()}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"  {ex.InnerException.GetType()}: {ex.InnerException.Message}");
            }
        }
    }
}