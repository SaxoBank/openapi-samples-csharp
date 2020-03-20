
# Windows Console Application - Certificate Based Authentication
> Please note that this is work in progress!

**This web based application explains:**
* How to Get & Renew Access Token using Certificate based Authentication?
* How to call Open API?

## Prerequisite
Before you get started, your application must be registered with Saxo Bank A/S, which will provide the following information:
* Urls and AppKey and Secret for your application.
* How to obtain a Client Certificate, which is used to identify the user, which is logging in using your application.
* How to obtain a Saxo Encryption certificate which is used to sign the request to the Saxo authentication system.

## Installing Certificates
Make sure the certificates are installed at a location, where they can be accessed by the application. For this example to run, under the identity of a logged in windows user, the certificates have been installed under "Local_Machine", Personal.

* The .cer Saxo encryption certificate can be installed directly by clicking on it.
* The .pfx client certificate, requires a password, which was provied when the certificate was downloaded.

![Image Not Found ](InstallingCertifcates.png)

## Configuring and Running the Sample Application
The sample application is a console app in a Visual Studio 2013 solution.
After opening the solution, go to the In the app.config and insert the following configuration values: 

 Name | Description |
 ---- | --- |
 AppKey | Application Key identifying your application to Saxo Bank.
 AppSecret | Application Secret identifying your application to Saxo Bank.
 AppUrl | Application URL identifying your application to Saxo Bank.
 AuthenticationUrl | The URL of the Saxo Bank _authentication and authorization_ server.
 OpenApiBaseUrl | Base URL for calling OpenAPI REST endpoints.
 ClientCertificateSerialNumber | The serial number of the client certificate.
 SaxoBankCertificateSerialNumber | The serial number of the Saxo Bank signing certificate.
 UserId | The Saxo user id, who is identified by the client certificate.
 PartnerIdpUrl | The URL defining your partner configuration with Saxo Bank.


After doing this, you should be able to compile and run the application.

![Image Not Found ](ApplicationRun.png)

---
Copyright © 2016 Saxo Bank A/S
