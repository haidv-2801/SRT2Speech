
using Microsoft.AspNetCore.SignalR.Client;

namespace SRT2Speech.Socket.Client
{
    public class MessageClient
    {
        private readonly string URL;
        private readonly string CHANNEL;
        private HubConnection _connection;
        private HubConnectionBuilder _connectionBuilder;

        public MessageClient(string listenUrl, string channel)
        {
            URL = listenUrl;
            CHANNEL = channel;
        }

        public async Task CreateConncetion<T>(Action<T> callback)
        {
            _connectionBuilder = new HubConnectionBuilder();

            _connection = _connectionBuilder.WithUrl(URL)
                .WithAutomaticReconnect()
                .Build();

            _connection.Reconnecting += async (error) =>
            {
                await Task.CompletedTask;
            };
            _connection.Reconnected += async (connectionId) =>
            {
                await Task.CompletedTask;
            };
            _connection.Closed += OnClosed;

            if (_connection.State == HubConnectionState.Disconnected)
            {
                _connection.On<T>(CHANNEL, callback);
                await _connection.StartAsync();
            }
        }

        public async Task OnClosed(Exception? error)
        {
            await Task.Delay(new Random().Next(0, 5) * 1000);
            await _connection.StartAsync();
        }
    }

}
