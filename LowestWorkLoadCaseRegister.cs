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
                    var users = FetchActiveUsers(service, tracingService);

                    // Calculate the user with the lowest workload
                    var lowestWorkloadUser = GetLowestWorkloadUser(service, tracingService, users);

                    // If no user with the lowest workload was found, pick a random user
                    if (lowestWorkloadUser == null)
                    {
                        tracingService.Trace("No user found with the lowest workload. Assigning to a random user.");
                        var randomUser = GetRandomUser(users);
                        lowestWorkloadUser = randomUser;
                    }

                    // Assign the case to the selected user (either the lowest workload or random)
                    AssignCaseToUser(service, tracingService, caseEntity, lowestWorkloadUser);
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

        private EntityCollection FetchActiveUsers(IOrganizationService service, ITracingService tracingService)
        {
            string fetchXml = @"
                <fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
                    <entity name='systemuser'>
                        <attribute name='systemuserid'/>
                        <filter type='and'>
                            <condition attribute='isdisabled' operator='eq' value='0'/>
                            <condition attribute='accessmode' operator='eq' value='0'/>
                        </filter>
                    </entity>
                </fetch>";

            EntityCollection users = service.RetrieveMultiple(new FetchExpression(fetchXml));
            tracingService.Trace("Total active users retrieved: {0}", users.Entities.Count);

            return users;
        }

        private EntityReference GetLowestWorkloadUser(IOrganizationService service, ITracingService tracingService, EntityCollection users)
        {
            EntityReference lowestWorkloadUser = null;
            int lowestWorkload = int.MaxValue;

            foreach (var user in users.Entities)
            {
                try
                {
                    // Fetch the number of cases assigned to the user
                    QueryExpression query = new QueryExpression("incident")
                    {
                        ColumnSet = new ColumnSet("incidentid"),
                        Criteria = new FilterExpression()
                        {
                            Conditions = {
                        new ConditionExpression("ownerid", ConditionOperator.Equal, user.Id)
                    }
                        }
                    };

                    EntityCollection userCases = service.RetrieveMultiple(query);
                    int userWorkload = userCases.Entities.Count;

                    tracingService.Trace("User ID: {0}, Workload: {1}", user.Id, userWorkload);

                    if (userWorkload < lowestWorkload)
                    {
                        lowestWorkload = userWorkload;
                        lowestWorkloadUser = new EntityReference("systemuser", user.Id);
                    }
                }
                catch (Exception ex)
                {
                    tracingService.Trace("Error while checking workload for user {0}: {1}", user.Id, ex.Message);
                    continue;  // Continue with the next user if there is an error
                }
            }

            if (lowestWorkloadUser == null)
            {
                throw new InvalidPluginExecutionException("No user found with the lowest workload.");
            }

            return lowestWorkloadUser;
        }

        private EntityReference GetRandomUser(EntityCollection users)
        {
            Random random = new Random();
            int randomIndex = random.Next(users.Entities.Count);
            var randomUser = users.Entities[randomIndex];
            return new EntityReference("systemuser", randomUser.Id);
        }

        private void AssignCaseToUser(IOrganizationService service, ITracingService tracingService, Entity caseEntity, EntityReference lowestWorkloadUser)
        {
            // Create a new instance to update only specific fields
            Entity updatedCase = new Entity(caseEntity.LogicalName)
            {
                Id = caseEntity.Id
            };

            // Set the fields to update
            updatedCase["ownerid"] = lowestWorkloadUser;

            // Perform the update
            service.Update(updatedCase);

            tracingService.Trace("Case successfully assigned to user with ID: {0}", lowestWorkloadUser.Id);
        }

    }
}
