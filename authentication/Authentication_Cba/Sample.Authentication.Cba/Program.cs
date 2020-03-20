using Newtonsoft.Json;
using Sample.Authentication.Cba.Services;
using System;
using System.IO;

namespace Sample.Authentication.Cba
{
    class Program
    {
        static readonly CbaAuthService _authService = new CbaAuthService();

        static void Main(string[] args)
        {
            try
            {
                var app = GetApp();
                var certificate = GetCertificate();

                Console.WriteLine("Getting Token by Certificate... ");
                var token = GetToken(app, certificate);
                var client = new ClientService().GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);
                Console.WriteLine("Token: ");
                Console.WriteLine(JsonConvert.SerializeObject(new { Token = token, Client = client }, Formatting.Indented));
                Console.WriteLine("================================ ");


                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Refreshing Token... ");
                var newToken = RefreshToken(token.RefreshToken);
                client = new ClientService().GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);
                Console.WriteLine("New Token: ");
                Console.WriteLine(JsonConvert.SerializeObject(new { Token = newToken, Client = client }, Formatting.Indented));
                Console.WriteLine("================================ ");          
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Console.Read();
            }
        }


        /// <summary>
        /// Certificate base Auth and return token + client
        /// </summary>
        /// <returns></returns>
        static Token GetToken(App app, Certificate certificate)
        {
            var token = _authService.GetTokenByOAuthCba(app, certificate);

            return token;
        }



        /// <summary>
        /// Refresh Token
        /// </summary>
        /// <param name="refreshToken">refresh token</param>
        /// <returns>Token</returns>
        static Token RefreshToken(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentException("Invalid refresh token");

            var app = GetApp();

            var token = _authService.RefreshToken(app, refreshToken);

            return token;
        }

        static App GetApp()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "App.json");
            var content = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<App>(content);
        }


        static Certificate GetCertificate()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Certificate.json");
            var content = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Certificate>(content);
        }
    }
}
