using System;
using System.Net.Http;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MyContosoPlugins.Helper;


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
                Entity targetEntity = UpdateRiskScoreHelper.ValidateTargetEntity(context);

                Entity preImage = UpdateRiskScoreHelper.ValidatePreImage(context);

                // Check if status changed from New to Review
                if (UpdateRiskScoreHelper.IsStatusChangedToReview(targetEntity, preImage, tracingService))
                {
                    tracingService.Trace("Mortgage status changed from New to Review.");

                    // Process contact risk score
                    UpdateRiskScoreHelper.ProcessContactRiskScore(preImage, service, tracingService);
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
    }
}
