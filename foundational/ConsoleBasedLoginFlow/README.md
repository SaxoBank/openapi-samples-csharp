# Windows Console Application - Authentication
> Please note that this is work in progress!

**This web based application explains:**
* How to Authenticate & get AuthCode for Windows Console Applications?
* How to Get & Renew Access Token?
* How to call Open API?

## Prerequisite
Before you get started, your application must be registered with **Saxo Bank A/S**, which will provide the following information:

 Name | Description |
 ---- | --- |
 AppKey | Application Key identifying your application to Saxo Bank.
 AppSecret | Application Secret identifying your application to Saxo Bank.
 AppUrl | Application URL identifying your application to Saxo Bank.
 AuthenticationUrl | The URL of the Saxo Bank _authentication and authorization_ server.
 OpenApiBaseUrl | Base URL for calling OpenAPI REST endpoints.

## Setting up Demo Project
After opening the solution, go to the In the app.config and insert your app key and -secret and your service provider URL into the corresponding settings. Also verify the other URLs in the config against that URLs you have recieved from Saxo Bank.

After doing this, you should be able to run this console application.

---
Copyright © 2016 Saxo Bank A/S
