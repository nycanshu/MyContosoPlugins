using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace MyContosoPlugins
{
    public class UpdateFinalAprOnStatusChange : IPlugin
    {
        private const int ReviewStatus = 100000001;
        private const int ApprovedStatus = 100000002;
        private const string PreImageAlias = "PreImage";

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin execution started.");

            // Check if the status changed from Review to Approved
            if (!IsStatusChangedToApproved(context, tracingService))
            {
                tracingService.Trace("Status has not changed from Review to Approved, skipping APR calculation.");
                return;
            }

            // Retrieve the mortgage entity
            var mortgage = (Entity)context.InputParameters["Target"];

            // Calculate the final APR
            var finalApr = CalculateFinalApr(mortgage, context, service, tracingService);

            // Update the mortgage entity with the calculated APR
            UpdateMortgageApr(mortgage, finalApr, service, tracingService);

            tracingService.Trace("Plugin execution completed.");
        }

        private bool IsStatusChangedToApproved(IPluginExecutionContext context, ITracingService tracingService)
        {
            // Retrieve the pre-image (the original values of the entity before the update)
            var preImage = GetPreImage(context, tracingService);

            // Check if status field exists
            if (!preImage.Contains("statuscode"))
            {
                throw new InvalidPluginExecutionException("Statuscode field is missing from the pre-image.");
            }

            var originalStatus = ((OptionSetValue)preImage["statuscode"]).Value;
            var currentStatus = ((OptionSetValue)((Entity)context.InputParameters["Target"])["statuscode"]).Value;

            return originalStatus == ReviewStatus && currentStatus == ApprovedStatus;
        }

        private Entity GetPreImage(IPluginExecutionContext context, ITracingService tracingService)
        {
            if (context.PreEntityImages.Contains(PreImageAlias))
            {
                return (Entity)context.PreEntityImages[PreImageAlias];
            }
            else
            {
                throw new InvalidPluginExecutionException("Pre-image is not available.");
            }
        }

        private decimal CalculateFinalApr(Entity mortgage, IPluginExecutionContext context, IOrganizationService service, ITracingService tracingService)
        {
            // Retrieve Base APR from preimage
            var preImage = GetPreImage(context, tracingService);
            var baseApr = preImage.GetAttributeValue<int>("new_baseapr");
            var margin = 0.02M; // Example margin value
            var salesTax = 0.05; // Default sales tax (Canada)

            tracingService.Trace($"Base APR from PreImage: {baseApr}, Default Sales Tax: {salesTax}");

            // Retrieve Region from preimage
            var region = ((OptionSetValue)preImage["new_region"]).Value;
            if (region == 100000000) // US
            {
                tracingService.Trace("Region is US, fetching state tax.");
                var state = preImage.GetAttributeValue<string>("new_state");

                if (!string.IsNullOrEmpty(state))
                {
                    salesTax = GetSalesTax(state, tracingService);
                }
                else
                {
                    tracingService.Trace("State is null or empty, using default sales tax.");
                }
            }

            // Retrieve Contact from preimage (for Risk Score)
            var riskScore = 0;
            if (preImage.Contains("new_contact") && preImage["new_contact"] is EntityReference contactRef)
            {
                // Retrieve the Contact entity to access riskscore
                try
                {
                    var contact = service.Retrieve("contact", contactRef.Id, new ColumnSet("new_riskscore"));
                    tracingService.Trace($"Contact reference found: {contactRef.Id}");
                    if (contact.Contains("new_riskscore") && contact["new_riskscore"] != null)
                    {
                        riskScore = contact.GetAttributeValue<int>("new_riskscore");
                        tracingService.Trace($"Retrieved Risk Score from PreImage: {riskScore}");
                    }
                }
                catch
                {
                    throw new InvalidPluginExecutionException("The associated contact does not have a valid Risk Score.");
                }
            }

            // Safeguard against invalid risk score
            if (riskScore <= 0)
            {
                throw new InvalidPluginExecutionException("Risk Score must be greater than 0 for valid APR calculation.");
            }

            // Calculate Final APR (ensure it's decimal)
            var finalApr = (decimal)((baseApr + (double)margin) + Math.Log(riskScore) + salesTax);
            tracingService.Trace($"Calculated Final APR: {finalApr}");

            return finalApr;
        }

        private void UpdateMortgageApr(Entity mortgage, decimal finalApr, IOrganizationService service, ITracingService tracingService)
        {
            // Update the mortgage with calculated APR
            mortgage["new_apr"] = finalApr;
            service.Update(mortgage);
            tracingService.Trace("Final APR updated successfully.");
        }

        private double GetSalesTax(string state, ITracingService tracingService)
        {
            var stateTaxList = new Dictionary<string, double>
            {
                { "Alabama", 4.0 }, { "Alaska", 0.0 }, { "Arizona", 5.6 },
                { "Arkansas", 6.5 }, { "California", 7.5 }, { "Colorado", 2.9 },
                { "Connecticut", 6.35 }, { "Delaware", 0.0 }, { "District Of Columbia", 5.75 },
                { "Florida", 6.0 }, { "Georgia", 4.0 }, { "Hawaii", 4.0 },
                { "Idaho", 6.0 }, { "Illinois", 6.25 }, { "Indiana", 7.0 },
                { "Iowa", 6.0 }, { "Kansas", 6.15 }, { "Kentucky", 6.0 },
                { "Louisiana", 4.0 }, { "Maine", 5.5 }, { "Maryland", 6.0 },
                { "Massachusetts", 6.25 }, { "Michigan", 6.0 }, { "Minnesota", 6.875 },
                { "Mississippi", 7.0 }, { "Missouri", 4.225 }, { "Montana", 0.0 },
                { "Nebraska", 5.5 }, { "Nevada", 6.85 }, { "New Hampshire", 0.0 },
                { "New Jersey", 7.0 }, { "New Mexico", 5.125 }, { "New York", 4.0 },
                { "North Carolina", 4.75 }, { "North Dakota", 5.0 }, { "Ohio", 5.75 },
                { "Oklahoma", 4.5 }, { "Oregon", 0.0 }, { "Pennsylvania", 6.0 },
                { "Rhode Island", 7.0 }, { "South Carolina", 6.0 }, { "South Dakota", 4.0 },
                { "Tennessee", 7.0 }, { "Texas", 6.25 }, { "Utah", 5.95 },
                { "Vermont", 6.0 }, { "Virginia", 5.3 }, { "Washington", 6.5 },
                { "West Virginia", 6.0 }, { "Wisconsin", 5.0 }, { "Wyoming", 4.0 }
            };

            if (!stateTaxList.ContainsKey(state))
            {
                tracingService.Trace($"State '{state}' not found in the tax list. Defaulting to 0.0.");
                return 0.0;
            }

            return stateTaxList[state];
        }
    }
}
