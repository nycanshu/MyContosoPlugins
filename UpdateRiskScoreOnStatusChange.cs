using System;
using System.Net.Http;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MyContosoPlugins
{
    public class UpdateRiskScoreOnStatusChange : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Retrieve the plugin execution context
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin execution started.");

            // Ensure the target entity is present
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
            {
                tracingService.Trace("Target entity found.");

                // Retrieve the Pre-Image to check the previous statuscode
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    Entity preImage = context.PreEntityImages["PreImage"];

                    // Check if the statuscode is changing
                    if (targetEntity.Contains("statuscode"))
                    {
                        OptionSetValue newStatusCode = (OptionSetValue)targetEntity["statuscode"];
                        OptionSetValue oldStatusCode = preImage.Contains("statuscode") ? (OptionSetValue)preImage["statuscode"] : null;

                        if (newStatusCode.Value == 100000001 && (oldStatusCode == null || oldStatusCode.Value != 100000001)) // Review status
                        {
                            tracingService.Trace("Mortgage status changed to Review.");

                            // Ensure the mortgage has an associated contact
                            if (preImage.Contains("new_contact") && preImage["new_contact"] is EntityReference contactRef)
                            {
                                tracingService.Trace($"Contact reference found: {contactRef.Id}");

                                // Retrieve the Contact entity to access SSN
                                Entity contact = service.Retrieve("contact", contactRef.Id, new ColumnSet("new_socialsecuritynumber"));

                                if (contact.Contains("new_socialsecuritynumber") && contact["new_socialsecuritynumber"] != null)
                                {
                                    string ssn = contact["new_socialsecuritynumber"].ToString();
                                    tracingService.Trace($"Contact SSN retrieved: {ssn}");

                                    // Call the external API to get the risk score
                                    int riskScore = GetRiskScoreFromApi(ssn, tracingService);

                                    // Update the Contact record with the new risk score
                                    contact["new_riskscore"] = riskScore;
                                    service.Update(contact); // Persist the changes
                                    tracingService.Trace($"Risk score updated successfully for Contact: {riskScore}");
                                }
                                else
                                {
                                    throw new InvalidPluginExecutionException("The associated contact does not have a valid Social Security Number.");
                                }
                            }
                            else
                            {
                                throw new InvalidPluginExecutionException("The mortgage record does not have an associated contact.");
                            }
                        }
                        else
                        {
                            tracingService.Trace("Mortgage status is not set to Review or status did not change. No action taken.");
                        }
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException("The statuscode field is missing from the target entity.");
                    }
                }
                else
                {
                    throw new InvalidPluginExecutionException("Pre-Image is not available.");
                }
            }
            else
            {
                throw new InvalidPluginExecutionException("Target entity not found in input parameters.");
            }
        }

        private int GetRiskScoreFromApi(string ssn, ITracingService tracingService)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // API URL
                    string apiUrl = "https://contosoapi-38bv.onrender.com/api/getriskscore";

                    // Prepare the request payload
                    string payload = $"{{ \"ssn\": \"{ssn}\" }}";
                    StringContent content = new StringContent(payload, Encoding.UTF8, "application/json");

                    // Make the POST request
                    tracingService.Trace("Making API request to fetch risk score.");
                    HttpResponseMessage response = client.PostAsync(apiUrl, content).Result;

                    // Ensure a successful response
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = response.Content.ReadAsStringAsync().Result;
                        tracingService.Trace($"API response: {responseBody}");

                        // Parse the JSON response
                        var json = System.Text.Json.JsonDocument.Parse(responseBody);
                        if (json.RootElement.TryGetProperty("riskScore", out var riskScoreElement))
                        {
                            int riskScore = riskScoreElement.GetInt32();
                            tracingService.Trace($"Risk score parsed: {riskScore}");
                            return riskScore;
                        }
                        else
                        {
                            throw new InvalidPluginExecutionException("API response does not contain a valid riskScore.");
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
                tracingService.Trace($"Error during API call: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error retrieving risk score: {ex.Message}");
            }
        }
    }
}
