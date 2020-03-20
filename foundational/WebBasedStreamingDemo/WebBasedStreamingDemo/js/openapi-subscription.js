// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

(function (global, $) {
    'use strict';

    var refIdCounter = 0;

    /** Private Methods **/

    // Send message to all message observers
	function sendDataToObservers(data) {
        for (var i = 0, observer; observer = this.observers[i]; i++) {
			observer.call(this, data);
        }
    }

    // Send all messages in the queue to all message observers
    function flushQueue() {
        for (var msg; msg = this.queue.shift() ;) {
			sendDataToObservers.call(this, msg.data);
        }
    }

    /** Event Handler Methods **/

	//handle inactivity
	function onInactivity() {
		disable.call(this);
		sendDataToObservers.call(this, 'reset');
	}

    // Handle activation success
    function onSubscribeSuccess(result) {
        this.result = result;
        this.onSubscribeCallback.call(this, result.Snapshot.Data);
        this.active = true;
        flushQueue.call(this);
		this.inactivityTimeout = setTimeout(onInactivity.bind(this), this.result.InactivityTimeout * 1000);
    }

    // Handle activation failure
    function onSubscribeFail(result) {
        this.result = result;
        this.onSubscribeCallback.call(this, result);
    }

    // Handle message events
	function onStreamData(type, timestamp, data) {
        this.timeOfLastMessage = new Date(timestamp);
        if (type == 'data') {
			var msg = { type: type, timestamp: timestamp, data: data };
            this.messages.push(msg);
            if (!this.active) {
                this.queue.push(msg);
                return;
            }
			sendDataToObservers.call(this, data);
        }
        else if (type == 'reset') {
			sendDataToObservers.call(this, 'reset');
            disable.call(this);
        }
        else if (type == 'disabled') {
			sendDataToObservers.call(this, 'disabled');
            disable.call(this);
        }
        else if (type == 'slow') {
			sendDataToObservers.call(this, 'slow');
        }
        else if (type == 'heartbeat') {
			sendDataToObservers.call(this, 'heartbeat');
        }
		clearTimeout(this.inactivityTimeout);
		this.inactivityTimeout = setTimeout(onInactivity.bind(this), this.result.InactivityTimeout * 1000);
    }

    /** Prototype Methods **/

    // Make the call to the api to create the subscription
    function enable(callback) {
        if (typeof callback !== 'function') throw new TypeError('callback must be a function');
        this.onSubscribeCallback = callback;

        var payload = {
            Arguments: this.arguments,
            ContextId: this.stream.contextId,
            ReferenceId: this.referenceId
        };
        if (this.refreshRate) payload.RefreshRate = this.refreshRate;
        if (this.tag) payload.Tag = this.tag;

        $.ajax({
            method: "POST",
            url: this.baseUrl + '/active',
            headers: {
                authorization: 'Bearer ' + this.stream.token,
                accept: 'application/json',
                'content-type': 'application/json'
            },
            data: JSON.stringify(payload),
            xhrFields: {
                withCredentials: true
            }
        }).then(onSubscribeSuccess.bind(this), onSubscribeFail.bind(this));

    }

    // Make the call to the api to delete the subscription
    function disable() {
        this.active = false;

        $.ajax({
            method: "DELETE",
            url: this.baseUrl + '/' + this.stream.contextId + '/' + this.referenceId,
            headers: {
                authorization: 'Bearer ' + this.stream.token,
            },
            xhrFields: {
                withCredentials: true
            }
        });

    }

    // Bind a callback to message evnets 
    function observe(callback) {
        if (typeof callback !== 'function') throw new TypeError('callback must be a function');
        this.observers.push(callback);
    }

    /** Public Methods **/

    // Representation of streaming subscription
    function OpenApiSubscription(openApiStream, baseUrl, args, refreshRate, tag) {
        this.baseUrl = baseUrl;
        this.stream = openApiStream;
        if (tag) this.tag = tag;
        if (refreshRate) this.refreshRate = refreshRate;
        this.referenceId = ++refIdCounter;
        this.arguments = args;
        this.messages = [];
        this.observers = [];
        this.queue = [];
        this.active = false;

        this.stream.observeMessages(this.referenceId, onStreamData.bind(this));
    }

    OpenApiSubscription.prototype = {
        observe: observe,
        enable: enable,
        disable: disable
    };

    /** Expose Public Methods **/

    global.OpenApiSubscription = OpenApiSubscription;

}(this, jQuery));