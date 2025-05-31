using System.Net.WebSockets;
using System.Text.Json;
using Serilog;
using Websocket.Client;

namespace Logic;

public class ProPresenterWebsocketWatcher
{
    private string _port;
    private string _password;

    public ProPresenterWebsocketWatcher(string port, string password)
    {
        _port = port;
        _password = password;
    }

    public event EventHandler<MsgRecvdArgs> OnMsgRecvd;

    public void Start()
    {
        var exitEvent = new ManualResetEvent(false);
        var url = new Uri($"ws://localhost:{_port}/remote");

        using var client = new WebsocketClient(url);
        
        client.ReconnectTimeout = null; 
        client.ErrorReconnectTimeout = TimeSpan.FromSeconds(30);
        client.LostReconnectTimeout = null;
        
        client.DisconnectionHappened.Subscribe(info =>
        {
            Log.Information($"DisconnectionHappened, type: {info.Type}");
        });
        client.ReconnectionHappened.Subscribe(info =>
        {
            Log.Information($"ReconnectionHappened, type: {info.Type}");
            SendAuth(client);
        });
        client.MessageReceived.Subscribe(msg =>
        {
            Log.Information($"Message received: {msg}");
            OnMsgRecvd?.Invoke(this, new MsgRecvdArgs { msg = msg.Text });
        });
        client.Start();

        exitEvent.WaitOne();
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
        public string msg { get; set; }
    }
}