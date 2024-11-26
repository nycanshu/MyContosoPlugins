using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MyContosoPlugins
{
    public class UpdateBaseAprOnCreate : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Retrieve the plugin execution context
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin execution started.");

            // Validate the target entity and operation
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
                return;

            Entity mortgage = (Entity)context.InputParameters["Target"];

            //get base apr from the API
            var baseApr = FetchBaseAPRFromAPI();

            //set base apr value to mortgage entity

            mortgage["new_baseapr"] = baseApr;
            service.Update(mortgage);

            tracingService.Trace("Plugin execution completed.");
        }

        private int FetchBaseAPRFromAPI()
        {
            using (var client = new HttpClient())
            {
                var response = client.GetAsync("https://contosoapi-38bv.onrender.com/api/getbaseapr").Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Failed to fetch Base APR from the API.");

                var content = response.Content.ReadAsStringAsync().Result;
                // Parse JSON and extract baseApr as an integer
                var json = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(content);
                if (json == null || json.baseApr == null)
                    throw new Exception("Invalid response format from the API.");

                return (int)json.baseApr;
            }
        }

    }
}
