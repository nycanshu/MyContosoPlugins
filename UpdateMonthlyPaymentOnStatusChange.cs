using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace MyContosoPlugins
{
    public class UpdateMonthlyPaymentOnStatusChange : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            tracingService.Trace("Plugin execution started.");

            // Retrieve the mortgage entity
            var mortgage = (Entity)context.InputParameters["Target"];

            // Update the monthly payment
            UpdateMonthlyPayment(mortgage, context, service, tracingService);

            tracingService.Trace("Plugin execution completed.");
        }

        private void UpdateMonthlyPayment(Entity mortgage, IPluginExecutionContext context, IOrganizationService service, ITracingService tracingService)
        {
            try
            {
                // Retrieve fields from mortgage
                if (!mortgage.Contains("new_mortgageamount") || !mortgage.Contains("new_apr") || !mortgage.Contains("new_mortgageterm"))
                {
                    throw new InvalidPluginExecutionException("Required fields for monthly payment calculation are missing.");
                }

                var loanAmount = mortgage.GetAttributeValue<Money>("new_mortgageamount").Value;
                var apr = mortgage.GetAttributeValue<decimal>("new_apr");
                var term = mortgage.GetAttributeValue<int>("new_mortgageterm");

                // Calculate the monthly payment
                var monthlyPayment = CalculateMonthlyPayment(loanAmount, apr, term, tracingService);

                // Update the mortgage entity with the calculated monthly payment
                mortgage["new_monthlypaymenttext"] = new Money(monthlyPayment);
                service.Update(mortgage);
                tracingService.Trace($"Monthly payment updated successfully: {monthlyPayment}");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error updating monthly payment: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error updating monthly payment: {ex.Message}");
            }
        }

        private decimal CalculateMonthlyPayment(decimal loanAmount, decimal apr, int term, ITracingService tracingService)
        {
            // Calculate the periodic interest rate (R)
            var periodicInterestRate = apr / 12 / 100;

            // Calculate the number of interest periods (n)
            var numberOfInterestPeriods = term * 12;

            // Calculate the monthly payment using the formula: P = (Pv * R) / [1 - (1 + R)^(-n)]
            var monthlyPayment = (loanAmount * periodicInterestRate) / (1 - (decimal)Math.Pow(1 + (double)periodicInterestRate, -numberOfInterestPeriods));

            tracingService.Trace($"Calculated monthly payment: {monthlyPayment}");

            return monthlyPayment;
        }
    }
}
