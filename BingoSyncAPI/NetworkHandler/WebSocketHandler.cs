using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BingoSyncAPI.NetworkHandler
{
    internal class WebSocketHandler
    {
        public WebSocketHandler(Uri URI, string socketKey, MessageReceived messageReceiver)
        {
            this.URI                = URI;
            this.SocketKey          = socketKey;
            this.messageReceiver    = messageReceiver;

            OnMessageReceived += messageReceiver;
        }

        public Uri      URI         { get; private set; }
        public string   SocketKey   { get; private set; }

        private MessageReceived messageReceiver;

        public event            MessageReceived OnMessageReceived;
        public delegate void    MessageReceived(string message);

        private const int MaxAttempts = 3;

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
        public enum ConnectionStatus
        {
            Connected,
            Connecting,
            Disconnected,
            Disconnecting
        }

        private ClientWebSocket socket;

        public async Task<ConnectionStatus> StartSocket()
        {
            if (Status != ConnectionStatus.Connected && Status != ConnectionStatus.Connecting && Status != ConnectionStatus.Disconnecting)
            {
                Status = ConnectionStatus.Connecting;
                OpenSocket();
                return await WaitForConnection();
            }
            return Status;
        }

        private async Task<ConnectionStatus> WaitForConnection()
        {
            while (Status == ConnectionStatus.Connecting) await Task.Yield();
            return Status;
        }

        private async void OpenSocket()
        {
            byte[] socketData = Encoding.ASCII.GetBytes(SocketKey);

            using (var socket = new ClientWebSocket())
            {
                await socket.ConnectAsync(URI, CancellationToken.None);

                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await socket.SendAsync(new ArraySegment<byte>(socketData), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        Status = ConnectionStatus.Disconnected;
                    }

                    if (Status == ConnectionStatus.Connecting)
                    {
                        this.socket = socket;
                        await StartListeningToSocket();
                    }
                }
            }

            OnMessageReceived -= this.messageReceiver;
            Status = ConnectionStatus.Disconnected;
        }

        private async Task StartListeningToSocket()
        {
            Status = ConnectionStatus.Connected;
            await ReceiveMessages();
        }

        private async Task ReceiveMessages()
        {
            byte[] buffer = new byte[1024];

            int attempt = 0;

            while (socket.State == WebSocketState.Open && Status == ConnectionStatus.Connected && attempt < MaxAttempts)
            {
                WebSocketReceiveResult result = null;

                attempt++;

                try
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    attempt = 0;
                }
                catch
                {
                    if (attempt >= MaxAttempts)
                    {
                        await CloseSocket();
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Close && Status == ConnectionStatus.Connected)
                {
                    await CloseSocket();
                    break;
                }
                else if (result != null)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    if (message != null && message != string.Empty)
                        OnMessageReceived?.Invoke(message);
                }
            }
        }

        public async Task CloseSocket()
        {
            if (socket != null && socket.State == WebSocketState.Open && Status == ConnectionStatus.Connected)
            {
                Status = ConnectionStatus.Disconnecting;

                WebSocketCloseStatus    closeStatus = socket.CloseStatus ?? WebSocketCloseStatus.NormalClosure;
                string                  closeReason = socket.CloseStatusDescription;

                try
                {
                    await socket.CloseOutputAsync   (closeStatus, closeReason, CancellationToken.None);
                    await socket.CloseAsync         (closeStatus, closeReason, CancellationToken.None);
                } catch { }

                Status = ConnectionStatus.Disconnected;
                socket = null;

                OnMessageReceived?.Invoke("Socket Closed");
            }
        }
    }
}
