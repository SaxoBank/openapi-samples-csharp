// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.
(function (global) {

	/** configuration **/

	var streamingUrl = 'https://streaming.saxotrader.com/sim/openapi/streaming/connection';
	var subscriptionBaseUrl = 'https://gateway.saxobank.com/sim/openapi/';

	/** Local Variables **/

	var stream;
	var subscription;
	var snapshotItemMap = {};
	var snapshot;

	/** Local Helper Methods **/

	function applyDelta(target, delta) {
		for (var prop in delta) {
			if (typeof delta[prop] != 'object' || typeof target[prop] != 'object') {
				target[prop] = delta[prop];
			}
			else if (delta[prop] instanceof Array) {
				//for infoprices we can safely asumed that all arrays are to be treated as value types
				target[prop] = delta[prop];
			}
			else {
				applyDelta(target[prop], delta[prop]);
			}
		}
	}

	/** Public Methods **/

	function isConnected() {
		return stream && stream.isConnected;
	}

	function connect(token, stateChangeCallback) {
		stream = new OpenApiStream(streamingUrl, token);
		stream.observeState(stateChangeCallback);
		stream.connect();
	}

	function disconnect() {
		if (stream) stream.disconnect();
	}

	function subscribe(subscribeEndpoint, args, snapshotCallback, msgCallback) {

		// Create a subscription representation
		subscription = new OpenApiSubscription(stream, subscriptionBaseUrl + subscribeEndpoint, args, 1000);

		//observe messages for the subscription
		subscription.observe(function (message) {

			// If the type of message is object then we received data
			if (typeof message == 'object') {

				// Update the snapshots data
				for (var i = 0, item; item = message[i]; i++) {
					//Uic not found in map, this is an add situation
					if (!snapshotItemMap[item.Uic]) {
						snapshotItemMap[item.Uic] = item;
						snapshot.push(item);
					}
						//this is a delete situation
					else if (item.__meta_deleted) {
						//remove object from map
						delete snapshotItemMap[item.Uic];
						//remove object from snapshot
						for (var j = 0, l = snapshot.length; j < l; j++) {
							if (snapshot[j].Uic == item.Uic) {
								snapshot.splice(j, 1);
							}
						}
					}
						//apply delta
					else {
						applyDelta(snapshotItemMap[item.Uic], item);
					}
				}

				// Send the data back to the ui
				msgCallback(this.subscription.referenceId, this.subscription.messages.length, message, snapshot);
			}
				//subscription has been reset, create a new and hook it up
			else if (message == 'reset') {
				subscribe(this.subscribeEndpoint, this.args, this.snapshotCallback, this.msgCallback);
			}
			else if (message == 'disabled') {
				// Subscription was disabled permanently on the serverside, don't try to re-subscribe
				msgCallback(this.subscription.referenceId, this.subscription.messages.length, message);
			}
				//message is either 'slow' or 'heartbeat'
			else {
				msgCallback(this.subscription.referenceId, this.subscription.messages.length, message);
			}

		}.bind({
			subscription: subscription,
			subscribeEndpoint: subscribeEndpoint,
			args: args,
			snapshotCallback: snapshotCallback,
			msgCallback: msgCallback
		}));

		// Enable the subscription and get the initial data snapshot
		subscription.enable(function (snapshotData) {
			// "this" is set to the OpenApiSubscription object
			var refId = this.referenceId;
			var numberOfMessages = this.messages.length;

			snapshot = snapshotData;
			// Create a map with Uics as keys for easilyb update the snapshot when messages arrive
			for (var i = 0, item; item = snapshot[i]; i++) {
				snapshotItemMap[item.Uic] = item;
			}

			// Send data back to the ui code
			snapshotCallback(refId, numberOfMessages, snapshot);

		}.bind(subscription));

	}

	function unsubscribe() {
		subscription.disable();
	}

	/** Expose Public Methods **/

	global.OpenApiSampleLogic = {
		isConnected: isConnected,
		connect: connect,
		disconnect: disconnect,
		subscribe: subscribe,
		unsubscribe: unsubscribe
	};

}(this));