using Newtonsoft.Json;
using Sample.Auth.Pkce.Models;
using Sample.Auth.Pkce.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Sample.Auth.Pkce
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                App app = GetApp();
                PkceAuthService authService = new PkceAuthService();
                ClientService clientService = new ClientService();

                // Open Listener for Redirect
                HttpListener listener = BeginListening(app);

                // Get token and call API
                Token token = GoLogin(app, listener);
                dynamic client = new ClientService().GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);
                Console.WriteLine("Received token object (don't log in your own app):");
                Console.WriteLine(JsonConvert.SerializeObject(new { Token = token, Client = client }, Formatting.Indented));
                Console.WriteLine("================================ ");

                // Refresh token and call API again
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Refreshing Token... ");
                Token newToken = RefreshToken(app, token.RefreshToken);
                client = new ClientService().GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);
                Console.WriteLine("New token object (don't log in your own app):");
                Console.WriteLine(JsonConvert.SerializeObject(new { Token = newToken, Client = client }, Formatting.Indented));
                Console.WriteLine("================================ ");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Console.Read();
            }
        }

        private static Token GoLogin(App app, HttpListener listener)
        {
            PkceAuthService authService = new PkceAuthService();
            string authUrl = authService.GetAuthenticationRequest(app);
            Console.WriteLine("Loading AuthUrl in browser: " + authUrl);
            System.Diagnostics.Process.Start(authUrl);

            string authCode = GetAuthCode(listener);
            Console.WriteLine($"Auth code {authCode} received.");

            // Get Token
            return authService.GetToken(app, authCode);
        }

        private static Token RefreshToken(App app, string refreshToken)
        {
            PkceAuthService authService = new PkceAuthService();
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentException("Invalid refresh token");

            return authService.RefreshToken(app, refreshToken);
        }

        private static int GetRandomUnusedPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static HttpListener BeginListening(App app)
        {
            HttpListener listener;
            try
            {
                int port = GetRandomUnusedPort();
                Uri uri = new Uri(app.RedirectUrls[0]);
                listener = new HttpListener();
                listener.Prefixes.Add($"{uri.Scheme}://{uri.Host}:{port}/");
                listener.Start();

                app.RedirectUrls[0] = uri.AbsoluteUri.Replace(uri.Host, uri.Host + ":" + port);
                return listener;
            }
            catch(Exception ex)
            {
                throw new Exception("Failed to start the listener for the redirect URL", ex);
            }
        }

        private static string GetAuthCode(HttpListener listener)
        {
            // Listening
            HttpListenerContext httpContext = null;
            try
            {
                httpContext = listener.GetContext();
                string error = httpContext.Request.QueryString["error"];
                string authCode = httpContext.Request.QueryString["code"];
                using (StreamWriter writer = new StreamWriter(httpContext.Response.OutputStream))
                {
                    if (error != null)
                    {
                        string errorDescription = httpContext.Request.QueryString["error_description"];
                        // Make sure the customer knows something went wrong. A common issue is the account not being active yet, due to the initial deposit.
                        writer.WriteLine("An error has occurred (" + error + "): " + errorDescription);
                        throw new Exception(errorDescription);
                    }
                    else
                    {
                        writer.WriteLine("AuthCode received by App. You can close the browser.");
                    }
                    writer.Close();
                }

                return authCode;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get the authCode from URL: " + ex.Message, ex);
            }
            finally
            {
                if (httpContext != null)
                    httpContext.Response.Close();
            }
        }

        private static App GetApp()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "App.json");
            Console.WriteLine("Reading app config: " + path);
            string content = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<App>(content);
        }
    }
}
