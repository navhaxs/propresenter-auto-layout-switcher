using Serilog;
using Websocket.Client;

namespace Logic;

public class ProPresenterWebsocketWatcher
{
    public event EventHandler<MyEventArgs> OnMsgRecvd;

    public void Start()
    {
        var exitEvent = new ManualResetEvent(false);
        var url = new Uri("ws://localhost:61727/remote");

        using var client = new WebsocketClient(url);
        client.ReconnectTimeout = TimeSpan.FromSeconds(30);
        client.ReconnectionHappened.Subscribe(info =>
            Log.Information($"Reconnection happened, type: {info.Type}"));

        client.MessageReceived.Subscribe(msg =>
        {
            Log.Information($"Message received: {msg}");
            OnMsgRecvd?.Invoke(this,new MyEventArgs { msg = msg.Text });
        });
        client.Start();
            
        Task.Run(() => client.Send(
            "{action:\"authenticate\",protocol:701,password:'control'}"));

        exitEvent.WaitOne();
    }
    
    public class MyEventArgs : EventArgs
    {
        public string msg { get; set; }
    }

    
}