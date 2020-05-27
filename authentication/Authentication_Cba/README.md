# Certificate Based Authentication Sample
This sample demonstrates how to implement certificate based authentication.

>Certificate based authentication is only available to select institutional clients, upon request!

A description of this flow is provided here: [Certificate Based Authentication guide](https://developer.saxobank.com/openapi/learn/oauth-certificate-based-authentication).

The sample application is a simple .NET Core console application.

To get it running you need two pieces of information:
* Application configuration settings
* A userId and matching certificate for the user to be authenticated.

## Getting the application configuration settings
Applications which can perform certificate based authentication can only be created by Saxo Bank.

You will receive the following information

|Name|Description|Example|
|-----|-----------|-------|
|AppUrl|A string identifying your application. Most often a Url|https://localhost/mycbapplication|
|AppKey|The application key. Equivalent of the OAuth client_id parameter| 3ca59dd3a9b0408bad62bb0c7b42xxxx|
|AppSecret|The application secret. Equivalent of the OAuth client_secret|962d5c529166401db837e9c5c4cxxxxx|

## Getting a user certificate
You must also have login details for the user, who is supposed be authenticated. Further, this user must have been configured to be able to download a certificate, representing this user.

Saxo Bank will inform you, once the user has been configured for certificate management.

At that point you must follow the procedure outlined here to download a certificate [Managing Certificates in MyAccount] (https://developer.saxobank.com/openapi/learn/managing-certificates-in-myaccount).


## Installing the certificate
You must now install the certificate on the machine. Double-click the certificate file.
![Installing the certificate](./InstallCert1.png)

Select the certificate.

![Select the certificate](./InstallCert2.png)

Enter the password obtained when the certificate was downloaded.

![Enter certificate password](./InstallCert3.png)

Select a certificate store.

![Enter certificate password](./InstallCert4.png)

Complete the import

![Enter certificate password](./InstallCert5.png)

![Enter certificate password](./InstallCert6.png)


## Update Application Settings with the values provided
Application settings go into the App.json file.
Note the values for "TokenEndPoint" and "OpenApiBaseUrl" are set to point to the simulation environment.
![Enter App credentials](./VisualStudio1.png)

Settings for the certificate is placed in the Certificate.json file.

![Certificate settings](./VisualStudio2.png)

## Run the application
The application is now fully configured. However, since the application needs to fetch the certificate from the certificate store, it must be run with administrative privilges. (This not the preferred solution, but done in this example for simplicity.)

Make sure Visual Studio is run as administrator (see below). If not, exit Visual Studio and restart it with "Run as administrator".
![Certificate settings](./VisualStudio3.png)

### Now press F5.
You should now see the application automatically logging in, fetching an access token and returning client details.

![Certificate settings](./ClientDetails.png)


