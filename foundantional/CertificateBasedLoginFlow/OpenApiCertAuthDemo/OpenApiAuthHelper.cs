// Copyright © 2016 Saxo Bank A/S

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and limitations under the License.

using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace OpenApiCertAuthDemo
{
    public static class OpenApiAuthHelper
    {
        // Setup all the namespaces
        private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
        private static readonly XNamespace SamlNs = "urn:oasis:names:tc:SAML:2.0:assertion";
        private static readonly XNamespace SamlpNs = "urn:oasis:names:tc:SAML:2.0:protocol";
        private static readonly XNamespace XmlDsigNs = "http://www.w3.org/2000/09/xmldsig#";
        private static readonly XNamespace XmlEncNs = "http://www.w3.org/2001/04/xmlenc#";

        public static async Task<OpenApiOAuth2TokenResponse> GetTokenByClientCertificate(
            X509Certificate2 clientCert, 
            X509Certificate2 encryptionCert,
            string appUrl,
            string partnerIdpUrl,
            string userId,
            string appKey,
            string appSecret,
            string authenticationUrl)
        {
            string samlRequest = CreateAuthnRequest(appUrl, partnerIdpUrl, userId, appKey, appSecret, clientCert, encryptionCert);

            string responseString = await SendSamlRequest(samlRequest, authenticationUrl);
            XmlElement soapResponseXml = GetXmlElement(responseString);
            if (soapResponseXml == null) return null;

            XmlNamespaceManager xmlns = new XmlNamespaceManager(soapResponseXml.OwnerDocument.NameTable);
            xmlns.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
            XmlNode oaTokenNode =
                soapResponseXml.SelectSingleNode("//saml:Attribute[@Name='OpenApiToken']/saml:AttributeValue", xmlns);
            if (oaTokenNode == null) return null;

            return OpenApiOAuth2TokenResponse.ParseToken(oaTokenNode.InnerText);
        }

        private static async Task<string> SendSamlRequest(string request, string destinationUrl)
        {
            string samlRequest = String.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                                               "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
                                               "soap:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                                               "<soap:Body>{0}</soap:Body></soap:Envelope>", request);

            // Initialize httpClient with cookie container to ensure stickiness and automatic decompression of recieved data. Note that in production code
            // this must be disposed correctly
            HttpClient httpClient = new HttpClient(
                new HttpClientHandler
                {
                    CookieContainer = new CookieContainer(),
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    UseDefaultCredentials = true
                });

            // We need to set the content-type without getting a chartset value as well
            HttpContent content =  new StringContent(samlRequest);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/soap+xml");

            HttpResponseMessage response = await httpClient.PostAsync(new Uri(destinationUrl), content);
            return await response.Content.ReadAsStringAsync();
        }

        private static string CreateAuthnRequest(string destination, string partnerIdpUrl, string userId, string appKey,
            string appSecret, X509Certificate2 clientCert, X509Certificate2 encryptionCert)
        {
            string authnRequestId = "_" + Guid.NewGuid();
            string responseId = "_" + Guid.NewGuid();
            string issueTime = string.Format("{0:s}Z", DateTime.UtcNow);

            var encryptedAssertionXElement = CreateEncryptedAssertion(destination, partnerIdpUrl, userId, appKey, appSecret, encryptionCert);

            XElement samlResponse = 
                new XElement(SamlpNs + "Response",
                    new XAttribute(XNamespace.Xmlns + "saml", SamlNs),
                    new XAttribute(XNamespace.Xmlns + "samlp", SamlpNs),
                    new XAttribute("ID", responseId),
                    new XAttribute("Version", "2.0"),
                    new XAttribute("IssueInstant", issueTime),
                    new XAttribute("Destination", destination),
                    new XElement(SamlpNs + "Issuer",
                        partnerIdpUrl
                        ),                    
                    new XElement(SamlpNs + "Status",
                        new XElement(SamlpNs + "StatusCode",
                            new XAttribute("Value", "urn:oasis:names:tc:SAML:2.0:status:Success")
                            )
                        ),
                    new XElement(SamlNs + "EncryptedAssertion",
                        encryptedAssertionXElement 
                    )
                );

            var signatureXml = CreateSignature(clientCert, samlResponse);
            samlResponse.AddFirst(XElement.Parse(signatureXml));

            XElement authnRequest = 
                new XElement(SamlpNs + "AuthnRequest",
                    new XAttribute(XNamespace.Xmlns + "samlp", SamlpNs),
                    new XAttribute(XNamespace.Xmlns + "saml", SamlNs),
                    new XAttribute("ID", authnRequestId),
                    new XAttribute("IssueInstant", issueTime),
                    new XAttribute("Destination", destination),
                    new XAttribute("AssertionConsumerServiceURL", destination),
                    new XAttribute("Version", "2.0"),
                    new XAttribute("ForceAuthn", false),
                    new XAttribute("IsPassive", false),
                    new XAttribute("ProtocolBinding", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"),
                    new XElement(SamlNs + "Issuer",
                        partnerIdpUrl
                    ),
                    new XElement(SamlpNs + "Extensions",
                        new XElement("Token",
                            samlResponse
                        )
                    ),
                    new XElement(SamlpNs + "NameIDPolicy",
                        new XAttribute("AllowCreate", false)
                    )
               );
            
         
            return authnRequest.ToString(SaveOptions.DisableFormatting);
        }

        private static XElement CreateEncryptedAssertion(string destination, string partnerIdpUrl, string userId, string appKey,
            string appSecret, X509Certificate2 encryptionCert)
        {
            // Create the SAML assertion containing the secrets
            string assertionId = "_" + Guid.NewGuid();
            string assertionIssuingTime = string.Format("{0:s}Z", DateTime.UtcNow);
            string assertionExpiryTime = string.Format("{0:s}Z", DateTime.UtcNow.AddMinutes(2));

            XElement assertion =
                new XElement(SamlNs + "Assertion",
                    new XAttribute(XNamespace.Xmlns + "saml", SamlNs),
                    new XAttribute("Version", "2.0"),
                    new XAttribute("ID", assertionId),
                    new XAttribute("IssueInstant", assertionIssuingTime),
                    new XElement(SamlNs + "Issuer",
                        partnerIdpUrl),
                    new XElement(SamlNs + "Subject",
                        new XElement(SamlNs + "NameID",
                            userId
                            ),                        
                        new XElement(SamlNs + "SubjectConfirmation",
                            new XAttribute("Method", "urn:oasis:names:tc:SAML:2.0:cm:bearer"),
                            new XElement(SamlNs + "SubjectConfirmationData",
                                new XAttribute("Recipient", destination)
                                )
                            )
                        ),
                    new XElement(SamlNs + "Conditions",
                        new XAttribute("NotOnOrAfter", assertionExpiryTime),
                        new XElement(SamlNs + "AudienceRestriction",
                            new XElement(SamlNs + "Audience",
                                destination
                                )
                            )
                        ),
                    new XElement(SamlNs + "AuthnStatement",
                        new XAttribute("AuthnInstant", assertionIssuingTime),
                        new XElement(SamlNs + "AuthnContext",
                            new XElement(SamlNs + "AuthnContextClassRef",
                                "urn:oasis:names:tc:SAML:2.0:ac:classes:Password"
                                )
                            )
                        ),
                    new XElement(SamlNs + "AttributeStatement",
                        new XElement(SamlNs + "Attribute",
                            new XAttribute("Name", "IsCertificateBasedAuthentication"),
                            new XAttribute("NameFormat", "urn:oasis:names:tc:SAML:2.0:attrname-format:basic"),
                            new XElement(SamlNs + "AttributeValue",
                                true
                                )
                            )
                        ),
                    new XElement(SamlNs + "AttributeStatement",
                        new XElement(SamlNs + "Attribute",
                            new XAttribute("Name", "AppKey"),
                            new XAttribute("NameFormat", "urn:oasis:names:tc:SAML:2.0:attrname-format:basic"),
                            new XElement(SamlNs + "AttributeValue",
                                appKey
                                )
                            )
                        ),
                    new XElement(SamlNs + "AttributeStatement",
                        new XElement(SamlNs + "Attribute",
                            new XAttribute("Name", "AppSecret"),
                            new XAttribute("NameFormat", "urn:oasis:names:tc:SAML:2.0:attrname-format:basic"),
                            new XElement(SamlNs + "AttributeValue",
                                appSecret
                                )
                            )
                        )
                    );

            // Encrypt the assertion
            XmlDocument doc = new XmlDocument();
            XmlElement assertionXmlEl = doc.ReadNode(assertion.CreateReader()) as XmlElement;
            EncryptedXml eXml = new EncryptedXml();

            if (assertionXmlEl == null)
                throw new NullReferenceException("assertionXmlEl was null");

            // Encrypt the element.
            EncryptedData encryptedElement = eXml.Encrypt(assertionXmlEl, encryptionCert);

            XElement encryptedAssertionXElement = XElement.Parse(encryptedElement.GetXml().OuterXml);

            // .Net adds the encryption certificate in a KeyInfo->EncryptedKey_>KeyInfo, we don't want that, so we just remove it
            encryptedAssertionXElement
                .Element(XmlDsigNs + "KeyInfo")
                .Element(XmlEncNs + "EncryptedKey")
                .Element(XmlDsigNs + "KeyInfo")
                .Remove();
                
            return encryptedAssertionXElement;
        }

        private static string CreateSignature(X509Certificate2 clientCert, XElement samlResponse)
        {
            string responseId = samlResponse.Attribute("ID").Value;

            XmlDocument doc = new XmlDocument();
            XmlElement samlResponseXmlEl = doc.ReadNode(samlResponse.CreateReader()) as XmlElement;
            doc.AppendChild(samlResponseXmlEl);

            if (samlResponseXmlEl == null)
                throw new NullReferenceException("samlResponseXmlEl was null");

            var signedXml = new SignedXml(doc);

            var signatureReference = new Reference("#" + responseId);
            signatureReference.AddTransform(new XmlDsigExcC14NTransform("#default samlp saml ds xs xsi"));
            signatureReference.AddTransform(new XmlDsigEnvelopedSignatureTransform());

            signedXml.AddReference(signatureReference);

            signedXml.SigningKey = clientCert.PrivateKey;

            var certificateKeyInfo = new KeyInfo();
            certificateKeyInfo.AddClause(new KeyInfoX509Data(clientCert));
            signedXml.KeyInfo = certificateKeyInfo;
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
            signedXml.ComputeSignature();

            string signatureXml = signedXml.GetXml().OuterXml;
            return signatureXml;
        }

        private static XmlElement GetXmlElement(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc.DocumentElement;
        }
    }
}