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

    var requestRx = /X-Request-Id: ([0-9]+)/;
    var httpCodeRx = /HTTP\/1.1 ([0-9]+)/;

    function parseBatch(responseText) {

        if (!responseText) {
            throw new Error("Required Parameter: responseText in batch parse");
        }

        var lines = responseText.split('\r\n');
        var responseBoundary = lines[0];
        var currentData = null;
        var requestId = null;
        var responseData = [];

        for (var i = 0, l = lines.length; i < l; i++) {
            var line = lines[i];
            if (line.length) {

                if (!responseData[requestId]) {
                    requestId = line.match(requestRx);
                    if (requestId) {
                        requestId = parseInt(requestId[1], 10);
                        responseData[requestId] = {};
                    }
                }

                if (line.indexOf(responseBoundary) === 0) {
                    if (currentData) {
                        requestId = requestId === null ? responseData.length : requestId;
                        responseData[requestId] = currentData;
                    }

                    requestId = null;
                    currentData = {};
                } else if (currentData) {
                    if (!currentData.status) {
                        var statusMatch = line.match(httpCodeRx);
                        if (statusMatch) {
                            // Change the status to be a number to match fetch
                            currentData.status = Number(statusMatch[1]);
                        }
                    } else if (!currentData.response) {
                        var firstCharacter = line.charAt(0);
                        if (firstCharacter === '{' || firstCharacter === '[') {
                            currentData.response = JSON.parse(line);
                        }
                    }
                }
            }
        }

        return responseData;
    }

    function buildBatch(subRequests, boundary, authToken, host) {

        if (!subRequests || !boundary || !authToken || !host) {
            throw new Error("Missing required parameters: batch build requires all 4 parameters");
        }

        var body = [];

        for (var i = 0, l = subRequests.length; i < l; i++) {
            var request = subRequests[i];
            var method = request.method.toUpperCase();

            body.push('--' + boundary);
            body.push('Content-Type: application/http; msgtype=request', '');

            body.push(method + ' ' + request.url + ' HTTP/1.1');
            body.push('X-Request-Id: ' + i);
            if (request.headers) {
                for (var header in request.headers) {
                    if (request.headers.hasOwnProperty(header)) {
                        body.push(header + ": " + request.headers[header]);
                    }
                }
            }

            body.push("Authorization" + ': ' + authToken);

            // Don't care about content type for requests that have no body.
            if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
                body.push('Content-Type: application/json; charset=utf-8');
            }

            body.push('Host: ' + host, '');
            body.push(request.data || "");
        }

        body.push('--' + boundary + '--', '');
        return body.join('\r\n');
    }

    global.OpenApiBatch = {
        parse: parseBatch,
        build: buildBatch
    };

}(this));