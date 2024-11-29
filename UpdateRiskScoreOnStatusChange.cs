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
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin execution started.");

            try
            {
                // Validate the target entity and pre-image
                Entity targetEntity = ValidateTargetEntity(context);
                Entity preImage = ValidatePreImage(context);

                // Check if status changed from New to Review
                if (IsStatusChangedToReview(targetEntity, preImage, tracingService))
                {
                    tracingService.Trace("Mortgage status changed from New to Review.");

                    // Process contact risk score
                    ProcessContactRiskScore(preImage, service, tracingService);
                }
                else
                {
                    tracingService.Trace("Status change condition not met. Plugin execution skipped.");
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error: {ex.Message}");
                throw new InvalidPluginExecutionException($"Plugin execution failed: {ex.Message}");
            }

            tracingService.Trace("Plugin execution completed.");
        }

        private Entity ValidateTargetEntity(IPluginExecutionContext context)
        {
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity)
            {
                return targetEntity;
            }

            throw new InvalidPluginExecutionException("Target entity not found in input parameters.");
        }

        private Entity ValidatePreImage(IPluginExecutionContext context)
        {
            if (context.PreEntityImages.Contains("PreImage"))
            {
                return context.PreEntityImages["PreImage"];
            }

            throw new InvalidPluginExecutionException("Pre-Image is not available.");
        }

        private bool IsStatusChangedToReview(Entity targetEntity, Entity preImage, ITracingService tracingService)
        {
            if (targetEntity.Contains("statuscode") && preImage.Contains("statuscode"))
            {
                OptionSetValue newStatusCode = (OptionSetValue)targetEntity["statuscode"];
                OptionSetValue oldStatusCode = (OptionSetValue)preImage["statuscode"];

                return newStatusCode.Value == 100000001 && oldStatusCode.Value == 1;
            }

            throw new InvalidPluginExecutionException("The statuscode field is missing from the target entity or pre-image.");
        }

        private void ProcessContactRiskScore(Entity preImage, IOrganizationService service, ITracingService tracingService)
        {
            if (preImage.Contains("new_contact") && preImage["new_contact"] is EntityReference contactRef)
            {
                tracingService.Trace($"Contact reference found: {contactRef.Id}");

                // Retrieve contact to get SSN
                Entity contact = service.Retrieve("contact", contactRef.Id, new ColumnSet("new_socialsecuritynumber"));

                if (contact.Contains("new_socialsecuritynumber") && contact["new_socialsecuritynumber"] != null)
                {
                    string ssn = contact["new_socialsecuritynumber"].ToString();
                    tracingService.Trace($"Contact SSN retrieved: {ssn}");

                    // Call the API or test method to get risk score
                    int riskScore = TestRiskScore(ssn, tracingService);
                    tracingService.Trace($"Risk score retrieved: {riskScore}");

                    // Update the contact with the new risk score
                    contact["new_riskscore"] = riskScore;
                    service.Update(contact);
                    tracingService.Trace("Contact risk score updated successfully.");
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

        private int GetRiskScoreFromApi(string ssn, ITracingService tracingService)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = "https://contosoapi-38bv.onrender.com/api/getriskscore";
                    string payload = $"{{ \"ssn\": \"{ssn}\" }}";
                    StringContent content = new StringContent(payload, Encoding.UTF8, "application/json");

                    tracingService.Trace("Making API request to fetch risk score.");
                    HttpResponseMessage response = client.PostAsync(apiUrl, content).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = response.Content.ReadAsStringAsync().Result;
                        tracingService.Trace($"API response: {responseBody}");

                        var json = System.Text.Json.JsonDocument.Parse(responseBody);
                        if (json.RootElement.TryGetProperty("riskScore", out var riskScoreElement))
                        {
                            return riskScoreElement.GetInt32();
                        }

                        throw new InvalidPluginExecutionException("API response does not contain a valid riskScore.");
                    }

                    throw new InvalidPluginExecutionException($"API call failed with status code {response.StatusCode}.");
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error during API call: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error retrieving risk score: {ex.Message}");
            }
        }

        private int TestRiskScore(string ssn, ITracingService tracingService)
        {
            try
            {
                // Generate a random number between 1 and 100 using SSN as seed
                int seed = ssn.GetHashCode();
                Random random = new Random(seed);
                int riskScore = random.Next(1, 101);

                tracingService.Trace($"Generated test risk score: {riskScore} for SSN: {ssn}");
                return riskScore;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error generating test risk score: {ex.Message}");
                throw;
            }
        }
    }
}
