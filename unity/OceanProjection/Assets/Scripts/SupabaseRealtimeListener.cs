using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class SupabaseRealtimeListener : IDisposable
{
    private const string Topic = "realtime:unity-fishes";
    private const string JoinReference = "1";
    private const int HeartbeatSeconds = 20;
    private const int MaximumMessageBytes = 1024 * 1024;

    private static readonly int[] ReconnectDelaysMilliseconds = { 1000, 2000, 5000, 10000 };

    private readonly Uri websocketUri;
    private readonly string joinMessage;
    private readonly Action<string> onDatabaseChanged;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly object socketLock = new object();
    private readonly object statusLock = new object();

    private ClientWebSocket activeSocket;
    private Task runTask;
    private string statusMessage = "Realtime is not started.";
    private int statusRevision;
    private int isSubscribed;
    private int messageReference = 1;
    private bool disposed;

    public SupabaseRealtimeListener(string supabaseUrl, string supabaseAnonKey, Action<string> onDatabaseChanged)
    {
        websocketUri = BuildWebsocketUri(supabaseUrl, supabaseAnonKey);
        joinMessage = SupabaseRealtimeProtocol.BuildJoinMessage(Topic, JoinReference);
        this.onDatabaseChanged = onDatabaseChanged;
    }

    public bool IsSubscribed => Volatile.Read(ref isSubscribed) == 1;
    public int StatusRevision => Volatile.Read(ref statusRevision);

    public string StatusMessage
    {
        get
        {
            lock (statusLock)
            {
                return statusMessage;
            }
        }
    }

    public void Start()
    {
        if (disposed || runTask != null)
        {
            return;
        }

        runTask = RunReconnectLoopAsync(cancellation.Token);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();

        lock (socketLock)
        {
            activeSocket?.Abort();
        }

        SetStatus(false, "Realtime stopped.");
        cancellation.Dispose();
    }

    private async Task RunReconnectLoopAsync(CancellationToken cancellationToken)
    {
        int reconnectAttempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            bool subscribedDuringSession = false;

            using (ClientWebSocket socket = new ClientWebSocket())
            {
                SetActiveSocket(socket);

                try
                {
                    SetStatus(false, "Realtime connecting...");
                    await socket.ConnectAsync(websocketUri, cancellationToken);
                    SetStatus(false, "Realtime socket connected; joining fishes channel...");
                    await SendTextAsync(socket, joinMessage, cancellationToken);

                    using (CancellationTokenSource sessionCancellation =
                           CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        Task heartbeatTask = HeartbeatLoopAsync(socket, sessionCancellation.Token);
                        subscribedDuringSession = await ReceiveLoopAsync(socket, sessionCancellation.Token);
                        sessionCancellation.Cancel();

                        try
                        {
                            await heartbeatTask;
                        }
                        catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested)
                        {
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    SetStatus(false, $"Realtime unavailable: {exception.Message}");
                }
                finally
                {
                    ClearActiveSocket(socket);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            reconnectAttempt = subscribedDuringSession ? 0 : reconnectAttempt + 1;
            int delayIndex = Math.Min(Math.Max(0, reconnectAttempt - 1), ReconnectDelaysMilliseconds.Length - 1);
            int delayMilliseconds = ReconnectDelaysMilliseconds[delayIndex];
            SetStatus(false, $"Realtime disconnected; retrying in {delayMilliseconds / 1000f:0.#} seconds.");

            try
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<bool> ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        bool subscribedDuringSession = false;

        using (MemoryStream messageBuffer = new MemoryStream())
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                messageBuffer.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        SetStatus(false, "Realtime server closed the connection.");
                        return subscribedDuringSession;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        continue;
                    }

                    messageBuffer.Write(buffer, 0, result.Count);
                    if (messageBuffer.Length > MaximumMessageBytes)
                    {
                        throw new InvalidDataException("Realtime message exceeded the 1 MB safety limit.");
                    }
                }
                while (!result.EndOfMessage);

                if (messageBuffer.Length == 0)
                {
                    continue;
                }

                string messageJson = Encoding.UTF8.GetString(
                    messageBuffer.GetBuffer(),
                    0,
                    (int)messageBuffer.Length
                );

                if (!HandleMessage(messageJson, ref subscribedDuringSession))
                {
                    return subscribedDuringSession;
                }
            }
        }

        return subscribedDuringSession;
    }

    private bool HandleMessage(string messageJson, ref bool subscribedDuringSession)
    {
        SupabaseRealtimeProtocol.Envelope message = SupabaseRealtimeProtocol.ParseEnvelope(messageJson);
        if (message == null || string.IsNullOrWhiteSpace(message.@event))
        {
            return true;
        }

        switch (message.@event)
        {
            case "phx_reply" when message.@ref == JoinReference:
                if (!string.Equals(message.payload?.status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    string reason = message.payload?.response?.reason;
                    SetStatus(false, $"Realtime join failed: {FirstNonEmpty(reason, "unknown reason")}");
                    return false;
                }

                if (message.payload?.response?.postgres_changes == null
                    || message.payload.response.postgres_changes.Length == 0)
                {
                    SetStatus(false, "Realtime joined without a fishes subscription. Enable the table publication.");
                    return false;
                }

                subscribedDuringSession = true;
                SetStatus(true, "Realtime subscribed to public.fishes.");
                return true;

            case "system":
                if (!string.Equals(message.payload?.extension, "postgres_changes", StringComparison.Ordinal))
                {
                    return true;
                }

                bool subscriptionReady = string.Equals(
                    message.payload?.status,
                    "ok",
                    StringComparison.OrdinalIgnoreCase
                );
                if (subscriptionReady)
                {
                    subscribedDuringSession = true;
                    SetStatus(true, "Realtime subscribed to public.fishes.");
                }
                else
                {
                    SetStatus(false, $"Realtime subscription degraded: {FirstNonEmpty(message.payload?.message, "unknown reason")}");
                }

                return true;

            case "postgres_changes":
                subscribedDuringSession = true;
                SetStatus(true, "Realtime subscribed to public.fishes.");
                onDatabaseChanged?.Invoke(messageJson);
                return true;

            case "phx_error":
            case "phx_close":
                SetStatus(false, $"Realtime channel ended ({message.@event}).");
                return false;

            default:
                return true;
        }
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                await Task.Delay(TimeSpan.FromSeconds(HeartbeatSeconds), cancellationToken);
                int reference = Interlocked.Increment(ref messageReference);
                string heartbeat =
                    $"{{\"topic\":\"phoenix\",\"event\":\"heartbeat\",\"payload\":{{}},\"ref\":\"{reference}\",\"join_ref\":null}}";
                await SendTextAsync(socket, heartbeat, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            socket.Abort();
        }
    }

    private static async Task SendTextAsync(
        ClientWebSocket socket,
        string message,
        CancellationToken cancellationToken
    )
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken
        );
    }

    private void SetStatus(bool subscribed, string message)
    {
        Interlocked.Exchange(ref isSubscribed, subscribed ? 1 : 0);

        lock (statusLock)
        {
            if (statusMessage == message)
            {
                return;
            }

            statusMessage = message;
            Interlocked.Increment(ref statusRevision);
        }
    }

    private void SetActiveSocket(ClientWebSocket socket)
    {
        lock (socketLock)
        {
            activeSocket = socket;
        }
    }

    private void ClearActiveSocket(ClientWebSocket socket)
    {
        lock (socketLock)
        {
            if (ReferenceEquals(activeSocket, socket))
            {
                activeSocket = null;
            }
        }
    }

    private static Uri BuildWebsocketUri(string supabaseUrl, string supabaseAnonKey)
    {
        if (!Uri.TryCreate(supabaseUrl, UriKind.Absolute, out Uri rootUri))
        {
            throw new ArgumentException("Supabase URL is not a valid absolute URL.", nameof(supabaseUrl));
        }

        UriBuilder builder = new UriBuilder(rootUri);
        if (string.Equals(builder.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            builder.Scheme = "wss";
        }
        else if (string.Equals(builder.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            builder.Scheme = "ws";
        }
        else
        {
            throw new ArgumentException("Supabase URL must use http or https.", nameof(supabaseUrl));
        }

        builder.Path = $"{builder.Path.TrimEnd('/')}/realtime/v1/websocket";
        builder.Query = $"apikey={Uri.EscapeDataString(supabaseAnonKey)}&vsn=1.0.0";
        return builder.Uri;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(values[index]))
            {
                return values[index].Trim();
            }
        }

        return "";
    }
}
