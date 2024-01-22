using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;


namespace FXViewer.Background
{
    public class FetchExchangeRate
    {
        private static HttpClient client = new HttpClient();
        private readonly IConfiguration _configuration;

        public FetchExchangeRate(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("FetchExchangeRate")]
        public async Task Run([TimerTrigger("%TimerSchedule%")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var response = await client.GetAsync(_configuration["APIURL"]);

            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync();
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);

                var euroRow = htmlDocument.DocumentNode.SelectSingleNode("//tr[td[contains(text(), 'Euro')]]");
                var euroRate = euroRow.SelectSingleNode("td[contains(@class, 'exrateText')][3]").InnerText;

                var document = new
                {
                    Date = DateTime.UtcNow,
                    ExchangeRate = euroRate
                };

                var cosmosDbUri = _configuration["CosmosDbUri"];
                var cosmosDbKey = _configuration["CosmosDbKey"];
                var dbName = _configuration["DbName"];
                var collectionName = _configuration["CollectionName"];

                using (var documentClient = new DocumentClient(new Uri(cosmosDbUri), cosmosDbKey))
                {
                    await documentClient.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(dbName, collectionName), document);
                }

                log.LogInformation($"Saved Euro exchange rate: {euroRate}");
            }
            else
            {
                log.LogError($"Failed to fetch exchange rate: {response.StatusCode}");
            }
        }
    }
}

