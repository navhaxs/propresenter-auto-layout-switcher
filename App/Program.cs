using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Logic;
using Serilog;
using YamlDotNet.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Starting...");

// Load the YAML config file
string configFilePath = @"config.yml";
var yamlConfig = new Deserializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(configFilePath));

Log.Information("Loaded config");

var watcher = new ProPresenterWebsocketWatcher(yamlConfig["port"].ToString(), yamlConfig["password"].ToString());
var api = new ProPresenterAPI(yamlConfig["port"].ToString());
var debounceTimer = new System.Timers.Timer(200) {AutoReset = false};

object lock_object = new();

debounceTimer.Elapsed += (sender, args) =>
{
    Log.Information("Trigger layout change check");
    lock (lock_object)
    {
        var layers_info = api.GetActiveLayers();
        bool shouldUseSongLayout = false;
        //"media": true,
        //"slide": false,
        if (layers_info["media"].ToString() == "true" && layers_info["slide"].ToString() == "false")
        {
            shouldUseSongLayout = false;
        }
        else
        {
            var presentation_info = api.GetCurrentPresentation();
            if (presentation_info == null)
            {
                return;
            }
        
            var presentation_path  = presentation_info["presentation"]?["presentation_path"];
            if (presentation_path == null)
            {
                return;
            }

            var library = Regex.Match(presentation_path.ToString(), @"(?<=Libraries\/).+?(?=/)").Value;
            if (library.Equals(string.Empty))
            {
                library = Regex.Match(presentation_path.ToString(), @"(?<=Libraries\\).+?(?=\\)").Value;
            }
            if (library.Equals(string.Empty))
            {
                return;
            }

            shouldUseSongLayout = library.Equals(yamlConfig["song_library"]);
        }
        
        var target_layout = (string)(shouldUseSongLayout ? yamlConfig["song_layout"] : yamlConfig["slides_layout"]);

        var layoutMap = api.GetLayoutMap();

        /*
         * [
             {
               "layout": {
                 "name": "Sermon Notes"
               },
               "screen": {
                 "name": "Side Screen"
               }
             }
           ]
         */

        var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();

        foreach (var screen in layoutMap)
        {
            if (screen.Value != target_layout)
            {
                Log.Information($"Changing layout for screen [{screen.Key}] to [{target_layout}]");

                writer.WriteStartObject();

                writer.WriteStartObject("screen");
                writer.WriteString("name", screen.Key);
                writer.WriteEndObject();

                writer.WriteStartObject("layout");
                writer.WriteString("name", target_layout);
                writer.WriteEndObject();

                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();
        writer.Flush();
        stream.Flush();

        stream.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new StreamReader(stream);
        string jsonString = reader.ReadToEnd();

        api.PutLayout(jsonString);
    }
};

var lastPresentationPath = "";
watcher.OnMsgRecvd += (sender, args) =>
{
    /*
      {
        "action": "presentationTriggerIndex",
        "slideIndex": 11,
        "presentationDestination": 0,
        "presentationPath": "1:6"
      }
    */
    var message = JsonNode.Parse(args.msg);
    if (message?["action"] == null)
    {
        return;
    }
    
    var action = message["action"].ToString();

    switch (action)
    {
        case "presentationTriggerIndex":
        {
            var currentPresentationPath = message["presentationPath"]?.ToString();
            if (currentPresentationPath == null || lastPresentationPath.Equals(currentPresentationPath))
            {
                return;
            }

            lastPresentationPath = currentPresentationPath;
            break;
        }
        case "authenticate":
        {
            if (message["authenticated"].ToString() != "1")
            {
                return;
            }

            lastPresentationPath = "";
            break;
        }
        case "clearText":
        case "clearVideo":
            lastPresentationPath = "";
            break;
        default:
            return;
    }

    debounceTimer.Stop();
    debounceTimer.Start();
};

watcher.Start();