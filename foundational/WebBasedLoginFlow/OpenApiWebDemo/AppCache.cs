// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System.Configuration;

namespace OpenApiWebDemo
{
    /// <summary>
    /// Simple static cache used for configuration values and Open API tokens.
    /// </summary>
    public class AppCache
    {
        public static OpenApiOAuth2TokenResponse AccessToken;
        
        public static string AppKey { get; private set; }
        public static string AppSecret{ get; private set; }
        public static string AuthenticationUrl { get; private set; }
        public static string OpenApiBaseUrl { get; private set; }

        static AppCache()
        {
            AppKey              = ConfigurationManager.AppSettings["AppKey"];    
            AppSecret           = ConfigurationManager.AppSettings["AppSecret"];    
            AuthenticationUrl   = ConfigurationManager.AppSettings["AuthenticationUrl"];    
            OpenApiBaseUrl      = ConfigurationManager.AppSettings["OpenApiBaseUrl"];    
        }
    }
}