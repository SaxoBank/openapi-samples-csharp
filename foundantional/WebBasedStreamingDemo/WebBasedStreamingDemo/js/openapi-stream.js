// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

(function (global, signalR) {
    'use strict';

    /** Private Methods **/

    // Generate a guid or use as contextId
    function createGuid() {
        // http://stackoverflow.com/questions/105034/how-to-create-a-guid-uuid-in-javascript/2117523#2117523 
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0,
                v = c == 'x' ? r : r & 0x3 | 0x8;
            return v.toString(16);
        });
    }


    // Broadcast state change events to state change observers
    function broadcastToStateObservers(message) {
        for (var i = 0, observer; observer = this.stateObservers[i]; i++) {
            observer(this.contextId, message);
        }
    }

    // Boadcast message events to message observers
    function broadcastToMessageObservers(type, referenceId, timestamp, data) {
        var observers = this.messageObservers[referenceId];
        if (observers) {
            for (var i = 0, observer; observer = observers[i]; i++) {
                observer(type, timestamp, data);
            }
        }
    }

    // Unpack _resetsubscriptions messages and call message observers with a 'reset' type
    function handleResetSubscriptions(message) {
        var refIds = message.TargetReferenceIds;
        if (!refIds || !refIds.length) {
            for (var referenceId in this.messageObservers) {
                this.messageObservers[referenceId]('reset', message.Timestamp);
            }
        }
        else for (var i = 0, l = refIds.length; i < l; i++) {
            broadcastToMessageObservers.call(this, 'reset', message.refIds[i], message.Timestamp);
        }
    }

    // Unpack _heartbeat messages and call message observers with a corresponding message type
    function handleHeartbeat(message) {
        for (var i = 0, heartbeat; heartbeat = message.Heartbeats[i]; i++) {
            switch (heartbeat.Reason) {
                case "NoNewData":
                    broadcastToMessageObservers.call(this, 'heartbeat', heartbeat.OriginatingReferenceId, message.Timestamp);
                    break;
                case "SubscriptionTemporarilyDisabled":
                    broadcastToMessageObservers.call(this, 'slow', heartbeat.OriginatingReferenceId, message.Timestamp);
                    break;
                case "SubscriptionPermanentlyDisabled":
                    broadcastToMessageObservers.call(this, 'disabled', heartbeat.OriginatingReferenceId, message.Timestamp);
                    break;
            }
        }
    }

	function handlePartition(message) {
		//This sample does not handle partitions, please refer to the documentation on the developer portal for partion specifications
		broadcastToMessageObservers.call(this, 'data', message.ReferenceId, message.Timestamp, message.Data);
	}

    /** Event Handlers **/

    // Event handler for message events
    function onMessageReceived(messages) {
        for (var i = 0, message; message = messages[i]; i++) {
            if (message.ReferenceId == '_heartbeat') {
                handleHeartbeat.call(this, message);
            }
            else if (message.ReferenceId == '_resetsubscriptions') {
                handleResetSubscriptions.call(this, message);
            }
            else if (message.ReferenceId.substr(0, 1) == '_') {
                //ignore unknown control messages
			}
			else if (typeof message.__pn != 'undefined' && typeof message.__pc != 'undefined') {
				handlePartition.call(this, message);
            }
            else {
                broadcastToMessageObservers.call(this, 'data', message.ReferenceId, message.Timestamp, message.Data);
            }
        }
    }

    // Event handler for state change events
    function onStateChanged(change) {
        var message;
        switch (change.newState) {
            case signalR.connectionState.connecting:
                message = "connecting";
                this.isConnecting = true;
                this.isConnected = false;
                break;
            case signalR.connectionState.reconnecting:
                message = "reconnecting";
                this.isConnecting = true;
                this.isConnected = false;
                break;
            case signalR.connectionState.connected:
                message = "connected";
                this.isConnecting = false;
                this.isConnected = true;
                break;
            case signalR.connectionState.reconnected:
                message = "reconnected";
                this.isConnecting = false;
                this.isConnected = true;
                break;
            case signalR.connectionState.disconnected:
                message = "disconnected";
                this.isConnecting = false;
                this.isConnected = false;
                break;
            default:
                message = "unknown";
                break;
        }
        broadcastToStateObservers.call(this, message);
    }

    /** Prototype Methods **/

    // Connect to streaming server
    function connect() {
        if (this.isConnecting || this.isConnected) return;
        this.connection.start({ waitForPageLoad: false, transport: ['webSockets'] });
    }

    // Disconnect from streaming server
    function disconnect() {
        this.connection.stop(true, true);
    }

    // Bind a callback to state change events on the connection
    function observeState(callback) {
        if (typeof callback !== 'function') throw new TypeError('callback must be a function');
        this.stateObservers.push(callback);
    }

    // Bind a callback to message events on the connection
    function observeMessages(referenceId, callback) {
        if (typeof callback !== 'function') throw new TypeError('callback must be a function');

        if (!this.messageObservers[referenceId]) this.messageObservers[referenceId] = [];
        this.messageObservers[referenceId].push(callback);
    }

    /** Public Methods **/

    // Representation of a streaming connection
    function OpenApiStream(connectUrl, token) {
        this.isConnecting = false;
        this.isConnected = false;
        this.connectUrl = connectUrl;
        this.token = token;
        this.contextId = createGuid();
        this.messageObservers = {};
        this.stateObservers = [];
		this.currentPartitions = {};
        this.connection = signalR(this.connectUrl, { authorization: this.token, context: this.contextId });
        this.connection.received(onMessageReceived.bind(this));
        this.connection.stateChanged(onStateChanged.bind(this));
    }

    OpenApiStream.prototype = {
        connect: connect,
        disconnect: disconnect,
        observeState: observeState,
        observeMessages: observeMessages
    };

    /** Expose Public Methods **/

    global.OpenApiStream = OpenApiStream;

}(this, jQuery.signalR));