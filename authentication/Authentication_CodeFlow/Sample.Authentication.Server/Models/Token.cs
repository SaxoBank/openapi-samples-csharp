using Newtonsoft.Json;
using System;

namespace Sample.Authentication.Server.Models
{
    public class Token
    {
        public DateTime IssueTime
        {
            get { return DateTime.UtcNow; }
        }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("refresh_token_expires_in")]
        public int RefreshTokenExpiresIn { get; set; }

    }
}
