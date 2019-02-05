using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using Siemplify.Common.ExternalChannels;
using Siemplify.Common.ExternalChannels.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MsTeamsManager
{
    class Program
    {

        public static void Main()
        {
            try
            {
                var msTeamsManager = new MsTeamsChannelProvider();
                msTeamsManager.Connect();

                Console.WriteLine($"Token: {msTeamsManager.Token}\r\nCurrent Team Id: {msTeamsManager.CurrentTeamId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }            
        }

        private static async Task RunAsync()
        {
            AuthenticationConfig config = AuthenticationConfig.ReadFromJsonFile("appsettings.json");


            // TODO: Remove this code
#if DEBUG
            if (Environment.MachineName == "PC") config = AuthenticationConfig.ReadFromJsonFile("appsettings-dk.json");
#endif

            // Even if this is a console application here, a daemon application is a confidential client application
            ClientCredential clientCredentials;

#if !VariationWithCertificateCredentials
            clientCredentials = new ClientCredential(config.ClientSecret);
#else
            X509Certificate2 certificate = ReadCertificate(config.CertificateName);
            clientCredentials = new ClientCredential(new ClientAssertionCertificate(certificate));
#endif
            var app = new ConfidentialClientApplication(config.ClientId, config.Authority, "https://daemon", clientCredentials, null, new TokenCache());

            // With client credentials flows the scopes is ALWAYS of the shape "resource/.default", as the 
            // application permissions need to be set statically (in the portal or by PowerShell), and then granted by
            // a tenant administrator
            string[] scopes = new string[] { "https://graph.microsoft.com/.default" };

            AuthenticationResult result = null;
            try
            {
                result = await app.AcquireTokenForClientAsync(scopes);
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be of the form "https://resourceurl/.default"
                // Mitigation: change the scope to be as expected
            }

            if (result != null)
            {
                var httpClient = new HttpClient();
                var apiCaller = new ProtectedApiCallHelper(httpClient);
                await apiCaller.CallWebApiAndProcessResultASync("https://graph.microsoft.com/v1.0/users", result.AccessToken, Display);
            }
        }



        /// <summary>
        /// Display the result of the Web API call
        /// </summary>
        /// <param name="result">Object to display</param>
        private static void Display(JObject result)
        {
            foreach (JProperty child in result.Properties().Where(p => !p.Name.StartsWith("@")))
            {
                Console.WriteLine($"{child.Name} = {child.Value}");
            }
        }

    }
}
