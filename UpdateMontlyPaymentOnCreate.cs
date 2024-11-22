using Microsoft.Xrm.Sdk;
using System;

namespace MyContosoPlugins
{
    public class UpdateMonthlyPaymentOnCreate : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the execution context
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Ensure this is a Create operation and the target is an entity
            if (context.MessageName.ToLower() == "create" && context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity mortgage = (Entity)context.InputParameters["Target"];

                // Validate that required attributes are provided
                if (mortgage.Contains("new_apr") && mortgage.Contains("new_mortgageamount") && mortgage.Contains("new_mortgageterm"))
                {
                    // Retrieve values from the entity
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
                    mortgage["new_monthlypaymenttext"] = new Money(monthlyPayment);
                }
                else
                {
                    throw new InvalidPluginExecutionException("Required attributes APR, Mortgage Amount, or Mortgage Term are missing.");
                }
            }
        }
    }
}
