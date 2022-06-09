using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenAPI.Models;
using Sample.Authentication.Server.Services;

namespace Sample.Authentication.Server.Controllers
{
    /// <summary>
    /// This sample only shows how to sign in a single user with the Code Flow.
    /// Refer to: https://www.developer.saxo/openapi/learn/oauth-authorization-code-grant for details.
    /// Token management for a large number of users is not incorporated in this sample.
    /// 
    /// Three URLs:
    /// https://localhost:44315/startup/app
    /// https://localhost:44315/startup
    /// https://localhost:44315/startup/token/{refreshToken}
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class StartupController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ClientService _clientService;

        public StartupController()
        {
            _authService = new AuthService();
            _clientService = new ClientService();
        }

        /// <summary>
        /// Redirect to the login page
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                App app = GetApp();
                string redirectUrl = _authService.GetAuthenticationRequest(app);

                return this.Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Please make sure that the RedirectURL points to this endpoint
        /// Callback - Get access token by authentication code
        /// </summary>
        /// <param name="error">Optional error code</param>
        /// <param name="error_description">Optional error description</param>
        /// <param name="code">Authentication code</param>
        /// <param name="state">State</param>
        /// <returns>Token</returns>
        [HttpGet]
        [Route("authorization")]
        public IActionResult Autorization(string error, string error_description, string code, string state)
        {
            try
            {
                // Make sure the customer knows something went wrong. A common issue is the account not being active yet, due to the initial deposit.
                if (string.IsNullOrEmpty(code))
                    return this.BadRequest("Error during authorization (" + error + "): " + error_description);
                // Compare the state used when generating the URL with the received state - they must match
                if (state != Models.Constants.State)
                    return this.BadRequest("Invalid state returned");
                
                App app = GetApp();

                Models.Token token = _authService.GetToken(app, code);

                dynamic exampleApiResponse = _clientService.GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);

                return this.Ok(new { Token = token, ExampleApiResponse = JsonConvert.SerializeObject(exampleApiResponse) });
            }
            catch (Exception ex)
            {
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Refresh Token
        /// </summary>
        /// <param name="refreshToken">refresh token</param>
        /// <returns>Token</returns>
        [HttpGet]
        [Route("token/{refreshToken}")]
        public IActionResult RefreshToken(string refreshToken)
        {
            try
            {
                if (string.IsNullOrEmpty(refreshToken))
                    return this.BadRequest("Invalid refresh token");

                App app = GetApp();

                Models.Token token = _authService.RefreshToken(app, refreshToken);

                dynamic exampleApiResponse = _clientService.GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);

                return this.Ok(new { Token = token, ExampleApiResponse = JsonConvert.SerializeObject(exampleApiResponse) });
            }
            catch (Exception ex)
            {
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Read App from configuration file
        /// </summary>
        /// <returns>App</returns>
        [HttpGet]
        [Route("app")]
        public App GetApp()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "App.json");
            Console.WriteLine("Reading app config: " + path);
            string content = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<App>(content);
        }
    }
}