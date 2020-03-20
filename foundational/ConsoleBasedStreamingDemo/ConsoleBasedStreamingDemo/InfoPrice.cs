// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

namespace ConsoleBasedStreamingDemo
{
    // -----------------------------------------------------------------------------------------
    // These classes represent data received from info price subscriptions.
    // 
    // For demonstration purposes the classes are not complete with respect to the actual data 
    // sent from the Open API.
    // -----------------------------------------------------------------------------------------

    class InfoPrice
    {
        public int Uic { get; set; }

        public string AssetType { get; set; }

        public Quote Quote { get; set; }
    }

    class Quote
    {
        public int Amount { get; set; }

        public decimal Bid { get; set; }

        public decimal Ask { get; set; }

        public decimal Mid { get; set; }

        public int DelayedByMinutes { get; set; }
    }
}