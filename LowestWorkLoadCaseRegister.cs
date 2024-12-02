using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyContosoPlugins
{
    public class LowestWorkLoadCaseRegister : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("LowestWorkLoadCaseRegister Plugin execution started.");

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity caseEntity)
            {
                if (caseEntity.LogicalName != "incident")
                {
                    tracingService.Trace("Entity is not a case. Exiting plugin.");
                    return;
                }

                try
                {
                    // Fetch active users with read/write access
                    var users = Helper.LowestWorkLoadHelper.FetchActiveUsers(service, tracingService);

                    // Calculate the user with the lowest workload
                    var lowestWorkloadUser = Helper.LowestWorkLoadHelper.GetLowestWorkloadUser(service, tracingService, users);

                    // If no user with the lowest workload was found, pick a random user
                    if (lowestWorkloadUser == null)
                    {
                        tracingService.Trace("No user found with the lowest workload. Assigning to a random user.");
                        var randomUser = Helper.LowestWorkLoadHelper.GetRandomUser(users);
                        lowestWorkloadUser = randomUser;
                    }

                    // Assign the case to the selected user (either the lowest workload or random)
                    Helper.LowestWorkLoadHelper.AssignCaseToUser(service, tracingService, caseEntity, lowestWorkloadUser);
                }
                catch (Exception ex)
                {
                    // Log the user who triggered the error (this is the user who initiated the plugin execution)
                    tracingService.Trace("Error in LowestWorkLoadCaseRegister Plugin. Error occurred for user with ID: {0}", context.UserId);

                    // You could retrieve the user's name for further context
                    var user = service.Retrieve("systemuser", context.UserId, new ColumnSet("fullname"));
                    string userName = user.Contains("fullname") ? user["fullname"].ToString() : "Unknown User";
                    tracingService.Trace("Error occurred for user: {0}", userName);

                    // Log the exception details
                    tracingService.Trace("Exception details: {0}", ex.ToString());

                    throw new InvalidPluginExecutionException("An error occurred during case assignment.", ex);
                }
            }
            else
            {
                tracingService.Trace("Target is not an entity. Exiting plugin.");
            }
        }  
    }
}
