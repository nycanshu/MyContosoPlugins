using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MyContosoPlugins
{
    public class updateBaseAprOnCreateMortgage : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.MessageName.ToLower() == "create" && context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity mortgage = (Entity)context.InputParameters["Target"];

                // Ensure the mortgage entity has a valid ID
                if (mortgage.Id != Guid.Empty)
                {
                    // Fetch Base APR from the API
                    int baseApr = GetBaseApr();

                    // Update the mortgage with the retrieved base APR value
                    if (baseApr > 0)
                    {
                        mortgage["new_baseapr"] = baseApr;
                        IOrganizationService service = (IOrganizationService)serviceProvider.GetService(typeof(IOrganizationService));
                        service.Update(mortgage); // Update the mortgage record in CRM
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException("Failed to retrieve valid Base APR.");
                    }
                }
                else
                {
                    throw new InvalidPluginExecutionException("Mortgage entity does not have a valid ID.");
                }
            }
            else
            {
                throw new InvalidPluginExecutionException("This plugin should only be registered on the Create message for the Mortgage entity.");
            }
        }

        private int GetBaseApr()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Set the base URL for your API
                    string apiUrl = "http://localhost:8080/api/getbaseapr";

                    // Make the GET request to your API
                    HttpResponseMessage response = client.GetAsync(apiUrl).Result;

                    // Ensure a successful response
                    if (response.IsSuccessStatusCode)
                    {
                        string content = response.Content.ReadAsStringAsync().Result;

                        // Attempt to parse the integer value from the API response
                        if (int.TryParse(content, out int baseApr))
                        {
                            return baseApr;
                        }
                        else
                        {
                            throw new InvalidPluginExecutionException("API response does not contain a valid integer value for Base APR.");
                        }
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException($"API call failed with status code {response.StatusCode}.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle errors that occur during the HTTP request or parsing
                throw new InvalidPluginExecutionException($"Error during Base APR retrieval: {ex.Message}");
            }
        }
    }
}
