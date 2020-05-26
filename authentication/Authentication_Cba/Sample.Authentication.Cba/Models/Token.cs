using System;
using Newtonsoft.Json;

namespace Sample.Authentication.Cba.Models
{
    public class Token
    {
        public DateTime IssueTime => DateTime.UtcNow;

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
