using System.Text.Json;
using Serilog;
using Websocket.Client;

namespace Logic;

public class ProPresenterWebsocketWatcher
{
    private readonly string _port;
    private readonly string _password;
    private WebsocketClient? _client;

    public ProPresenterWebsocketWatcher(string port, string password)
    {
        _port = port;
        _password = password;
    }

    public event EventHandler<MsgRecvdArgs>? OnMsgRecvd;
    public event EventHandler<bool>? ConnectionStateChanged; // true = connected, false = disconnected

    // Start the websocket client in a non-blocking way and keep it alive via a field.
    public void Start()
    {
        if (_client != null)
        {
            // Already started
            return;
        }

        var url = new Uri($"ws://localhost:{_port}/remote");
        _client = new WebsocketClient(url)
        {
            ReconnectTimeout = null,
            ErrorReconnectTimeout = TimeSpan.FromSeconds(30),
            LostReconnectTimeout = null
        };

        // On start we assume disconnected until proven connected
        try { ConnectionStateChanged?.Invoke(this, false); } catch { }

        _client.DisconnectionHappened.Subscribe(info =>
        {
            Log.Information($"DisconnectionHappened, type: {info.Type}, info: {info.Exception.Message}");
            try { ConnectionStateChanged?.Invoke(this, false); } catch { }
        });
        _client.ReconnectionHappened.Subscribe(info =>
        {
            Log.Information($"ReconnectionHappened, type: {info.Type}");
            try { ConnectionStateChanged?.Invoke(this, true); } catch { }
            if (_client != null)
                SendAuth(_client);
        });
        _client.MessageReceived.Subscribe(msg =>
        {
            Log.Information($"Message received: {msg}");
            OnMsgRecvd?.Invoke(this, new MsgRecvdArgs { msg = msg.Text });
        });

        _client.Start();
    }

    public void Stop()
    {
        try
        {
            _client?.Dispose();
        }
        catch
        {
            // ignore dispose errors
        }
        finally
        {
            _client = null;
            try { ConnectionStateChanged?.Invoke(this, false); } catch { }
        }
    }

    private void SendAuth(WebsocketClient client)
    {
        var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("action", "authenticate");
        writer.WriteNumber("protocol", 701);
        writer.WriteString("password", _password);
        writer.WriteEndObject();

        writer.Flush();

        client.Send(JSONUtil.StreamToString(stream));
    }

    public class MsgRecvdArgs : EventArgs
    {
        public string msg { get; set; } = string.Empty;
    }
}