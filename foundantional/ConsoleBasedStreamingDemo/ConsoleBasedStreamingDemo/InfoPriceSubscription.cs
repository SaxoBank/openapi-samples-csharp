// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleBasedStreamingDemo
{
    /// <summary>
    /// Represents info price subscriptions.
    /// </summary>
    /// <remarks>
    /// The subscription keeps a queue of updates for two reasons:
    ///
    /// 1. The data snapshot from the subscription request may be received later than the
    ///    first streaming update, i.e. it must wait for the snapshot before applying updates.
    ///
    /// 2. Large updates may be split into a number of smaller updates. The subscription 
    ///    waits for the last update of a series of partial updates before the updates are
    ///    applied.
    /// </remarks>
    class InfoPriceSubscription
    {
        public readonly object DataLock = new object();

        /// <summary>Queue of updates waiting to be processed.</summary>
        private Queue<JArray> _updateQueue = new Queue<JArray>();

        /// <summary>Indicates whether the update queue is ready to be processed.</summary>
        /// <remarks>
        /// The queue can be incomplete the last update in a series of partial updates has not arrived.
        /// </remarks>
        private bool _updateQueueComplete;

        /// <summary>The reference id that(along with streaming context id and session id) identifies the subscription (within the context of a specific service/subscription type).</summary>
        public string ReferenceId { get; }

        /// <summary>The UICs of the instruments that are subscribed to.</summary>
        public IReadOnlyCollection<int> Uics { get; }

        /// <summary>The asset type of the instruments that are subscribed to.</summary>
        public string AssetType { get; }

        /// <summary>The time that the client should accept the subscription to be inactive before considering it invalid.</summary>
        public TimeSpan InactivityTimeout { get; set; }

        private DateTime _lastUpdated;

        /// <summary>The current data snapshot of the subscription.</summary>
        /// <remarks>
        /// This property is updated when the snapshot is set and when streaming updates are
        /// handled.
        /// </remarks>
        public InfoPrice[] Data { get; set; }

        ///<summary>Indicates whether the subscription is inactive, i.e. if neither a heartbeat nor data has been received.</summary>
        public bool Inactive => DateTime.Now - _lastUpdated > InactivityTimeout;

        /// <summary>
        /// Updates the subscription activity.
        /// </summary>
        public void UpdateActivity()
        {
            _lastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Sets the initial data snapshot on the subscription.
        /// </summary>
        /// <remarks>
        /// The snapshot is parsed and the <see cref="Data"/> property is updated with the value.
        /// Finally, any waiting streaming updates are applied to the snapshot.
        /// </remarks>
        public void SetSnapshot(JToken jsonSnapshot)
        {
            var infoPrices = new List<InfoPrice>();
            var jsonPrices = (JArray)jsonSnapshot;
            foreach (var jsonPrice in jsonPrices)
            {
                var jsonQuote = jsonPrice["Quote"];
                var quote = new Quote
                {
                    Amount = (int)jsonQuote["Amount"],
                    Ask = (decimal)jsonQuote["Ask"],
                    Bid = (decimal)jsonQuote["Bid"],
                    Mid = (decimal)jsonQuote["Mid"],
                    DelayedByMinutes = (int)jsonQuote["DelayedByMinutes"]
                };

                var infoPrice = new InfoPrice
                {
                    Uic = (int)jsonPrice["Uic"],
                    AssetType = (string)jsonPrice["AssetType"],
                    Quote = quote
                };

                infoPrices.Add(infoPrice);
            }

            lock (DataLock)
            {
                Data = infoPrices.ToArray();

                // Flush the update queue to apply all updates that were received before the snapshot
                if (_updateQueueComplete)
                    FlushUpdateQueue();
            }
        }

        /// <summary>
        /// Applies an update to the <see cref="Data"/> property.
        /// </summary>
        private void ApplyUpdate(JArray jsonPrices)
        {
            foreach (var jsonPrice in jsonPrices)
            {
                // Identify the updated info price
                var uic = (int)jsonPrice["Uic"];
                var infoPrice = Data.FirstOrDefault(p => p.Uic == uic);
                if (infoPrice == null)
                {
                    Console.Error.WriteLine($"[{ReferenceId}]: Received an update for an unknown UIC: {uic}");
                    continue;
                }

                // Apply updated fields
                var jsonQuote = jsonPrice["Quote"];
                if (jsonQuote != null)
                {
                    if (jsonQuote["Amount"] != null)
                    {
                        infoPrice.Quote.Amount = (int)jsonQuote["Amount"];
                    }
                    if (jsonQuote["Ask"] != null)
                    {
                        infoPrice.Quote.Ask = (decimal)jsonQuote["Ask"];
                    }
                    if (jsonQuote["Bid"] != null)
                    {
                        infoPrice.Quote.Bid = (decimal)jsonQuote["Bid"];
                    }
                    if (jsonQuote["Mid"] != null)
                    {
                        infoPrice.Quote.Mid = (decimal)jsonQuote["Mid"];
                    }
                    if (jsonQuote["DelayedByMinutes"] != null)
                    {
                        infoPrice.Quote.DelayedByMinutes = (int)jsonQuote["DelayedByMinutes"];
                    }
                }
            }
        }

        /// <summary>
        /// Indicates whether an update is complete and ready for applying to the <see cref="Data"/> property.
        /// </summary>
        private static bool IsComplete(JToken update)
        {
            // If there is not a partition number and partition count the update is complete
            var pn = update["__pn"];
            var pc = update["__pc"];
            if (pn == null || pc == null)
                return true;
            
            // The update is only complete when the partition is the last in the series of partitions.
            var partitionNumber = (int)pn;
            var partitionCount = (int)pc;
            return partitionNumber == partitionCount - 1;
        }

        /// <summary>
        /// Enqueues an update for later processing.
        /// </summary>
        private void EnqueueUpdate(JToken update)
        {
            var data = (JArray)update["Data"];
            _updateQueueComplete = IsComplete(update);
            _updateQueue.Enqueue(data);
        }

        /// <summary>
        /// Applies all the updates in the queue to the <see cref="Data"/> object.
        /// </summary>
        private void FlushUpdateQueue()
        {
            while (_updateQueue.Count > 0)
            {
                var update = _updateQueue.Dequeue();
                ApplyUpdate(update);
            }
        }

        /// <summary>
        /// Handles a streaming update for the subscription.
        /// </summary>
        /// <remarks>
        /// The streaming update is applied to the <see cref="Data"/> property, but it may be held
        /// in a queue if the subscription snapshot has not yet been received or if the update
        /// is not the last in a series of partial updates.
        /// </remarks>
        public void HandleUpdate(JToken update)
        {
            lock (DataLock)
            {
                EnqueueUpdate(update);

                // Return if the snapshot has not been received
                if (Data == null)
                    return;

                // Apply waiting updates if the queue can be flushed
                if (_updateQueueComplete)
                    FlushUpdateQueue();
            }
        }

        public InfoPriceSubscription(string referenceId, IEnumerable<int> uics, string assetType)
        {
            ReferenceId = referenceId;
            Uics = uics.ToArray();
            AssetType = assetType;

            // Set a default in case the activity monitor performs a cleanup before the subscription response is parsed
            InactivityTimeout = TimeSpan.FromMinutes(2);
            UpdateActivity();
        }
    }
}
