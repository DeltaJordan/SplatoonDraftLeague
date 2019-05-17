using AirtableApiClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Plus0_Bot.Core.Airtable
{
    public static class Data
    {
        private static string baseId = "appHvHRfmPn96vjoL";
        private static string appKey = "keyCBOb1hwDrUgXdl";
        static public async Task<string> GetUsername(ulong id)
        {
            string offset = null;
            string errorMessage = null;
            var records = new List<AirtableRecord>();

            using (AirtableBase airtableBase = new AirtableBase(appKey, baseId))
            {
                //
                // Use 'offset' and 'pageSize' to specify the records that you want
                // to retrieve.
                // Only use a 'do while' loop if you want to get multiple pages
                // of records.
                //

                do
                {
                    Task<AirtableListRecordsResponse> task = airtableBase.ListRecords(
                           "Draft Standings",
                           offset,
                           null,
                           "filterByFormula={DiscordID}=\"301733804949766144\"",
                           5,
                           null,
                           null,
                           null);

                    AirtableListRecordsResponse response = await task;

                    if (response.Success)
                    {
                        records.AddRange(response.Records.ToList());
                        offset = response.Offset;
                    }
                    else if (response.AirtableApiError is AirtableApiException)
                    {
                        errorMessage = response.AirtableApiError.ErrorMessage;
                        break;
                    }
                    else
                    {
                        errorMessage = "Unknown error";
                        break;
                    }
                } while (offset != null);
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                // Error reporting
                return errorMessage;
            }
            else
            {
                // Do something with the retrieved 'records' and the 'offset'
                // for the next page of the record list.
                string test = "";
                

                foreach (KeyValuePair<string, object> item in records[0].Fields)
                {
                    test += "\n" + item.Value.ToString() + " ";
                    ;
                }
                return test;
            }
        }
    }
}
