// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System;
using System.Web;

namespace OpenApiWebDemo
{
    public partial class TokenRefresh : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var token = AppCache.AccessToken;
            
            // If we don't have a token, redirect back to the main page to trigger the authentication flow
            if (token == null)
            {
                HttpContext.Current.Response.Redirect("default.aspx");
                return;
            }

            // Refresh token
            var refreshedToken = OpenApiAuthHelper.RefreshToken(AppCache.AuthenticationUrl, AppCache.AppKey, AppCache.AppSecret, token.RefreshToken).Result;
            if (refreshedToken != null)
            {
                AppCache.AccessToken = refreshedToken;
                Status = "Token refreshed";
            }
            else
            {
                Status = "Token refresh failed";
            }
        }

        // Model for UI
        public string Status { get; set; }

        public string TokenValue => AppCache.AccessToken.AccessToken ?? "null";
    }
}