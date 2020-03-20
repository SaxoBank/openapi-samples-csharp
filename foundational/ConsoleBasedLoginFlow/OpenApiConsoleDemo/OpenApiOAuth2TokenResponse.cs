// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System.Runtime.Serialization;

namespace OpenApiConsoleDemo
{
    [DataContract]
    public class OpenApiOAuth2TokenResponse
    {
        /// <summary>
        /// The access token; used for accessing Open API services
        /// </summary>
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        /// <summary>
        /// The token type
        /// </summary>
        [DataMember(Name = "token_type")]
        public string TokenType { get; set; }

        /// <summary>
        /// The expiry of the access token in seconds
        /// </summary>
        [DataMember(Name = "expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// The Refresh token; used for getting a new access token
        /// </summary>
        [DataMember(Name = "refresh_token")]
        public string RefreshToken { get; set; }

        /// <summary>
        /// The expiry of the refresh token in seconds
        /// </summary>
        [DataMember(Name = "refresh_token_expires_in")]
        public int RefreshTokenExpiresIn { get; set; }

        /// <summary>
        /// The base URI of the Open API service groups
        /// </summary>
        [DataMember(Name = "base_uri")]
        public string BaseUri { get; set; }
    }
}