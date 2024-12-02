using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace MyContosoPlugins.Helper
{
    public static class LowestWorkLoadHelper
    {


        // Fetch active users with read/write access
        public static EntityCollection FetchActiveUsers(IOrganizationService service, ITracingService tracingService)
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

        // Get the user with the lowest workload
        public static EntityReference GetLowestWorkloadUser(IOrganizationService service, ITracingService tracingService, EntityCollection users)
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


        // Get a random user from the list
        public static EntityReference GetRandomUser(EntityCollection users)
        {
            Random random = new Random();
            int randomIndex = random.Next(users.Entities.Count);
            var randomUser = users.Entities[randomIndex];
            return new EntityReference("systemuser", randomUser.Id);
        }


        // Assign the case to the user with the lowest workload
        public static void AssignCaseToUser(IOrganizationService service, ITracingService tracingService, Entity caseEntity, EntityReference lowestWorkloadUser)
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
