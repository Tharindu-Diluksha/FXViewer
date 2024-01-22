using System;
using HtmlAgilityPack;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FXViewer.Background
{
    public class FetchExchangeRates
    {
        private readonly ILogger _logger;
        private static HttpClient client = new HttpClient();
        private readonly IConfiguration _configuration;

        public FetchExchangeRates(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<FetchExchangeRates>();
            _configuration = configuration;
        }

        [Function("FetchExchangeRates")]
        public async Task Run([TimerTrigger("%TimerSchedule%")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

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

                _logger.LogInformation($"Saved Euro exchange rate: {euroRate}");
            }
            else
            {
                _logger.LogError($"Failed to fetch exchange rate: {response.StatusCode}");
            }

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
