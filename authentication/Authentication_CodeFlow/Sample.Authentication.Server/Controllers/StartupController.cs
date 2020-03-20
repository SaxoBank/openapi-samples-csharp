using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenAPI.Models;
using System.Web.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Sample.Authentication.Server.Models;
using Sample.Authentication.Server.Services;

namespace Sample.Authentication.Server.Controllers
{
    /// <summary>
    /// This sample only shows how to sign in a single user with the Code Flow
    /// Refer to : https://www.developer.saxo/openapi/learn/oauth-authorization-code-grant for details
    /// Token management and state management for a large number of users are not incorporated in this sample.
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class StartupController : ControllerBase
    {
        private AuthService _authService;
        private ClientService _clientService;

        public StartupController()
        {
            _authService = new AuthService();
            _clientService = new ClientService();
        }

        /// <summary>
        /// Redirect to the login page SAXO
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                var app = GetApp();
                var redirectUrl = _authService.GetAuthenticationRequest(app);

                return this.Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Plese make sure that the RedirectURL points to this endpoint
        /// Callback by SAXO - Get access token by authentication code
        /// </summary>
        /// <param name="code">authentication code</param>
        /// <param name="state">state</param>
        /// <returns>Token</returns>
        [HttpGet]
        [Route("authorization")]
        public IActionResult Autorization(string code, string state)
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                    return this.BadRequest("Invalid authorization code");

                var app = GetApp();

                var token = _authService.GetToken(app, code);

                var client = _clientService.GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);

                return this.Ok(new { Token = token, Client = client });
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

                var app = GetApp();

                var token = _authService.RefreshToken(app, refreshToken);

                var client = _clientService.GetClient(app.OpenApiBaseUrl, token.AccessToken, token.TokenType);

                return this.Ok(new { Token = token, Client = client });
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
            var path = Path.Combine(AppContext.BaseDirectory, "App.json");
            var content = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<App>(content);
        }
    }
}