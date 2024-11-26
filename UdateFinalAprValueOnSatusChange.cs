using Microsoft.Xrm.Sdk;
using System;
using System.Activities.Debugger;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyContosoPlugins
{
    public class UdateFinalAprValueOnSatusChange : IPlugin
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

            //get base apr from entity
            var baseApr = mortgage.GetAttributeValue<int>("new_baseapr");

            //taking dummy margin value as  2%;
            var margin = 0.02;

            var riskScore = 0;

            //set sales tax to 5% and update if the mortgage is in approved status and in US region
            var salesTax = 0.05;

            var region = mortgage.GetAttributeValue<EntityReference>("new_region");

            if (region != null && region.Name == "US")

            {
                //get state value
                var state = mortgage.GetAttributeValue<EntityReference>("new_state");
                salesTax = getSalesTax(state);
            }

            //check if mortgage contains new_contact as contact Lookup
            if (mortgage.Contains("new_contact") && mortgage["new_contact"] is EntityReference contactRef)
            {
                // Retrieve the Contact entity to access SSN
                Entity contact = service.Retrieve("contact", contactRef.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet("new_riskscore"));

                if (contact.Contains("new_riskscore") && contact["new_riskscore"] != null)
                {
                    riskScore = contact.GetAttributeValue<int>("new_riskscore");
                }
            }

            //calculate final apr
            var finalApr = (baseApr + margin) + Math.Log(riskScore) + (salesTax);


            //update the field apr in mortgage entity in last step
            mortgage["new_apr"] = finalApr;

            tracingService.Trace("Plugin execution completed.");
        }

        private double getSalesTax(EntityReference state)
        {
            var stateTaxList = new Dictionary<string, double>
{
    { "Alabama", 4.0 },
    { "Alaska", 0.0 },
    { "Arizona", 5.6 },
    { "Arkansas", 6.5 },
    { "California", 7.5 },
    { "Colorado", 2.9 },
    { "Connecticut", 6.35 },
    { "Delaware", 0.0 },
    { "District Of Columbia", 5.75 },
    { "Florida", 6.0 },
    { "Georgia", 4.0 },
    { "Hawaii", 4.0 },
    { "Idaho", 6.0 },
    { "Illinois", 6.25 },
    { "Indiana", 7.0 },
    { "Iowa", 6.0 },
    { "Kansas", 6.15 },
    { "Kentucky", 6.0 },
    { "Louisiana", 4.0 },
    { "Maine", 5.5 },
    { "Maryland", 6.0 },
    { "Massachusetts", 6.25 },
    { "Michigan", 6.0 },
    { "Minnesota", 6.875 },
    { "Mississippi", 7.0 },
    { "Missouri", 4.225 },
    { "Montana", 0.0 },
    { "Nebraska", 5.5 },
    { "Nevada", 6.85 },
    { "New Hampshire", 0.0 },
    { "New Jersey", 7.0 },
    { "New Mexico", 5.125 },
    { "New York", 4.0 },
    { "North Carolina", 4.75 },
    { "North Dakota", 5.0 },
    { "Ohio", 5.75 },
    { "Oklahoma", 4.5 },
    { "Oregon", 0.0 },
    { "Pennsylvania", 6.0 },
    { "Rhode Island", 7.0 },
    { "South Carolina", 6.0 },
    { "South Dakota", 4.0 },
    { "Tennessee", 7.0 },
    { "Texas", 6.25 },
    { "Utah", 5.95 },
    { "Vermont", 6.0 },
    { "Virginia", 5.3 },
    { "Washington", 6.5 },
    { "West Virginia", 6.0 },
    { "Wisconsin", 5.0 },
    { "Wyoming", 4.0 }
};

            return stateTaxList[state.Name];

        }
    }
}
