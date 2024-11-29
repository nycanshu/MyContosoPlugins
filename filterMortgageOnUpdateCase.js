function filterMortgagesOnContactChange(executionContext) {
    try {
        // Get the form context
        const formContext = executionContext.getFormContext();

        // Get the selected Contact from the 'customerid' lookup field
        const contact = formContext.getAttribute("customerid").getValue();

        if (contact && contact[0]) {
            // Retrieve the GUID of the selected Contact
            const contactId = contact[0].id;

            // Create a FetchXML query to filter mortgages related to this Contact
            const fetchXml =
                "<filter type='and'>" +
                "   <condition attribute='new_contact' operator='eq' value='" + contactId + "' />" +
                "</filter>";

            // Apply the custom filter to the 'new_mortgage' lookup field
            const mortgageLookupControl = formContext.getControl("new_mortgage");
            if (mortgageLookupControl) {
                mortgageLookupControl.addCustomFilter(fetchXml, "new_mortgage");
            }
        } else {
            // If no contact is selected, clear the filter
            const mortgageLookupControl = formContext.getControl("new_mortgage");
            if (mortgageLookupControl) {
                mortgageLookupControl.addCustomFilter("", "new_mortgage");
            }
        }
    } catch (error) {
        console.error("Error in filterMortgagesOnContactChange:", error.message);
    }
}
