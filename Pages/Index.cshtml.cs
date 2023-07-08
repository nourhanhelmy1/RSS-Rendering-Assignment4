using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Xml.Linq;

namespace RenderingRSS.Pages
{
    public class IndexModel : PageModel
    {
        private const int PageSize = 10;

        private readonly IHttpClientFactory _httpClientFactory;
        public List<ItemProperties> ItemsProperties { get; set; } = new List<ItemProperties>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var opmlResponse = await FetchXmlContentAsync(httpClient, "https://blue.feedland.org/opml?screenname=dave");

            if (opmlResponse.IsSuccessStatusCode)
            {
                var opmlContent = await opmlResponse.Content.ReadAsStringAsync();
                var feedUrls = ParseOpmlContent(opmlContent);

                var tasks = feedUrls.Select(url => FetchAndParseRssFeedAsync(httpClient, url));
                var rssResponses = await Task.WhenAll(tasks);
                ItemsProperties = rssResponses.SelectMany(r => r).ToList();

                TotalPages = (int)Math.Ceiling((double)ItemsProperties.Count / PageSize);
                CurrentPage = pageNumber;

                // Apply paging
                ItemsProperties = ItemsProperties
                    .Skip((pageNumber - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                return Page();
            }
            else
            {
                return RedirectToPage("/Error");
            }
        }

        private async Task<HttpResponseMessage> FetchXmlContentAsync(HttpClient httpClient, string url)
        {
            return await httpClient.GetAsync(url);
        }

        private List<string> ParseOpmlContent(string opmlContent)
        {
            var feedUrls = new List<string>();

            var doc = XDocument.Parse(opmlContent);
            var outlines = doc.Descendants("outline");

            foreach (var outline in outlines)
            {
                var xmlUrl = outline.Attribute("xmlUrl")?.Value;
                if (!string.IsNullOrEmpty(xmlUrl))
                {
                    feedUrls.Add(xmlUrl);
                }
            }

            return feedUrls;
        }

        private async Task<List<ItemProperties>> FetchAndParseRssFeedAsync(HttpClient httpClient, string url)
        {
            var itemPropertiesList = new List<ItemProperties>();

            var response = await FetchXmlContentAsync(httpClient, url);
            if (response.IsSuccessStatusCode)
            {
                var xmlContent = await response.Content.ReadAsStringAsync();
                itemPropertiesList = ParseXmlContent(xmlContent);
            }

            return itemPropertiesList;
        }

        private List<ItemProperties> ParseXmlContent(string xmlContent)
        {
            var itemPropertiesList = new List<ItemProperties>();
            var doc = XDocument.Parse(xmlContent);
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                var itemProperties = new ItemProperties
                {
                    Description = item.Element("description")?.Value,
                    PubDate = item.Element("pubDate")?.Value,
                    Link = item.Element("link")?.Value,
                };

                itemPropertiesList.Add(itemProperties);
            }

            return itemPropertiesList;
        }
    }

    public class ItemProperties
    {
        public string Description { get; set; }
        public string PubDate { get; set; }
        public string Link { get; set; }
    }
}
