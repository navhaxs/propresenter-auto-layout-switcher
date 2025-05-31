using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Serilog;

namespace Logic;

using System.Net;
using System.IO;

public class ProPresenterAPI
{
    private string _port;
    public ProPresenterAPI(string port)
    {
        _port = port;
    }

    public JsonNode? GetCurrentPresentation()
    {
        return JsonNode.Parse(Get($"http://localhost:{_port}/v1/presentation/active?chunked=false"));
    }

    public JsonNode? GetActiveLayers()
    {
        return JsonNode.Parse(Get($"http://localhost:{_port}/v1/status/layers?chunked=false"));
    }

    public Dictionary<String, String> GetLayoutMap()
    {
        var result = Get($"http://localhost:{_port}/v1/stage/layout_map");
        var json = (JsonArray)JsonNode.Parse(result);
        return json.ToArray().ToDictionary(
            obj => (string)obj["screen"]["name"],
            obj => (string)obj["layout"]["name"]
        );
    }

    public void PutLayout(String data)
    {
        Put($"http://localhost:{_port}/v1/stage/layout_map", data);
    }

    public List<string> GetLayouts()
    {
        var result = Get($"http://localhost:{_port}/v1/stage/layouts?chunked=false");
        /*
         * Response body

           [
             {
               "id": {
                 "uuid": "1FEF2B1F-8739-4BCF-AE41-A60AF406430F",
                 "name": "Current + Next Text",
                 "index": 0
               }
             },
             {
               "id": {
                 "uuid": "C04039B7-208B-42D4-8715-7EF6B519A51D",
                 "name": "Slides View",
                 "index": 1
               }
             }
           ]
         */
        var json = (JsonArray)JsonNode.Parse(result);
        var layouts = json.ToArray().Select(x => (JsonObject)x).Select(x => (string)x["id"]["name"]);
        return layouts.ToList();
    }

    private string Get(string requestUriString)
    {
        return httpRequest(new RequestInfo() { method = "GET", requestUriString = requestUriString });
    }

    private void Put(string requestUriString, string? data = null)
    {
        httpRequest(new RequestInfo() { method = "PUT", requestUriString = requestUriString, data = data });
    }

    class RequestInfo
    {
        public string method { get; set; }
        public string requestUriString { get; set; }
        public string? data { get; set; }
    }

    private string httpRequest(RequestInfo requestInfo)
    {
        var request = (HttpWebRequest)WebRequest.Create(requestInfo.requestUriString);
        request.Method = requestInfo.method;
        request.Accept = "application/json";

        if (requestInfo.data != null)
        {
            request.ContentType = "application/json";
            byte[] dataBytes = Encoding.UTF8.GetBytes(requestInfo.data);
            request.ContentLength = dataBytes.Length;

            using (Stream sendStream = request.GetRequestStream())
            {
                sendStream.Write(dataBytes, 0, dataBytes.Length);
                sendStream.Close();
            }
        }

        try
        {
            using (var response = request.GetResponse())
            {
                var stream = response.GetResponseStream();
                using (var reader = new StreamReader(stream))
                {
                    var jsonData = reader.ReadToEnd();
                    return jsonData;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Web request failed");
            return null;
        }
    }
}