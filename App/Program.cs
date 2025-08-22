using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Logic;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Observable;
using YamlDotNet.Serialization;

namespace ProPresenter_StageDisplayLayout_AutoSwitcher
{
    internal static class Program
    {
        // Keep references to prevent GC while running in WinForms context
        private static ProPresenterWebsocketWatcher? _watcher;
        private static ProPresenterAPI? _api;
        private static System.Timers.Timer? _debounceTimer;
        private static readonly object _lockObject = new();
        private static string _lastPresentationPath = string.Empty;
        private static Dictionary<string, object>? _yamlConfig;

        // Single-instance synchronization primitives
        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _showLogsEvent;
        private static RegisteredWaitHandle? _showLogsWaitHandle;
        private const string MutexName = "Local\\ProPresenterAutoLayoutSwitcher_SingleInstance";
        private const string ShowLogsEventName = "Local\\ProPresenterAutoLayoutSwitcher_ShowLogs";

        [STAThread]
        private static void Main()
        {
            // Ensure only one instance is running. If another is running, signal it to show logs and exit.
            if (!EnsureSingleInstance())
                return;

            InitializeLogging();
            Log.Information("Starting application...");

            try
            {
                LoadConfig();
                StartCoreLogic();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize core logic");
            }

            ApplicationConfiguration.Initialize();
            Application.ApplicationExit += (_, _) => CleanupSingleInstance();
            Application.Run(new TrayApplicationContext());
        }

        private static void InitializeLogging()
        {
            System.IObservable<Serilog.Events.LogEvent>? logEvents = null;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.Observers(events => logEvents = events)
                .CreateLogger();

            // Subscribe after logger creation to ensure the stream is alive
            try
            {
                logEvents?.Subscribe(SerilogUiBridge.Emit);
            }
            catch
            {
                // Ignore subscription errors
            }
        }

        private static void LoadConfig()
        {
            // Load the YAML config file
            const string configFilePath = "config.yml";
            _yamlConfig = new Deserializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(configFilePath));
            Log.Information("Loaded config");
        }

        private static void StartCoreLogic()
        {
            if (_yamlConfig == null) throw new InvalidOperationException("Config not loaded");

            _watcher = new ProPresenterWebsocketWatcher(_yamlConfig["port"].ToString(), _yamlConfig["password"].ToString());
            _api = new ProPresenterAPI(_yamlConfig["port"].ToString());
            _debounceTimer = new System.Timers.Timer(200) { AutoReset = false };

            // Bridge connection status to UI
            try
            {
                _watcher.ConnectionStateChanged += (_, connected) =>
                {
                    AppConnectionIndicator.SetConnected(connected);
                };
                // initial state
                AppConnectionIndicator.SetConnected(false);
            }
            catch { /* ignore */ }

            _debounceTimer.Elapsed += (_, _) =>
            {
                try
                {
                    Log.Information("Trigger layout change check");
                    lock (_lockObject)
                    {
                        var layers_info = _api!.GetActiveLayers();
                        bool shouldUseSongLayout = false;
                        //"media": true,
                        //"slide": false,
                        if (layers_info["media"].ToString() == "true" && layers_info["slide"].ToString() == "false")
                        {
                            shouldUseSongLayout = false;
                        }
                        else
                        {
                            var presentation_info = _api.GetCurrentPresentation();
                            if (presentation_info == null)
                            {
                                return;
                            }

                            var presentation_path = presentation_info["presentation"]?["presentation_path"];
                            if (presentation_path == null)
                            {
                                return;
                            }

                            var library = StringUtil.ExtractLibraryNameFromPath(presentation_path.ToString());
                            if (library.Equals(string.Empty))
                            {
                                return;
                            }

                            shouldUseSongLayout = library.Equals(_yamlConfig["song_library"]);
                        }

                        var target_layout = (string)(shouldUseSongLayout ? _yamlConfig["song_layout"] : _yamlConfig["slides_layout"]);

                        var layoutMap = _api.GetLayoutMap();

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

                        _api.PutLayout(jsonString);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during layout change check");
                }
            };

            _watcher.OnMsgRecvd += (_, args) =>
            {
                try
                {
                    var message = JsonNode.Parse(args.msg);
                    if (message?["action"] == null)
                    {
                        return;
                    }

                    var action = message["action"]!.ToString();

                    switch (action)
                    {
                        case "presentationTriggerIndex":
                        {
                            var currentPresentationPath = message["presentationPath"]?.ToString();
                            if (currentPresentationPath == null || _lastPresentationPath.Equals(currentPresentationPath))
                            {
                                return;
                            }

                            _lastPresentationPath = currentPresentationPath;
                            break;
                        }
                        case "authenticate":
                        {
                            if (message["authenticated"]!.ToString() != "1")
                            {
                                return;
                            }

                            _lastPresentationPath = string.Empty;
                            break;
                        }
                        case "clearText":
                        case "clearVideo":
                            _lastPresentationPath = string.Empty;
                            break;
                        default:
                            return;
                    }

                    _debounceTimer!.Stop();
                    _debounceTimer.Start();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error handling websocket message");
                }
            };

            _watcher.Start();
        }

        private static bool EnsureSingleInstance()
        {
            try
            {
                _singleInstanceMutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out bool createdNew);
                _showLogsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowLogsEventName, out _);
                if (!createdNew)
                {
                    try { _showLogsEvent.Set(); } catch { }
                    return false;
                }
                _showLogsWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                    _showLogsEvent!,
                    new WaitOrTimerCallback((state, timedOut) =>
                    {
                        try { TrayApplicationContext.RequestShowLogs(); } catch { }
                    }),
                    null,
                    Timeout.Infinite,
                    false);
                return true;
            }
            catch
            {
                return true;
            }
        }

        private static void CleanupSingleInstance()
        {
            try { _showLogsWaitHandle?.Unregister(null); } catch { }
            _showLogsWaitHandle = null;
            try { _showLogsEvent?.Dispose(); } catch { }
            _showLogsEvent = null;
            try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
            try { _singleInstanceMutex?.Dispose(); } catch { }
            _singleInstanceMutex = null;
        }
    }

}