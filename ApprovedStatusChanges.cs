using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using MyContosoPlugins.Helper;

namespace MyContosoPlugins
{
    public class ApprovedStatusChanges : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin execution started.");

            if (!ApproveStatusHelper.IsStatusChangedFromReviewToApproved(context, tracingService))
            {
                tracingService.Trace("Status is not changed to 'Approved'. Plugin execution terminated.");
                return;
            }

            var mortgage = (Entity)context.InputParameters["Target"];

            //get pre image
            Entity preImage = ApproveStatusHelper.GetPreImage(context, tracingService);

            //fetch base apr and pass to calculate funciton
            int baseApr = ApproveStatusHelper.GetBaseAprFromApi(tracingService);


            // Step 1: Calculate Final APR
            var finalApr = ApproveStatusHelper.CalculateFinalApr(mortgage, baseApr, context, service, tracingService);


            // Step 2: Update Final APR in the mortgage record
            ApproveStatusHelper.UpdateFinalApr(mortgage, finalApr,baseApr, service, tracingService);

            // Step 3: Calculate Monthly Payment based on Final APR
            var monthlyPayment = ApproveStatusHelper.CalculateMonthlyPayment(mortgage,preImage, finalApr, tracingService);

            // Step 4: Update Monthly Payment in the mortgage record
           ApproveStatusHelper.UpdateMonthlyPayment(mortgage, monthlyPayment, service, tracingService);

            //create mortgage payment
            ApproveStatusHelper.CreateMortgagePayments(mortgage, preImage, monthlyPayment, service, tracingService);

            tracingService.Trace("Plugin execution completed.");
        }
    }
}
