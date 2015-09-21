using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace ISVDemoUsage.Controllers
{
    [Route("api/[controller]")]
    public class ProxyController : Controller
    {
        private string _clientId;
        private string _password;
        private string _organizationId;
        private string _authority;
        private string _resourceId;

        private static readonly string ResourceString =
            @"https://management.azure.com/subscriptions/{0}/providers/Microsoft.Commerce/UsageAggregates?api-version={1}&reportedStartTime={2}&reportedEndTime={3}&aggregationGranularity={4}&showDetails={5}&continuationToken={6}";
        private readonly object ApiVersion;

        // 0 - subscription ID
        // 1 - API version
        // 2 - reported start time (ISO9601 with escape codes)
        // 3 - reported end time (ISO9601 with escape codes) 
        // 4 - aggregation granularity
        // 5 - showdetail-boolean-Value
        // 6 - continuation token (empty on first call)

        // Sample
        //       string requesturl = String.Format("https://management.azure.com/subscriptions/{0}/providers/Microsoft.Commerce/UsageAggregates?api-version=2015-06-01-preview&reportedstartTime=2015-05-15+00%3a00%3a00Z&reportedEndTime=2015-05-16+00%3a00%3a00Z", subscriptionId);



        public ProxyController()
        {
            _clientId = ConfigurationManager.AppSettings["ida:ClientID"];
            _password = ConfigurationManager.AppSettings["ida:Password"];

            // TODO: get organization ID
            _authority = String.Format(
                ConfigurationManager.AppSettings["ida:Authority"],
                _organizationId);

            _resourceId = ConfigurationManager.AppSettings["ida:AzureResourceManagerIdentifier"];
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage inbound)
        {
            string signedInUserUniqueName = ClaimsPrincipal.Current.FindFirst(ClaimTypes.Name).Value.Split('#')[ClaimsPrincipal.Current.FindFirst(ClaimTypes.Name).Value.Split('#').Length - 1];
            string UsageResponse = "";
            try
            {
                // Aquire Access Token to call Azure Resource Manager
                var credential = new ClientCredential(_clientId, _password);

                // initialize AuthenticationContext with the token cache of the currently signed in user, 
                // as kept in the app's EF DB
                var authContext = new AuthenticationContext(
                    authority: _authority,
                    tokenCache: new ADALTokenCache(signedInUserUniqueName)
                );
                
                var userId = new UserIdentifier(signedInUserUniqueName, UserIdentifierType.RequiredDisplayableId);
                AuthenticationResult result = authContext.AcquireTokenSilent(
                   _resourceId, credential, userId);

                // Making a call to the Azure Usage API for a set time frame with the input AzureSubID

                /*
                   // 0 - subscription ID
                // 1 - API version
                // 2 - reported start time (ISO9601 with escape codes)
                // 3 - reported end time (ISO9601 with escape codes) 
                // 4 - aggregation granularity
                // 5 - showdetail-boolean-Value
                // 6 - continuation token (empty on first call)*/
                var startTime = DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                var endTime = DateTime.Now.AddMonths(-1).ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                var aggregationGranularity = "Hourly";

                string requesturl = String.Format(ResourceString, 
                    ApiVersion,
                    startTime,
                    endTime,
                    aggregationGranularity,
                    true,
                    ""
                );

                //Crafting the HTTP call
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requesturl);
                request.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + result.AccessToken);
                request.ContentType = "application/json";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Console.WriteLine(response.StatusDescription);
                Stream receiveStream = response.GetResponseStream();

                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
                UsageResponse = readStream.ReadToEnd();

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception ex0)
            {
                throw;
            }
        }
    }
}
 