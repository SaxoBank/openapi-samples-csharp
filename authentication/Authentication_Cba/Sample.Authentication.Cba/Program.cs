using Newtonsoft.Json;
using Sample.Authentication.Cba.Services;
using System;
using System.IO;
using Sample.Authentication.Cba.Models;

namespace Sample.Authentication.Cba
{
    class Program
    {
        static readonly CbaAuthService AuthService = new CbaAuthService();

        static void Main()
        {
            try
            {
                App app = GetApp();
                Certificate certificate = GetCertificate();

                Token token = GetToken(app, certificate);
                dynamic client = new ClientService().GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);
                Console.WriteLine("Received token object (don't log in your own app):");
                Console.WriteLine(JsonConvert.SerializeObject(new { Token = token, Client = client }, Formatting.Indented));
                Console.WriteLine("================================ ");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Refreshing Token... ");
                Token newToken = RefreshToken(token.RefreshToken);
                client = new ClientService().GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);
                Console.WriteLine("New token object (don't log in your own app):");
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
            Token token = AuthService.GetTokenByOAuthCba(app, certificate);
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

            App app = GetApp();

            Token token = AuthService.RefreshToken(app, refreshToken);

            return token;
        }

        static App GetApp()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "App.json");
            Console.WriteLine("Reading app config: " + path);
            string content = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<App>(content);
        }

        static Certificate GetCertificate()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Certificate.json");
            Console.WriteLine("Reading certificate config: " + path);
            string content = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Certificate>(content);
        }
    }
}
