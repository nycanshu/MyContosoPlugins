﻿using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyContosoPlugins.Helper
{
    public class ApproveStatusHelper
    {

        //to get preimage of the entity
        public static Entity GetPreImage(IPluginExecutionContext context, ITracingService tracingService)
        {
            string PreImageAlias = "PreImage";
            if (context.PreEntityImages.Contains(PreImageAlias))
                return (Entity)context.PreEntityImages[PreImageAlias];
            throw new InvalidPluginExecutionException("Pre-image is not available.");
        }

        //to check if status is changed from review to approved
        public static bool IsStatusChangedFromReviewToApproved(IPluginExecutionContext context, ITracingService tracingService)
        {
            var preImage = GetPreImage(context, tracingService);
            int ApprovedStatus = 100000002;
            int ReviewStatus = 100000001;

            if (!preImage.Contains("statuscode"))
                throw new InvalidPluginExecutionException("Statuscode field is missing from the pre-image.");

            var originalStatus = ((OptionSetValue)preImage["statuscode"]).Value;
            var currentStatus = ((OptionSetValue)((Entity)context.InputParameters["Target"])["statuscode"]).Value;

            return currentStatus == ApprovedStatus && originalStatus == ReviewStatus;
        }

        //to get risk score of the contact
        public static int GetRiskScore(Entity preImage, IOrganizationService service, ITracingService tracingService)
        {
            if (!preImage.Contains("new_contact") || !(preImage["new_contact"] is EntityReference contactRef))
                throw new InvalidPluginExecutionException("Contact reference is missing.");

            var contact = service.Retrieve("contact", contactRef.Id, new ColumnSet("new_riskscore"));
            if (!contact.Contains("new_riskscore") || contact["new_riskscore"] == null)
                throw new InvalidPluginExecutionException("The associated contact does not have a valid Risk Score.");

            var riskScore = contact.GetAttributeValue<int>("new_riskscore");
            if (riskScore <= 0)
                throw new InvalidPluginExecutionException("Risk Score must be greater than 0 for valid APR calculation.");

            tracingService.Trace($"Retrieved Risk Score: {riskScore}");
            return riskScore;
        }

        //to get base apr from api
        public static int GetBaseAprFromApi(ITracingService tracingService)
        {
            try
            {
                // Step 1: Make an HTTP GET request to the API
                using (var client = new System.Net.Http.HttpClient())
                {
                    // Set the base address for the API
                    client.BaseAddress = new Uri("https://contosoapi-38bv.onrender.com");

                    // Send the GET request to the endpoint
                    var response = client.GetAsync("/api/getbaseapr").Result;

                    // Check if the response is successful
                    if (response.IsSuccessStatusCode)
                    {
                        // Step 2: Parse the response JSON to extract the 'baseApr' value
                        var responseData = response.Content.ReadAsStringAsync().Result;
                        var jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(responseData);

                        // Extract 'baseApr' from the response
                        if (jsonResponse != null && jsonResponse.ContainsKey("baseApr"))
                        {
                            int baseApr = jsonResponse["baseApr"];
                            tracingService.Trace($"Base APR fetched from API: {baseApr}");
                            return baseApr; // Return the base APR value from the API
                        }
                        else
                        {
                            tracingService.Trace("API response does not contain a valid baseApr value. Returning default value of 5.");
                            return 5; // Default value in case the API response is invalid
                        }
                    }
                    else
                    {
                        // Handle unsuccessful response
                        tracingService.Trace($"Failed to fetch Base APR from API. Status Code: {response.StatusCode}. Returning default value of 5.");
                        return 5; // Default value if API call fails
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions
                tracingService.Trace($"Exception occurred while fetching Base APR from API: {ex.Message}. Returning default value of 5.");
                return 5; // Return default value in case of an exception
            }
        }

        public static double GetSalesTax(string state, ITracingService tracingService)
        {
            var stateTaxList = new Dictionary<string, double>
{
    { "Alabama", 4.0 },{ "Alaska", 0.0 }, { "Arizona", 5.6 }, { "Arkansas", 6.5 },{ "California", 7.5 },
    { "Colorado", 2.9 },{ "Connecticut", 6.35 },{ "Delaware", 0.0 },{ "District Of Columbia", 5.75 },{ "Florida", 6.0 },
    { "Georgia", 4.0 },{ "Hawaii", 4.0 },{ "Idaho", 6.0 },{ "Illinois", 6.25 },{ "Indiana", 7.0 },{ "Iowa", 6.0 },{ "Kansas", 6.15 },
    { "Kentucky", 6.0 },{ "Louisiana", 4.0 },{ "Maine", 5.5 },{ "Maryland", 6.0 },{ "Massachusetts", 6.25 },
    { "Michigan", 6.0 },{ "Minnesota", 6.875 },{ "Mississippi", 7.0 },{ "Missouri", 4.225 }, { "Montana", 0.0 },{ "Nebraska", 5.5 },
    { "Nevada", 6.85 }, { "New Hampshire", 0.0 },{ "New Jersey", 7.0 }, { "New Mexico", 5.125 }, { "New York", 4.0 },
    { "North Carolina", 4.75 },{ "North Dakota", 5.0 },{ "Ohio", 5.75 }, { "Oklahoma", 4.5 },{ "Oregon", 0.0 },
    { "Pennsylvania", 6.0 }, { "Rhode Island", 7.0 },{ "South Carolina", 6.0 }, { "South Dakota", 4.0 },  { "Tennessee", 7.0 },
    { "Texas", 6.25 },  { "Utah", 5.95 }, { "Vermont", 6.0 }, { "Virginia", 5.3 }, { "Washington", 6.5 }, { "West Virginia", 6.0 },
    { "Wisconsin", 5.0 }, { "Wyoming", 4.0 }
};

            return stateTaxList.ContainsKey(state) ? stateTaxList[state] : 0.0;
        }

        public static decimal CalculateFinalApr(Entity mortgage, int baseApr, IPluginExecutionContext context, IOrganizationService service, ITracingService tracingService)
        {
            var preImage = GetPreImage(context, tracingService);
            var margin = 0.02M;
            var salesTax = 0.05;

            tracingService.Trace($"Base APR: {baseApr}, Margin: {margin}, Default Sales Tax: {salesTax}");

            var region = ((OptionSetValue)preImage["new_region"]).Value;
            if (region == 100000000) // US
            {
                var state = preImage.GetAttributeValue<string>("new_state");
                if (!string.IsNullOrEmpty(state))
                    salesTax = GetSalesTax(state, tracingService);
            }

            var riskScore = GetRiskScore(preImage, service, tracingService);
            var finalApr = (decimal)((baseApr + (double)margin) + Math.Log(riskScore) + salesTax);

            tracingService.Trace($"Calculated Final APR: {finalApr}");
            return finalApr;
        }

        public static decimal CalculateMonthlyPayment(Entity mortgage, Entity preImage, decimal apr, ITracingService tracingService)
        {
            tracingService.Trace("Starting Monthly Payment calculation.");

            decimal loanAmount = mortgage.Contains("new_mortgageamount")
                ? mortgage.GetAttributeValue<Money>("new_mortgageamount").Value
                : preImage.GetAttributeValue<Money>("new_mortgageamount").Value;

            int term = mortgage.Contains("new_mortgageterm")
                ? mortgage.GetAttributeValue<int>("new_mortgageterm")
                : preImage.GetAttributeValue<int>("new_mortgageterm");

            var periodicInterestRate = apr / 12 / 100;
            var numberOfInterestPeriods = term * 12;

            var monthlyPayment = (loanAmount * periodicInterestRate) /
                                 (1 - (decimal)Math.Pow(1 + (double)periodicInterestRate, -numberOfInterestPeriods));

            tracingService.Trace($"Calculated Monthly Payment: {monthlyPayment}");
            return monthlyPayment;
        }

        public static void UpdateFinalApr(Entity mortgage, decimal finalApr, int baseApr, IOrganizationService service, ITracingService tracingService)
        {
            mortgage["new_apr"] = finalApr;
            mortgage["new_baseapr"] = baseApr;
            service.Update(mortgage);
            tracingService.Trace($"Final APR updated: {finalApr}");
        }

        public static void UpdateMonthlyPayment(Entity mortgage, decimal monthlyPayment, IOrganizationService service, ITracingService tracingService)
        {
            mortgage["new_monthlypaymenttext"] = new Money(monthlyPayment);
            service.Update(mortgage);
            tracingService.Trace($"Monthly Payment updated: {monthlyPayment}");
        }

        public static void CreateMortgagePayments(Entity mortgage, Entity preImage, decimal monthlyPayment, IOrganizationService service, ITracingService tracingService)
        {
            tracingService.Trace("Starting to create multiple Mortgage Payments.");

            // Step 1: Retrieve Mortgage ID, Contact, and other required fields
            if (!mortgage.Contains("new_contact") && !preImage.Contains("new_contact"))
            {
                throw new InvalidPluginExecutionException("Contact lookup is missing in both the Target and PreImage.");
            }

            EntityReference contactReference = mortgage.Contains("new_contact")
                ? mortgage.GetAttributeValue<EntityReference>("new_contact")
                : preImage.GetAttributeValue<EntityReference>("new_contact");

            if (!mortgage.Contains("new_mortgagenumber") && !preImage.Contains("new_mortgagenumber"))
            {
                throw new InvalidPluginExecutionException("Mortgage number is missing in both the Target and PreImage.");
            }

            string mortgageNumber = mortgage.Contains("new_mortgagenumber")
                ? mortgage.GetAttributeValue<string>("new_mortgagenumber")
                : preImage.GetAttributeValue<string>("new_mortgagenumber");

            if (!mortgage.Contains("new_newcolumn") && !preImage.Contains("new_newcolumn"))
            {
                throw new InvalidPluginExecutionException("Mortgage name (new_newcolumn) is missing in both the Target and PreImage.");
            }

            string mortgageName = mortgage.Contains("new_newcolumn")
                ? mortgage.GetAttributeValue<string>("new_newcolumn")
                : preImage.GetAttributeValue<string>("new_newcolumn");

            // Step 2: Construct Mortgage Payment Name
            string mortgagePaymentName = $"{mortgageName} - {mortgageNumber}";
            tracingService.Trace($"Constructed Mortgage Payment Name: {mortgagePaymentName}");

            // Step 3: Retrieve Term and Start Date
            int termMonths = mortgage.Contains("new_mortgageterm")
                ? mortgage.GetAttributeValue<int>("new_mortgageterm")
                : preImage.GetAttributeValue<int>("new_mortgageterm");


            DateTime startDate = DateTime.Now;  //  use current date

            // Step 4: Create Mortgage Payment Records for each month
            for (int i = 0; i < termMonths; i++)
            {
                // Calculate Due Date as one month after the start date
                DateTime dueDate = startDate.AddMonths(i);

                // Construct the Mortgage Payment record
                var mortgagePayment = new Entity("new_mortgagepayment");
                mortgagePayment["new_newcolumn"] = mortgagePaymentName; // Payment Name
                mortgagePayment["new_mortgage"] = new EntityReference(mortgage.LogicalName, mortgage.Id); // Link to Mortgage
                mortgagePayment["new_contact"] = contactReference; // Link to Contact
                mortgagePayment["new_monthlypayment"] = new Money(monthlyPayment); // Monthly Payment
                mortgagePayment["new_duedate"] = dueDate; // Due Date (monthly gap)

                // Create the Mortgage Payment record
                service.Create(mortgagePayment);
                tracingService.Trace($"Mortgage Payment {mortgagePaymentName} for due date {dueDate.ToString("yyyy-MM-dd")} created successfully.");
            }
        }


    }
}