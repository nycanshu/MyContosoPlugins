using Microsoft.Xrm.Sdk;
using System;

namespace MyContosoPlugins
{
    public class UpdateMonthlyPaymentOnUpdate : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the execution context
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Ensure this is an Update operation and the target is an entity
            if (context.MessageName.ToLower() == "update" && context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity target = (Entity)context.InputParameters["Target"];

                // Check if relevant fields are being updated
                if (!target.Contains("new_apr") && !target.Contains("new_mortgageamount") && !target.Contains("new_mortgageterm"))
                {
                    // Exit early if none of the relevant fields are being updated
                    return;
                }

                // Retrieve the pre-image for existing values
                Entity preImage = null;
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    preImage = context.PreEntityImages["PreImage"];
                }
                else
                {
                    throw new InvalidPluginExecutionException("Pre-image is required but not available.");
                }

                // Merge updated fields from Target into PreImage
                Entity mortgage = preImage.Clone(); // Clone pre-image
                foreach (var attribute in target.Attributes)
                {
                    mortgage[attribute.Key] = attribute.Value; // Update with new values
                }

                // Validate that all required attributes are present
                if (mortgage.Contains("new_apr") && mortgage.Contains("new_mortgageamount") && mortgage.Contains("new_mortgageterm"))
                {
                    // Retrieve values
                    decimal apr = mortgage.GetAttributeValue<decimal>("new_apr") / 100; // APR as a decimal
                    Money totalAmountMoney = mortgage.GetAttributeValue<Money>("new_mortgageamount");
                    int loanTermMonths = mortgage.GetAttributeValue<int>("new_mortgageterm");

                    if (totalAmountMoney == null)
                    {
                        throw new InvalidPluginExecutionException("Mortgage amount is null.");
                    }

                    decimal totalAmount = totalAmountMoney.Value; // Present value (Pv)

                    // Validate input values
                    if (apr < 0 || totalAmount <= 0 || loanTermMonths <= 0)
                    {
                        throw new InvalidPluginExecutionException("Invalid values for APR, Total Amount, or Loan Term.");
                    }

                    // Calculate periodic interest rate (R)
                    decimal periodicRate = apr / 12; // Monthly interest rate (R)

                    // Calculate monthly payment (P)
                    decimal monthlyPayment;
                    if (periodicRate == 0)
                    {
                        // If the periodic rate is 0, divide total amount by number of months
                        monthlyPayment = totalAmount / loanTermMonths;
                    }
                    else
                    {
                        // Apply the formula: P = (Pv * R) / [1 - (1 + R)^(-n)]
                        decimal denominator = (decimal)(1 - Math.Pow(1 + (double)periodicRate, -loanTermMonths));
                        monthlyPayment = totalAmount * periodicRate / denominator;
                    }

                    // Set the calculated monthly payment back on the entity
                    Entity updateEntity = new Entity(target.LogicalName, target.Id)
                    {
                        ["new_monthlypaymenttext"] = new Money(monthlyPayment)
                    };

                    // Update the entity
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
                    service.Update(updateEntity);
                }
                else
                {
                    throw new InvalidPluginExecutionException("Required attributes APR, Mortgage Amount, or Mortgage Term are missing.");
                }
            }
        }
    }

    public static class EntityExtensions
    {
        // Helper method to clone an entity
        public static Entity Clone(this Entity entity)
        {
            Entity clone = new Entity(entity.LogicalName, entity.Id);
            foreach (var attribute in entity.Attributes)
            {
                clone[attribute.Key] = attribute.Value;
            }
            return clone;
        }
    }
}
