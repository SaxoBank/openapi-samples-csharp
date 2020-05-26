namespace Sample.Authentication.Cba.Models
{
    public class App
    {
        /// <summary>
        /// The application key, to be found on https://www.developer.saxo/openapi/appmanagement
        /// </summary>
        public string AppKey { get; set; }

        /// <summary>
        /// Secret (remember, secrets have an expiry date)
        /// </summary>
        public string AppSecret { get; set; }

        /// <summary>
        /// On SIM this is https://sim.logonvalidation.net/token and on live this will be https://live.logonvalidation.net/token
        /// </summary>
        public string TokenEndpoint { get; set; }

        /// <summary>
        /// The base URL of the API
        /// </summary>
        public string OpenApiBaseUrl { get; set; }

        /// <summary>
        /// The unique identifier of your application
        /// On https://www.developer.saxo/openapi/appmanagement this can be found under the application redirect URL
        /// </summary>
        public string ServiceProviderUrl { get; set; }
    }
}
