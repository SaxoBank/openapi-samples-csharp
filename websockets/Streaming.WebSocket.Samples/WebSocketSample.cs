using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Streaming.WebSocket.Samples
{
    /// <summary>
    /// This sample shows how to connect to the Streaming Server and later set up a subscription to start receiving messages.
    /// 
    /// The connection flow is like this.
    /// 
    /// 1. First open a streaming WebSocket connection. 
    ///    The WebSocket connection is identified by a ContextId, that has to be used when later setting up subscriptions.
    ///    You can start creating the subscription (step 2) in a parallel task. There is a few seconds of buffers
    ///	   so you won't lose messages even if the subscription starts pushing messages before a WebSocket
    ///    connection is established.
    /// 
    /// 2. Create a subscription 
    ///    Subscriptions define the type of data you want to receive continuous updates on.
    ///    Each subscription is identified by a ContextId and a ReferenceId.
    ///    The ContextId tells our servers what streaming connection you want these messages to be sent to. 
    ///    So you need to use the same ContextId you used when opening the streaming connection.
    ///    The ReferenceId uniquely identifies the subscription on the streaming connection.
    ///    It is possible to set up multiple subscriptions on a single WebSocket connection. When you receive them 
    ///    you can distinguish what subscription a message belongs to by inspecting the ReferenceId in the message header.
    /// 
    /// 3. Receive and parse messages
    ///    Messages are sent as a binary byte stream. The first part of the message is the message envelope or the headers.
    ///    These header fields have a set format, and should always be parsed like this.
    /// 
    ///    -----------------------------------------------------------------------------------
    ///    | Field                | Bytes | Type   | Note                                    |
    ///    | --------------------------------------------------------------------------------|
    ///    | Message Id	          | 8     | Int64  | 64-bit little-endian unsigned integer.  |
    ///    | Reserved Field       | 2     | Int16  | Always 0.                               |
    ///    | Reference Id Size    | 1     | Int8   |                                         |
    ///    | Reference Id         | n     | Ascii  | n = Reference Id Size                   |
    ///    | Payload Format       | 1     | Int8   | 0 = Json. 8-bit unsigned integer.       |
    ///    | Payload Size         | 4     | Int32  |                                         |
    ///    | Payload              | n     | Byte[] | n = Payload Size. Json is UTF8 encoded. |
    ///    -----------------------------------------------------------------------------------
    ///    (*) Timestamp is the number of 100-nanosecond intervals that have elapsed since 12:00:00 midnight, January 1, 0001 (0:00:00 UTC on January 1, 0001, in the Gregorian calendar).
    /// 
    /// 4. Response codes to be aware of when creating subscriptions.
    ///	   409 Conflict. This means that you have tried to reuse a ContextId. This is not possible. So please create a new ContextId and try again.
    ///    429 Too many requests. This most likely happened because you have been throttled, and have exceeded the allowed number of connections.
    ///
    /// 5. Disconnecting
    ///	   When the client wants to disconnect it will have to send a WebSocket close frame to the server. The server will then respond with a close frame of its own.
    ///    This is implemented in the StopWebSocket method.
    ///
    /// 6. Reconnecting
    ///	   In case of errors that occur on the transport level, you will want to reconnect to the socket. To make sure this has as low an impact as possible
    ///    the server keeps a small buffer of the latest messages sent to the client. When reconnecting you can specify the last message id you have received
    ///	   which will tell the server to start sending from that message. If you specify message id 10 as your last seen message, then the first message you
    ///    will see after a reconnect is the message that comes after message 10. Do not assume that the message ids are ordered. They are merely ids and we
    ///    can restart the sequence at any time if we need to. In case the buffer has overflown, your reconnect request will still be accepted, but the Web Socket
    ///    server will send out a reset subscriptions control message, telling you to set up the subscription again.
    ///
    /// 7. Reauthorizing
    ///    Tokens issued from our OAuth2 server only have a limited lifetime. Along with the access token we also issue a refresh token. You need this
    ///    refresh token to get a new access token before the current one expires.
    ///	   With normal http requests, you would just replace your access token once it has been renewed and include the new token in all subsequent requests.
    ///    But Web Socket Streams don't work like regular HTTP requests, so the server only knows the token you had when you set up the streaming connection.
    ///    So once you have refreshed your token, you will have to let the server know that you have a new and valid token. Otherwise the server will disconnect
    ///    the streaming connection once the initial token expires.
    ///    Doing this is quite simple. You need to get a new access token from our OAuth2 server and then execute a PUT request with a correct Authorization
    ///    header and a context id in the querystring (/streamingws/authorize?contextid=abc123).
    ///    The server will return a 202 Accepted status code if the new access token is valid.
    ///    It will return 202 even if the context ids do not match any current connections.
    ///
    /// 8. Resetting subscriptions
    ///    In the event that the Web Socket server detects a message loss or other error, it will send a Reset Subscription control message on the Web Socket connection.
    ///    This message tells you that you have to recreate the subscriptions. The reset subscription messages may include a list of reference ids, that should be reset.
    ///	   If it does not include such a list or the list is empty, you will have to reset all subscriptions.
    ///    Resetting a subscription means first deleting the subscription and then creating a new one.
    /// 
    /// - A note on streams vs. subscriptions -
    /// 
    /// It is important to understand the difference between WebSocket streams and the subscriptions that generate data for those streams, and how they work together. 
    /// The WebSocket stream is the delivery channel for the updates you have asked for. The Subscriptions are where you ask for specific updates to be sent out.
    /// A stream can deliver data for multiple subscriptions. Streams are identified by a context id and subscriptions are identified by a reference id.
    /// 
    /// </summary>
    public sealed class WebSocketSample : IDisposable
    {

        private readonly string _contextId;
        private readonly string _referenceId;
        private readonly string _webSocketConnectionUrl;
        private readonly string _webSocketAuthorizationUrl;
        private readonly string _priceSubscriptionUrl;

        private ClientWebSocket _clientWebSocket;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private string _token;
        private bool _disposed;
        private long _lastSeenMessageId;
        private long _receivedMessagesCount;

        public WebSocketSample()
        {
            //A valid OAuth2 _token - get a 24-hour token here: https://www.developer.saxo/openapi/token/current
            _token = "######";

            //Url for streaming server.
            _webSocketConnectionUrl = "wss://streaming.saxobank.com/sim/openapi/streamingws/connect";

            //Url for streaming server.
            _webSocketAuthorizationUrl = "https://streaming.saxobank.com/sim/openapi/streamingws/authorize";

            //Url for creating price subscription.
            _priceSubscriptionUrl = "https://gateway.saxobank.com/sim/openapi/trade/v1/prices/subscriptions";

            //A string provided by the client to correlate the stream and the subscription. Multiple subscriptions can use the same contextId.
            _contextId = "ctx_123_1";

            //A unique string provided by the client to identify a certain subscription in the stream.
            _referenceId = "rf_abc_1";
        }

        /// <summary>
        /// The sample starts here.
        /// </summary>
        public async Task RunSample(CancellationTokenSource cts)
        {
            ThrowIfDisposed();

            _cts = cts;

            //First start the web socket connection.
            Task taskStartWebSocket = new Task(async () => { await StartWebSocket(); }, cts.Token);
            taskStartWebSocket.Start();

            //Then start the subscription.
            Task taskCreateSubscription = new Task(async () => { await CreateSubscription(); }, cts.Token);
            taskCreateSubscription.Start();

            //Start a task to renew the token when needed. If we don't do this the connection will be terminated once the token expires.
            DateTime tokenDummyExpiryTime = DateTime.Now.AddHours(2); //Here you need to provide the correct expiry time for the token. This is just a dummy value.
            //When the code breaks here, you probably need to add a valid _token in the code above.
            Task taskReauthorization = new Task(async () => { await ReauthorizeWhenNeeded(tokenDummyExpiryTime, cts.Token); }, cts.Token);
            taskReauthorization.Start();

            //Wait for both tasks to finish.
            Task[] tasks = { taskStartWebSocket, taskCreateSubscription, taskReauthorization };
            try
            {
                Task.WaitAll(tasks, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!cts.IsCancellationRequested) Console.WriteLine("Listening on web socket.");

            //Let's wait until someone stops the sample.
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Before the access token expires we need to renew it and reauthorize with the streaming server.
        /// This sample does not show how to refresh the token. For this we refer to the authorization guide.
        /// 
        /// </summary>
        private async Task ReauthorizeWhenNeeded(DateTime tokenExpiryTime, CancellationToken cts)
        {
            //Renew the token a minute before it expires, to give us ample time to renew.
            TimeSpan tokenRenewalDelay = tokenExpiryTime.AddSeconds(-60).Subtract(DateTime.Now);

            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(tokenRenewalDelay, cts);

                //This is where you should renew the token and get a new expiry time.
                //Here we have just created dummy values.
                tokenRenewalDelay = tokenRenewalDelay.Add(TimeSpan.FromHours(2));
                string refreshedToken = "<refreshedToken>";
                _token = refreshedToken;
                await Reauthorize(refreshedToken);
            }
        }

        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = System.Net.DecompressionMethods.GZip };
            return new HttpClient(handler);
        }


        /// <summary>
        /// A method that reauthorizes the Web Socket connection.
        /// </summary>
        /// <param name="token">A valid OAuth2 access token.</param>
        private async Task Reauthorize(string token)
        {
            using (HttpClient httpClient = CreateHttpClient())
            {
                // Disable Expect: 100 Continue according to https://www.developer.saxo/openapi/learn/openapi-request-response
                // In our experience the same two-step process has been difficult to get to work reliable, especially as we support clients world wide, 
                // who connect to us through a multitude of network gateways and proxies.We also find that the actual bandwidth savings for the majority of API requests are limited, 
                // since most requests are quite small.
                // We therefore strongly recommend against using the Expect:100 - Continue header, and expect you to make sure your client library does not rely on this mechanism.
                httpClient.DefaultRequestHeaders.ExpectContinue = false;

                Uri reauthorizationUrl = new Uri($"{_webSocketAuthorizationUrl}?contextid={_contextId}");
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, reauthorizationUrl))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("BEARER", token);
                    HttpResponseMessage response = await httpClient.SendAsync(request, _cts.Token);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine("Refreshed token successfully and reauthorized.");
                }
            }
        }

        /// <summary>
        /// This method deletes a subscription so it no longer sends out messages on the stream.
        /// </summary>
        private async Task DeleteSubscription(string[] referenceIds)
        {
            ThrowIfDisposed();
            using (HttpClient httpClient = CreateHttpClient())
            {
                // Disable Expect: 100 Continue according to https://www.developer.saxo/openapi/learn/openapi-request-response
                // In our experience the same two-step process has been difficult to get to work reliable, especially as we support clients world wide, 
                // who connect to us through a multitude of network gateways and proxies.We also find that the actual bandwidth savings for the majority of API requests are limited, 
                // since most requests are quite small.
                // We therefore strongly recommend against using the Expect:100 - Continue header, and expect you to make sure your client library does not rely on this mechanism.
                httpClient.DefaultRequestHeaders.ExpectContinue = false;

                //In a real implementation we would look at the reference ids passed in and 
                //delete all the subscriptions listed. But in this implementation only one exists.
                string deleteSubscriptionUrl = $"{_priceSubscriptionUrl}/{_contextId}/{_referenceId}";
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, deleteSubscriptionUrl))
                {
                    await httpClient.SendAsync(request, _cts.Token);
                }
            }
        }

        /// <summary>
        /// This method sets up a subscription on a stream.
        /// </summary>
        private async Task CreateSubscription()
        {
            ThrowIfDisposed();

            var subscriptionRequest = new
            {
                ContextId = _contextId,
                ReferenceId = _referenceId,
                Arguments = new
                {
                    AssetType = "FxSpot",
                    Uic = 21
                }
            };

            string json = JsonConvert.SerializeObject(subscriptionRequest);
            using (HttpClient httpClient = CreateHttpClient())
            {
                // Disable Expect: 100 Continue according to https://www.developer.saxo/openapi/learn/openapi-request-response
                // In our experience the same two-step process has been difficult to get to work reliable, especially as we support clients world wide, 
                // who connect to us through a multitude of network gateways and proxies.We also find that the actual bandwidth savings for the majority of API requests are limited, 
                // since most requests are quite small.
                // We therefore strongly recommend against using the Expect:100 - Continue header, and expect you to make sure your client library does not rely on this mechanism.
                httpClient.DefaultRequestHeaders.ExpectContinue = false;
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _priceSubscriptionUrl))
                {
                    //Make sure you prepend the _token with the BEARER scheme
                    request.Headers.Authorization = new AuthenticationHeaderValue("BEARER", _token);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    Stream myResponseStream = null;
                    try
                    {
                        HttpResponseMessage response = await httpClient.SendAsync(request, _cts.Token);
                        response.EnsureSuccessStatusCode();
                        // Saxobank is moving to HTTP/2, but here only HTTP/1.0 and HTTP/1.1 version requests are supported.
                        Console.WriteLine(request.RequestUri + " is using HTTP/" + response.Version);
                        // Read Response body
                        myResponseStream = await response.Content.ReadAsStreamAsync();
                        using (StreamReader myStreamReader = new StreamReader(myResponseStream))
                        {
                            string responseBody = myStreamReader.ReadToEnd();
                            Console.WriteLine("Received snapshot:");
                            Console.WriteLine(JToken.Parse(responseBody).ToString(Formatting.Indented));
                            Console.WriteLine();
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                    catch (HttpRequestException e)
                    {
                        Console.WriteLine("Subscription creation error.");
                        Console.WriteLine(e.Message);
                        _cts.Cancel(false);
                    }
                    finally
                    {
                        if(myResponseStream != null)
                            myResponseStream.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Open a websocket connection and start listening for data.
        /// </summary>
        private async Task StartWebSocket()
        {
            ThrowIfDisposed();

            //Make sure you append the contextId to the websocket connection url, and in the case of reconnection also the last seen message id.
            Uri url;
            if (_receivedMessagesCount > 0)
            {
                url = new Uri($"{_webSocketConnectionUrl}?contextid={_contextId}&messageid={_lastSeenMessageId}");
            }
            else
            {
                url = new Uri($"{_webSocketConnectionUrl}?contextid={_contextId}");
            }

            //Make sure you prepend the _token with the BEARER scheme
            string authorizationHeader = $"BEARER {_token}";

            //Connect to the web socket
            _clientWebSocket = new ClientWebSocket();
            _clientWebSocket.Options.SetRequestHeader("Authorization", authorizationHeader);

            try
            {
                await _clientWebSocket.ConnectAsync(url, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                string flattenedExceptionMessages = FlattenExceptionMessages(e);
                Console.WriteLine("WebSocket connection error.");
                Console.WriteLine(flattenedExceptionMessages);
                _cts.Cancel(false);
                return;
            }

            //start listening for messages
            _receiveTask = ReceiveMessages(ErrorCallBack, SuccessCallBack, ControlMessageCallBack);
        }

        /// <summary>
        /// In case a <exception cref="WebSocketException" /> is thrown we will try to reconnect.
        /// Reconnecting requires us to send the last received message id, so we
        /// can pick up the stream from where we left off.
        /// If other exceptions are thrown we will stop the Web Socket connection because we do not know what to do.
        /// </summary>
        /// <param name="exception">Exception thrown.</param>
        private async Task ErrorCallBack(Exception exception)
        {
            Console.WriteLine($"Error callback: {exception.Message}");
            if (exception is WebSocketException)
            {
                Console.WriteLine($"Reconnection with last seen message id {_lastSeenMessageId}.");
                _clientWebSocket?.Dispose();
                await StartWebSocket();
            }
            else
            {
                await StopWebSocket();
            }
        }

        /// <summary>
        /// Handles messages successfully delivered.
        /// </summary>
        /// <param name="webSocketMessage">Web Socket message to be handled.</param>
        private void SuccessCallBack(WebSocketMessage webSocketMessage)
        {
            PrintMessage(webSocketMessage);
        }

        /// <summary>
        /// Delegate to handle control messages that require special handling.
        /// </summary>
        /// <param name="webSocketMessage">A control Web Socket message.</param>
        private async Task ControlMessageCallBack(WebSocketMessage webSocketMessage)
        {
            //All control message reference ids start with an underscore
            if (!webSocketMessage.ReferenceId.StartsWith("_")) throw new ArgumentException($"Message {webSocketMessage.MessageId} with reference id {webSocketMessage.ReferenceId} is not a control message.");

            switch (webSocketMessage.ReferenceId)
            {
                case "_heartbeat":
                    // HeartBeat messages indicate that no new data is available. You do not need to do anything.
                    HeartbeatControlMessage[] heartBeatMessage = DecodeWebSocketMessagePayload<HeartbeatControlMessage[]>(webSocketMessage);
                    string referenceIdList = string.Join(",", heartBeatMessage.First().Heartbeats.Select(h => h.OriginatingReferenceId));
                    Console.WriteLine($"{webSocketMessage.MessageId}\tHeartBeat control message received for reference ids {referenceIdList}.");
                    break;
                case "_resetsubscriptions":
                    //For some reason the server is not able to send out messages,
                    //and needs the client to reset subscriptions by recreating them.
                    Console.WriteLine($"{webSocketMessage.MessageId}\tReset Subscription control message received.");
                    await ResetSubscriptions(webSocketMessage);
                    break;
                case "_disconnect":
                    //The server has disconnected the client. This messages requires you to re-authenticate
                    //if you wish to continue receiving messages. In this example we will just stop the WebSocket.
                    Console.WriteLine($"{webSocketMessage.MessageId}\tDisconnect control message received.");
                    await StopWebSocket();
                    break;
                default:
                    throw new ArgumentException($"Unknown control message reference id: {webSocketMessage.ReferenceId}");
            }
        }

        /// <summary>
        /// Reset the subscriptions the server tells you to reset.
        /// </summary>
        /// <param name="message">The parsed message.</param>
        private async Task ResetSubscriptions(WebSocketMessage message)
        {
            ResetSubscriptionsControlMessage resetSubscriptionMessage = DecodeWebSocketMessagePayload<ResetSubscriptionsControlMessage>(message);

            //First delete the subscriptions the server tells us need to be reconnected.
            await DeleteSubscription(resetSubscriptionMessage.TargetReferenceIds);

            //Next create the subscriptions again.
            //You should keep track of a list of your subscriptions so you know which ones you have to recreate. 
            //Here we only have one subscription to illustrate the point.
            await CreateSubscription();
        }

        /// <summary>
        /// Monitor the stream and parse messages when they arrive.
        /// </summary>
        private async Task ReceiveMessages(Func<Exception, Task> errorCallback, Action<WebSocketMessage> successCallback, Func<WebSocketMessage, Task> controlMessageCallback)
        {
            try
            {
                //Create a buffer to hold the received messages in.
                byte[] buffer = new byte[16 * 1024];
                int offset = 0;
                Console.WriteLine("Start receiving messages.");

                //Listen while the socket is open.
                while (_clientWebSocket.State == WebSocketState.Open && !_disposed)
                {
                    ArraySegment<byte> receiveBuffer = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
                    WebSocketReceiveResult result = await _clientWebSocket.ReceiveAsync(receiveBuffer, _cts.Token);

                    if (_cts.IsCancellationRequested)
                        break;

                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Binary:
                            offset += result.Count;
                            if (result.EndOfMessage)
                            {
                                byte[] message = new byte[offset];
                                Array.Copy(buffer, message, offset);
                                offset = 0;
                                WebSocketMessage[] parsedMessages = ParseMessages(message);

                                foreach (WebSocketMessage parsedMessage in parsedMessages)
                                {
                                    //Be sure to cache the last seen message id
                                    _lastSeenMessageId = parsedMessage.MessageId;
                                    _receivedMessagesCount++;

                                    if (IsControlMessage(parsedMessage))
                                    {
                                        await controlMessageCallback(parsedMessage);
                                    }
                                    else
                                    {
                                        successCallback(parsedMessage);
                                    }
                                }
                            }

                            break;
                        case WebSocketMessageType.Close:
                            if (_clientWebSocket.State == WebSocketState.Open)
                                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye",
                                    _cts.Token);
                            Console.WriteLine("Received a close frame.");
                            break;
                        case WebSocketMessageType.Text:
                            offset += result.Count;
                            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Goodbye",
                                _cts.Token);
                            Console.WriteLine("Closing connection - Reason: received a text frame.");
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                await errorCallback(e);
            }
        }

        /// <summary>
        /// Stop listening for messages from the websocket stream.
        /// </summary>
        public async Task StopWebSocket()
        {
            if (_disposed) return;

            try
            {
                //Send a close frame to the server.
                if (_clientWebSocket?.State == WebSocketState.Open)
                {
                    await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", _cts.Token).ConfigureAwait(false);
                }

                //The server will respond with a close frame.
                //The close frame from the server might come after you have closed down your connection.

                if (null != _receiveTask) await _receiveTask;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            Console.WriteLine("Stopped receiving messages.");
        }

        /// <summary>
        /// Print the content of the message to Console.
        /// </summary>
        /// <param name="message">The parsed message.</param>
        private void PrintMessage(WebSocketMessage message)
        {
            //Extract the UTF8 encoded message payload.
            string messagePayload = Encoding.UTF8.GetString(message.Payload);
            Console.WriteLine($"{message.MessageId}\tPayload: {messagePayload}");
        }

        /// <summary>
        /// Parse the messages received over the websocket stream.
        /// </summary>
        /// <param name="message">byte array containing the raw message bytes received.</param>
        /// <returns>A number of parsed <see cref="WebSocketMessage"/>s.</returns>
        private WebSocketMessage[] ParseMessages(byte[] message)
        {
            List<WebSocketMessage> parsedMessages = new List<WebSocketMessage>();
            int index = 0;
            do
            {
                //First 8 bytes make up the message id. A 64 bit integer.
                long messageId = BitConverter.ToInt64(message, index);
                index += 8;

                //Skip the next two bytes that contain a reserved field.
                index += 2;

                //1 byte makes up the reference id length as an 8 bit integer. The reference id has a max length og 50 chars.
                byte referenceIdSize = message[index];
                index += 1;

                //n bytes make up the reference id. The reference id is an ASCII string.
                string referenceId = Encoding.ASCII.GetString(message, index, referenceIdSize);
                index += referenceIdSize;

                //1 byte makes up the payload format. The value 0 indicates that the payload format is Json.
                byte payloadFormat = message[index];
                index++;

                //4 bytes make up the payload length as a 32 bit integer. 
                int payloadSize = BitConverter.ToInt32(message, index);
                index += 4;

                //n bytes make up the actual payload. In the case of the payload format being Json, this is a UTF8 encoded string.
                byte[] payload = new byte[payloadSize];
                Array.Copy(message, index, payload, 0, payloadSize);
                index += payloadSize;

                WebSocketMessage parsedMessage = new WebSocketMessage
                {
                    MessageId = messageId,
                    ReferenceId = referenceId,
                    PayloadFormat = payloadFormat,
                    Payload = payload
                };

                parsedMessages.Add(parsedMessage);

            } while (index < message.Length);

            return parsedMessages.ToArray();
        }

        /// <summary>
        /// Control message reference ids start with underscores.
        /// This methods inspects a message to see if it is a control message from the server, that requires special handling. 
        /// </summary>
        /// <param name="webSocketMessage">A Web Socket message.</param>
        /// <returns>True if the message is a Control Message.</returns>
        private bool IsControlMessage(WebSocketMessage webSocketMessage)
        {
            return webSocketMessage.ReferenceId.StartsWith("_");
        }

        /// <summary>
        /// Throws if disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WebSocketSample));
        }

        /// <summary>
        /// Dispose the sample class.
        /// </summary>
        public void Dispose()
        {
            _clientWebSocket?.Dispose();
            _clientWebSocket = null;
            _disposed = true;
        }

        /// <summary>
        /// Convenience method to prettify the exceptions thrown-
        /// </summary>
        /// <param name="exp">Nested exceptions.</param>
        /// <returns>Flattened exception message.</returns>
        private string FlattenExceptionMessages(Exception exp)
        {
            string message = string.Empty;
            Exception innerException = exp;

            do
            {
                message = message + Environment.NewLine + (string.IsNullOrEmpty(innerException.Message) ? string.Empty : innerException.Message);
                innerException = innerException.InnerException;
            }
            while (innerException != null);

            if (message.Contains("409"))
                message += Environment.NewLine + "ContextId cannot be reused. Please create a new one and try again.";

            if (message.Contains("429"))
                message += Environment.NewLine + "You have made too many request. Please wait and try again.";

            return message;
        }

        /// <summary>
        /// Easy deserializing of Web Socket message payload to a concrete class.
        /// </summary>
        /// <typeparam name="T">The resulting type.</typeparam>
        /// <param name="webSocketMessage">The received Web Socket message.</param>
        /// <returns></returns>
        private T DecodeWebSocketMessagePayload<T>(WebSocketMessage webSocketMessage)
        {
            string messagePayload = Encoding.UTF8.GetString(webSocketMessage.Payload);
            return JsonConvert.DeserializeObject<T>(messagePayload);
        }
    }
}