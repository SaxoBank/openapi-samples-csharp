# Web Application - Authentication
> Please note that this is work in progress!

**This web based application explains:**
* How to Authenticate & get AuthCode for Web Applications?
* How to Get & Renew Access Token?
* How to call Open API?

## Prerequisite
Before you get started, your application must be registered with **Saxo Bank A/S**, which will provide the following information:

 Name | Description |
 ---- | --- |
 AppKey | Application Key identifying your application to Saxo Bank.
 AppSecret | Application Secret identifying your application to Saxo Bank.
 AuthenticationUrl | The URL of the Saxo Bank _authentication and authorization_ server.
 OpenApiBaseUrl | Base URL for calling OpenAPI REST endpoints.

## Setting up Demo Project
The sample app is a Asp.Net website in a Visual Studio 2013 solution. It requires a bit of setup before trying it out on your machine:

* The web site should be mapped to a location that corresponds to your service provider URL - this is done in the web settings of the project (see image below)
![Image Not Found ](WebSettings.png)

* In the appSettings values in the web.config, you should insert your app key and -secret into the corresponding settings. Also verify the other URLs in the config against that URLs you have received from Saxo Bank.

Now you are all set to run your sample App.

---
Copyright © 2016 Saxo Bank A/S
