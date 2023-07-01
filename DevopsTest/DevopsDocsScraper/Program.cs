// Devops Docs Parser.

using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Net.Http;
using System.Xml.Linq;

var apiVer = "7.1";
var baseUrl = $"https://learn.microsoft.com/en-us/rest/api/azure/devops/";
var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(baseUrl);
var response = await httpClient.GetAsync($"toc.json?view=azure-devops-rest-{apiVer}");
var jsonContent = await response.Content.ReadAsStringAsync();
var toc = JsonConvert.DeserializeObject<DevopsToc>(jsonContent);

var endpoints = GetLeafNodes(toc);
var allModels = new List<dynamic>();


var allEndpointInfo = new List<dynamic>();
var rnd = new Random();
int count = 1;

var actualEndpoints = endpoints.Where(x => x.toc_title != "Overview").ToList();
foreach (var endpoint in actualEndpoints)
{
    //Console.WriteLine(endpoint.href);

    try
    {
        allEndpointInfo.Add(await GetAllEndpointData(endpoint));
    } catch {
        // retry once for good measure.
        try { 
        allEndpointInfo.Add(await GetAllEndpointData(endpoint));
        }
        catch
        {
            Console.WriteLine();
            Console.BackgroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to get " + count);
            Console.ResetColor();
        }

    }

    // Add a small dely to not get flagged for ddos.
    // There are roughly 1000 endpoints so it should take at most
    // 17 minutes.
    //await Task.Delay(rnd.Next(1000));
    Console.Write($"\rProcessing {count++}/{actualEndpoints.Count}");
}
Console.WriteLine();


var jsonOutput = JsonConvert.SerializeObject(new
{
    Endpoints = allEndpointInfo,
    Models = allModels,
});

File.WriteAllText("devopsApi.json",jsonOutput);













async Task<dynamic> GetAllEndpointData(Child child)
{
    var endpointResponse = await httpClient.GetAsync($"{child.href}?view=azure-devops-rest-{apiVer}");
    var htmlContent = await endpointResponse.Content.ReadAsStringAsync();
    htmlContent = StripOfSpecialCharacters(htmlContent);
    var html = new HtmlDocument();
    html.LoadHtml(htmlContent);
    #region URL
    var urlElement = html.DocumentNode.SelectSingleNode("//code[1]");
    //Console.WriteLine(urlElement.InnerText);
    #endregion

    #region URI Parameters
    var uriParameters = GetUriParameters(html);

    #endregion

    #region RequestBody (if Available)
    var bodyParameters = GetBodyParameters(html);

    #endregion

    #region ResponseBody
    var responseBody = GetResponseBodyType(html);

    #endregion

    #region Update Models Collection
    UpdateAllModels(html);

    #endregion

    //Console.WriteLine($"uriParameters: {uriParameters.Count}; bodyParameters: {bodyParameters?.Count ?? 0}; responseBody: {responseBody}; allModels: {allModels.Count}");

    return new
    {
        Url = urlElement.InnerText,
        uriParameters,
        bodyParameters,
        responseBody
    };
}

void UpdateAllModels(HtmlDocument html)
{
    var modelTables = html.DocumentNode.SelectNodes("//h2[text()=\"Definitions\"]/following-sibling::table");

    // No models defined.
    if (modelTables == null) return;

    // Skip first Table as it is just a link to the others.
    foreach (var modelTable in modelTables.Skip(1))
    {
        var modelProperties = new List<dynamic>();
        var description = modelTable.PreviousSibling;
        HtmlNode title;
        if (description.Name == "p")
        {
            title = description.PreviousSibling;
        }
        else
        {
            title = description;
        }
        
        // no need to add the same model twice.
        if (allModels.Any(x => x.Name == title.InnerHtml)) continue;

        var properties = modelTable.ChildNodes.Where(x => x.NodeType != HtmlNodeType.Text).Skip(1);
        foreach (var property in properties)
        {
            var asList = property.ChildNodes.Where(x => x.NodeType != HtmlNodeType.Text).ToList();

            modelProperties.Add(new
            {
                Name = asList[0]?.FirstChild?.InnerText,
                Type = asList[1]?.FirstChild?.InnerText,
                Description = asList[2]?.FirstChild?.InnerText
            });
        }

        allModels.Add(
            new
            {
                Name = title.InnerHtml,
                Description = description.InnerHtml,
                Properties = modelProperties
            });
    }
}

string? GetResponseBodyType(HtmlDocument html)
{
    var responsesTableRows = html.DocumentNode.SelectSingleNode("//h2[text()=\"Responses\"]/following-sibling::table");
    var responsesTableRowsWithoutHeaders = responsesTableRows.ChildNodes.Where(x => x.NodeType != HtmlNodeType.Text).Skip(1).First();

    var asList = responsesTableRowsWithoutHeaders.ChildNodes.Where(x => x.NodeType != HtmlNodeType.Text).ToList();
    return asList[1]?.FirstChild?.InnerText;
}

static List<dynamic>? GetBodyParameters(HtmlDocument html)
{
    var bodyParameters = new List<dynamic>();

    // Headers = Name, In, Required, Type, Description
    var bodyParamTableRows = html.DocumentNode.SelectSingleNode("//h2[text()=\"Request Body\"]/following-sibling::table");


    if (bodyParamTableRows == null) return null;


    var bodyParamTableRowsWithoutHeaders = bodyParamTableRows.ChildNodes.Where(x => x.NodeType != HtmlNodeType.Text).Skip(1);
    foreach (var param in bodyParamTableRowsWithoutHeaders)
    {
        var asList = param.ChildNodes.Where(x => x.NodeType != HtmlNodeType.Text).ToList();

        bodyParameters.Add(new
        {
            Name = asList[0]?.FirstChild?.InnerText,
            Type = asList[1]?.FirstChild?.InnerText,
            Description = asList[2]?.FirstChild?.InnerText
        });
    }

    return bodyParameters;
}

static List<dynamic> GetUriParameters(HtmlDocument html)
{
    var uriParameters = new List<dynamic>();

    // Headers = Name, In, Required, Type, Description
    var uriParamTableRows = html.DocumentNode.SelectSingleNode("//h2[text()=\"URI Parameters\"]/following-sibling::table");
    var uriParamTableRowsWithoutHeaders = uriParamTableRows.ChildNodes.Where(x => x.NodeType != HtmlNodeType.Text).Skip(1);
    foreach (var param in uriParamTableRowsWithoutHeaders)
    {
        var asList = param.ChildNodes.Where(x => x.NodeType != HtmlNodeType.Text).ToList();

        uriParameters.Add(new
        {
            Name = asList[0]?.FirstChild?.InnerText,
            In = asList[1]?.FirstChild?.InnerText,
            Required = asList[2]?.FirstChild?.InnerText,
            Type = asList[3]?.FirstChild?.InnerText,
            Description = asList[4]?.FirstChild?.InnerText
        });
    }

    return uriParameters;
}

string StripOfSpecialCharacters(string htmlContent)
{
    return htmlContent.Replace("\t", "").Replace("\n", "").Replace("\r", "");
}

static List<Child> GetLeafNodes(DevopsToc devopsToc)
{
    List<Child> leafNodes = new List<Child>();

    foreach (Item item in devopsToc.items)
    {
        GetLeafNodesRecursive(item.children, leafNodes);
    }

    return leafNodes;
}

static void GetLeafNodesRecursive(List<Child> children, List<Child> leafNodes)
{
    if (children == null || children.Count == 0)
    {
        return;
    }

    foreach (Child child in children)
    {
        if (child.children == null || child.children.Count == 0)
        {
            leafNodes.Add(child);
        }
        else
        {
            GetLeafNodesRecursive(child.children, leafNodes);
        }
    }
}

public class Child
{
    public string href { get; set; }
    public string toc_title { get; set; }
    public List<Child> children { get; set; }
}

public class Item
{
    public string href { get; set; }
    public List<string> monikers { get; set; }
    public string toc_title { get; set; }
    public List<Child> children { get; set; }
}

public class Metadata
{
    public string author { get; set; }
    public int count_of_node_with_href { get; set; }
    public string default_moniker { get; set; }
    public string feedback_github_repo { get; set; }
    public string feedback_product_url { get; set; }
    public string feedback_system { get; set; }
    public bool hideScope { get; set; }
    public List<string> monikers { get; set; }

    [JsonProperty("ms.author")]
    public string msauthor { get; set; }

    [JsonProperty("ms.devlang")]
    public string msdevlang { get; set; }

    [JsonProperty("ms.service")]
    public string msservice { get; set; }

    [JsonProperty("ms.subservice")]
    public string mssubservice { get; set; }

    [JsonProperty("ms.topic")]
    public string mstopic { get; set; }
    public bool open_to_public_contributors { get; set; }
    public List<string> products { get; set; }
    public string rest_product { get; set; }
    public string titleSuffix { get; set; }
    public string uhfHeaderId { get; set; }
}

public class DevopsToc
{
    public List<Item> items { get; set; }
    public Metadata metadata { get; set; }
}





