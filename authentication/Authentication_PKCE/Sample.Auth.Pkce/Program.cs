using Newtonsoft.Json;
using Sample.Auth.Pkce.Models;
using Sample.Auth.Pkce.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Sample.Auth.Pkce
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var app = GetApp();
                var authService = new PkceAuthService();
                var clientService = new ClientService();

                // Open Listener for Redirect
                var listener = BeginListening(app);

                // Get token and call API
                Console.WriteLine("Getting Token... ");
                var token = GoLogin(app, listener);
                var client = new ClientService().GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);
                Console.WriteLine("Token: ");
                Console.WriteLine(JsonConvert.SerializeObject(new { Token = token, Client = client }, Formatting.Indented));
                Console.WriteLine("================================ ");

                // Refhresh token and call api
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Refreshing Token... ");
                var newToken = RefreshToken(app, token.RefreshToken, listener);
                client = new ClientService().GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);
                Console.WriteLine("New Token: ");
                Console.WriteLine(JsonConvert.SerializeObject(new { Token = newToken, Client = client }, Formatting.Indented));
                Console.WriteLine("Demo Done.");
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
            var authService = new PkceAuthService();
            var authUrl = authService.GetAuthenticationRequest(app);

            System.Diagnostics.Process.Start(authUrl);

            var authCode = GetAuthCode(app, listener);
            Console.WriteLine($"Auth code {authCode} received.");

            // Get Token
            return authService.GetToken(app, authCode);
        }


        private static Token RefreshToken(App app, string refreshToken, HttpListener listener)
        {
            var authService = new PkceAuthService();
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentException("Invalid refresh token");

            var token = authService.RefreshToken(app, refreshToken);

            return token;
        }

        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static HttpListener BeginListening(App app)
        {
            HttpListener listener;
            try
            {
                var port = GetRandomUnusedPort();
                var uri = new Uri(app.RedirectUrls[0]);
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

        private static string GetAuthCode(App app, HttpListener listener)
        {
            // Listening
            HttpListenerContext httpContext = null;
            try
            {
                httpContext = listener.GetContext();
                var authCode = httpContext.Request.QueryString["code"];
                using (var writer = new StreamWriter(httpContext.Response.OutputStream))
                {
                    writer.WriteLine("AuthCode received by App. Please close the browser.");
                    writer.Close();
                }

                return authCode;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get the authCode from URL", ex);
            }
            finally
            {
                if (httpContext != null)
                    httpContext.Response.Close();
            }
        }

        private static App GetApp()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "App.json");
            var content = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<App>(content);
        }
    }
}
