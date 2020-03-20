// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

(function ($) {

    /** Local Variables **/

    var tokenAlertElm = $('#token-alert');
    var streamingLogElm = $('#streaming-log .panel-body');
    var subscriptionElm = $('#subscription');
    var streamingServerBtnElm = $('#streaming-server-btn');
    var subscribeFormElm = $('#subscribe-form');
    var unsubscribeFormElm = $('#unsubscribe-form');
    var snapshotElm = $('#snapshot');
    var messagesElm = $('#messages pre');
    var connectionStateElm = $('#connection-state');
    var currentContextElm = $('#current-context-id');
    var subscriptionStateElm = $('#subscription-state');
    var referenceIdAndMessagesElm = $('#reference-id-and-messages');

    /** Local Helper Methods **/

    function fixKeyValuePairs(match, pIndent, pKey, pVal, pComma) {
        var r = pIndent + '<span class=json-key>' + pKey + '</span>: ';
        if (pVal[0] == '{' || pVal[0] == '[') return r + pVal + pComma;
        if (pVal[0] == '"') return r + '<span class=json-string>' + pVal + '</span>' + pComma;
        return r + '<span class=json-value>' + pVal + '</span>' + pComma;
    }

    function fixLonelyStrings(match, pIndent, pStringValue, pComma) {
        return pIndent + '<span class=json-string>' + pStringValue + '</span>' + pComma;
    }

    function prettyPrint(obj) {
        if (!obj) return '';
        return JSON.stringify(obj, null, 3)
        .replace(/&/g, '&amp;')
        .replace(/\\"/g, '&quot;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/^(\s*)"([^"]+)":\s+(.*?)(,?)$/mg, fixKeyValuePairs)
        .replace(/^(\s*)("[^"]+")(,?)$/mg, fixLonelyStrings) + '\n';
    }

    function resetSubscriptionUi() {
        subscriptionStateElm.prop('title', 'inactive').removeClass('connected').addClass('disconnected');
        referenceIdAndMessagesElm.text('');
        unsubscribeFormElm.addClass('hidden');
        subscribeFormElm.removeClass('hidden');
    }

    /** Local Event Handlers **/

    // Event handler for state change events on the onnection
    function onStateChange(contextId, newState) {
        var oldState = connectionStateElm.prop('title');
        connectionStateElm.prop('title', newState);

        connectionStateElm.removeClass(oldState).addClass(newState)
        if (newState == 'disconnected') {
            streamingServerBtnElm.text('Connect');
            currentContextElm.text(' ');
        }
        else {
            currentContextElm.text('ContextId: ' + contextId);
        }
    }

    // Event handler for receiving initial snapshot when subscription becomes active
    function onActive(referenceId, numberOfMessages, snapshotItemMap) {
        subscriptionStateElm.prop('title', 'active').removeClass('connecting').addClass('connected');
        referenceIdAndMessagesElm.text('ReferenceId: ' + referenceId + ' | Messages: ' + numberOfMessages);
        snapshotElm.html(prettyPrint(snapshotItemMap));
    }

    // Event handler for receiving subscription messages
    function onMessage(referenceId, numberOfMessages, message, snapshotItemMap) {
        referenceIdAndMessagesElm.text('ReferenceId: ' + referenceId + ' | Messages: ' + numberOfMessages);
        if (message == 'heartbeat') {
        }
        else if (message == 'slow') {
            subscriptionStateElm.prop('title', 'slow').addClass('slow');
        }
        else if (message == 'reset') {
            resetSubscriptionUi();
        }
        else {
            subscriptionStateElm.prop('title', 'active').removeClass('slow');
            snapshotElm.html(prettyPrint(snapshotItemMap));
            messagesElm.prepend('<code class="pretty-print">' + prettyPrint(message) + '</code>');
        }
    }

    // Event handler for connecting and disconnecting
    function onConnectSubmit(e) {

        // Prevent actual form submition
        e.preventDefault();

        // If user forgot a token display error
        if (!this.token.value) {
            $(this.token).parent().addClass('has-error');
            tokenAlertElm.removeClass('hidden');
            return;
        }

        // Clear displayed token error (if any)
        $(this.token).parent().removeClass('has-error');
        tokenAlertElm.addClass('hidden');

        // If connected, disconnect
        if (OpenApiSampleLogic.isConnected()) {
            OpenApiSampleLogic.disconnect();
            streamingServerBtnElm.text('Connect');
            subscriptionElm.addClass('hidden');
            unsubscribeFormElm.addClass('hidden');
            subscribeFormElm.removeClass('hidden');
        }
            // If disconnected, connect
        else {
            OpenApiSampleLogic.connect(this.token.value, onStateChange);
            streamingServerBtnElm.text('Disconnect');
            subscriptionElm.removeClass('hidden');
        }
    }

    // Event handler for subscribing
    function onSubscribeSubmit(e) {

        // Prevent actual form submition
        e.preventDefault();

        OpenApiSampleLogic.subscribe(this.endpoint.value, JSON.parse(this.arguments.value), onActive, onMessage)

        snapshotElm.empty();
        messagesElm.empty();
        subscribeFormElm.addClass('hidden');
        unsubscribeFormElm.removeClass('hidden');
        subscriptionStateElm.prop('title', 'activating').removeClass('disconnected').addClass('connecting');
    }

    // Event handler for unsubscribing
    function onUnsubscribeSubmit(e) {

        // Prevent actual form submition
        e.preventDefault();

        OpenApiSampleLogic.unsubscribe();

        resetSubscriptionUi();
    }

    /** Immidiate Initialization **/

    $('#connect-form').on('submit', onConnectSubmit);
    subscribeFormElm.on('submit', onSubscribeSubmit);
    unsubscribeFormElm.on('submit', onUnsubscribeSubmit);

}(jQuery));