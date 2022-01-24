using Azure;
using Azure.Data.Tables;
using HtmlAgilityPack;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FunctionApp1
{
    public class Function1

    {
        [FunctionName("Function1")]
        //public async Task RunAsync([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
        public async Task RunAsync([TimerTrigger("*/1 * * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            try
            {
                var tableClient = new TableClient("DefaultEndpointsProtocol=https;AccountName=velos;AccountKey=PV5sCd9v9j1FE+20qCCAZnxRFtIKINyRLMTaH27IDowvux+wvhXK3iUUnPW24lob9j/Kc2jKQVJb4djJyNaJyQ==;EndpointSuffix=core.windows.net", "commuter2");
                await tableClient.CreateIfNotExistsAsync();

                var pageable = tableClient.Query<Velo>(filter: $"PartitionKey eq 'velo'", maxPerPage: 100);
                var lastVelos = new List<Velo>();
                lastVelos.AddRange(pageable);
                await tableClient.DeleteAsync();

                var url = "https://www.canyon.com/de-ch/fahrrad-outlet/trekking-city/?cgid=outlet-hybrid-city&prefn1=pc_familie&prefv1=Commuter";
                HtmlWeb web = new();
                var document = web.Load(url);
                var nodes = document.DocumentNode.SelectNodes("//li[contains(@class,'productGrid__listItem')]");

                var velos = new List<Velo>();
                foreach (HtmlNode node in nodes)
                {
                    velos.Add(new Velo
                    (
                        GetAttr(node, "productTile__productName"),
                        GetAttr(node, "productTile__size"),
                        GetAttr(node, "productTile__priceSale"),
                        GetLink(node)
                    ));
                }

                foreach (var velo in velos)
                {
                    var res = await tableClient.AddEntityAsync(velo);
                }

                var newVelos = velos?.FindAll(v => (bool)!lastVelos?.Exists(lv => lv.Name == v.Name && lv.Size == v.Size && lv.Price == v.Price && lv.Link == v.Link));
                SendMail();
            }
            catch (Exception e)
            {
                log.LogInformation($"Exception Caught! {e.Message} // {e.StackTrace}");
            }
        }

        private static string GetAttr(HtmlNode node, string attr)
        {
            var subNode = node.SelectSingleNode(".//div[contains(@class, '" + attr + "')]");
            return subNode?.GetDirectInnerText()?.Replace("\n", "")?.Replace("\r", "")?.Trim();
        }

        private static string GetLink(HtmlNode node)
        {
            var subNode = node.SelectSingleNode(".//a[contains(@class, 'productTile__link')]");
            return subNode?.Attributes["href"]?.Value?.Replace("\n", "")?.Replace("\r", "")?.Trim();
        }

        public static void SendMail()
        {
            var message = new MailMessage("*", "*")
            {
                Subject = "Using the new SMTP client.",
                Body = @"Using this new feature, you can send an email message from an application very easily."
            };
            var client = new SmtpClient("smtp.live.com", 25)
            {
                Credentials = new NetworkCredential("*", "*"),
                EnableSsl = true
            };
            client.Send(message);
        }
    }

    public class Velo : ITableEntity
    {
        public Velo() { }
        public Velo (string name, string size, string price, string link)
        {
            PartitionKey = "velo";
            RowKey = Guid.NewGuid().ToString()[..7];
            Name = name;
            Size = size;
            Price = price;
            Link = link;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string Name { get; set; }
        public string Size { get; set; }
        public string Price { get; set; }
        public string Link { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

}
