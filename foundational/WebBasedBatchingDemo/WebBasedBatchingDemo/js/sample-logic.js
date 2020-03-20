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
(function (global) {

    // Configuration

    var scheme = 'https';
    var host = 'gateway.saxobank.com';
    var baseUrl = '/sim/openapi/';
    var batchendPoint = 'port/batch';

    // Local Variables

    var accessToken;
    var boundary;
    var batchRequest;
    var batchResponse;

    // Local Helper Methods

    function onBatchRequestDone(result) {
        batchResponse = result.responseText;
        var promise = $.Deferred();
        promise.resolve(batchResponse);
        return promise;
    }

    // public Methods

    function createBatchRequest(token, endpoints) {
        accessToken = 'Bearer ' + token;
        boundary = btoa(new Date().toISOString());

        var requests = [];

        for (var i = 0, endpoint, request; endpoint = endpoints[i]; i++) {
            requests.push({ method: 'get', url: baseUrl + endpoint });
        }

        batchRequest = OpenApiBatch.build(requests, boundary, accessToken, host);
        return batchRequest;
    }

    function sendBatchRequest() {
        return $.ajax({
            type: 'POST',
            url: scheme + '://' + host + baseUrl + batchendPoint,
            dataType: 'json',
            beforeSend: function (request) {
                request.setRequestHeader('Authorization', accessToken);
            },
            data: batchRequest,
            contentType: 'multipart/mixed; boundary="' + boundary + '"'
        }).then(onBatchRequestDone, onBatchRequestDone);

    }

    function getParsedBatchResponse() {
        return OpenApiBatch.parse(batchResponse);
    }

    // Expose Methods

    global.OpenApiSampleLogic = {
        createBatchRequest: createBatchRequest,
        sendBatchRequest: sendBatchRequest,
        getParsedBatchResponse: getParsedBatchResponse,
    };

}(this));

