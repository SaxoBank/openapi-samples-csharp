// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

// Please refer to developer's portal for access token.
(function (global, $) {

    // Local Variables

    var tokenAlertElm = $('#token-alert');
    var batchRequestElm = $('#send-batch-requst');
    var batchResponseElm = $('#batch-response');
    var parsedResponseElm = $('#parsed-response');

    // Local Helper Methods

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
        .replace(/^(\s*)("[^"]+")(,?)$/mg, fixLonelyStrings);
    }

    // Local Event Handlers

    function onCreateBatchRequestSubmit(e) {

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

        // Get endpoint urls from form elements
        var endpoints = Array.prototype.map.call(this.endpoints, function (endpoint) { return endpoint.value; })

        // Show the batch request
        var batchRequest = OpenApiSampleLogic.createBatchRequest(this.token.value, endpoints);
        batchRequestElm.removeClass('hidden').find('code').text(batchRequest);
    }

    function onBatchRequestSubmit(e) {

        // Prevent actual form submition
        e.preventDefault();

        // Send the batch request
        OpenApiSampleLogic
            .sendBatchRequest()
            .then(function (result) {
                //show the batch response
                batchResponseElm.removeClass('hidden').find('code').text(result);
            });
    }

    function onBatchResponseSubmit(e) {

        // Prevent actual form submition
        e.preventDefault();

        // Get parsed response
        var parsedResponse = OpenApiSampleLogic.getParsedBatchResponse();

        // Split the responses
        var prettyResponses = []
        for (var i = 0, response; response = parsedResponse[i]; i++) {

            prettyResponses.push('<pre><code class="pretty-print">' + prettyPrint(response) + '</code></pre>');

        }

        // Show the parsed response
        parsedResponseElm.removeClass('hidden').find('.panel-body').html(prettyResponses.join(''));
    }

    // Immidiate Initialization

    $('#create-batch-form').on('submit', onCreateBatchRequestSubmit)
    batchRequestElm.on('submit', onBatchRequestSubmit)
    batchResponseElm.on('submit', onBatchResponseSubmit)

}(this, jQuery));