# Console Application - Streaming

This sample demonstrates how to:

 - ensure server stickiness when using Open API
 - create a streaming connection using the SignalR .NET client library
 - set up streaming subscriptions
 - wait for the data snapshot before applying streaming updates
 - apply streaming updates to snapshots
 - handle partial messages
 - handle subscription reset messages
 - handle subscriptions heartbeats
 - monitor subscription activity and react correctly to inactivity

## Prerequisite
The application assumes the following configuration parameters, all located in the app.config file.

 Name | Description |
 ---- | --- |
 AccessToken | A valid access token. You may retrieve this from the developer portal.
 OpenApiBaseUrl | The base URI of the trading service.
 StreamingBaseUrl | The base URI of the streaming service.

---
Copyright © 2016 Saxo Bank A/S
