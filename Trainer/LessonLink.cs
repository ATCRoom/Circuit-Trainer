using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace ATCRoom.TwrTrainer
{
    /// <summary>Side of a lesson between two PCs: the instructor (the simulation) or the student (the student console).</summary>
    internal enum LessonRole
    {
        Instructor,
        Student
    }

    /// <summary>
    /// TWR Trainer (copied from AuroraPAR v2 Trainer, the user 9 Nov): the lesson link between the instructor's PC and the student's PC, through the public MQTT
    /// relays of the coordination panel (encrypted connection, nothing to install, no port to open). The channel is
    /// computed from the lesson code (it cannot be found without it). Each side sends one small message about every
    /// second (the instructor every half second); nothing is kept on the relay, so a student who joins later sees only
    /// what is sent from then on. The other side is "present" while its messages arrive.
    /// </summary>
    internal sealed class LessonLink : IDisposable
    {
        /// <summary>The other side is present while its last message is younger than this (seconds).</summary>
        public const double PresentSeconds = 5;

        /// <summary>The public relays (as AuroraPAR v2): MQTT over TLS, or secure WebSocket where a network allows only that.</summary>
        private static readonly (string Host, int Port, string? WebSocket)[] Brokers =
        [
            ("broker.emqx.io", 8883, null),
            ("broker.emqx.io", 8084, "wss://broker.emqx.io:8084/mqtt"),
            ("broker.hivemq.com", 8883, null),
            ("broker.hivemq.com", 8884, "wss://broker.hivemq.com:8884/mqtt")
        ];

        public LessonRole Role { get; }
        public string Code { get; }
        /// <summary>A random name of this run of the link (the numbers of the "forget" start again with it).</summary>
        public string Session { get; } = Guid.NewGuid().ToString("N")[..8];

        private readonly string channel;
        private readonly string clientId = "twrtrainer-lesson-" + Guid.NewGuid().ToString("N")[..12];
        private readonly MqttFactory factory = new();
        private volatile IMqttClient? client;
        private volatile int brokerIndex;
        private readonly CancellationTokenSource stop = new();
        private DateTime connectedAt = DateTime.MinValue;
        /// <summary>Time (UTC ticks, of this PC) of the last message of the other side; 0: never.</summary>
        private long lastHeardTicks;
        /// <summary>1 while a message is being sent (one at a time: a new one is dropped meanwhile).</summary>
        private int sending;

        /// <summary>A message of the other side (raised on a background thread).</summary>
        public event Action<string>? Received;

        public LessonLink(LessonRole role, string code)
        {
            Role = role;
            Code = code;
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes("TWR Trainer lesson " + code));
            channel = "atcroom/twr/lesson/v1/" + Convert.ToHexString(hash)[..20].ToLowerInvariant();
            Log($"Lesson: start as {role}");
            _ = Task.Run(Run);
        }

        private string OwnTopic => channel + (Role == LessonRole.Instructor ? "/i" : "/s");
        private string OtherTopic => channel + (Role == LessonRole.Instructor ? "/s" : "/i");

        public bool Connected => client?.IsConnected == true;
        /// <summary>Relay in use, or null while not connected.</summary>
        public string? RelayName => Connected ? Brokers[brokerIndex].Host : null;

        /// <summary>The other side has been heard at least once.</summary>
        public bool EverHeard => Interlocked.Read(ref lastHeardTicks) != 0;

        /// <summary>The other side is linked now (a message less than <see cref="PresentSeconds"/> ago).</summary>
        public bool OtherPresent
        {
            get
            {
                long ticks = Interlocked.Read(ref lastHeardTicks);
                return ticks != 0 && (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds < PresentSeconds;
            }
        }

        /// <summary>The first relay of the list with another name than the first one (the "other relay").</summary>
        private static int OtherRelayIndex
        {
            get
            {
                for (int i = 1; i < Brokers.Length; i++) if (Brokers[i].Host != Brokers[0].Host) return i;
                return 0;
            }
        }

        /// <summary>Connects, reconnects after a network problem, and changes relay when the other side is not heard.</summary>
        private async Task Run()
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    if (!Connected)
                    {
                        await Connect();
                    }
                    else
                    {
                        // Each side uses the first relay that works on its own network: the two may end up on two relays,
                        // both connected but deaf to each other. The student then tries the other relay soon, the
                        // instructor much later (so they meet, instead of changing relay together).
                        double alone = Role == LessonRole.Student ? 15 : 60;
                        long ticks = Interlocked.Read(ref lastHeardTicks);
                        DateTime heard = ticks == 0 ? DateTime.MinValue : new DateTime(ticks, DateTimeKind.Utc);
                        DateTime since = heard > connectedAt ? heard : connectedAt;
                        if ((DateTime.UtcNow - since).TotalSeconds > alone)
                        {
                            Log($"Lesson: the other side is not heard on {Brokers[brokerIndex].Host}, trying the other relay");
                            await Disconnect();
                            brokerIndex = Brokers[brokerIndex].Host == Brokers[0].Host ? OtherRelayIndex : 0;
                        }
                    }
                }
                catch (Exception)
                {
                    // Never stop the loop.
                }
                try
                {
                    await Task.Delay(Connected ? 1000 : 2000, stop.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task Connect()
        {
            IMqttClient next = factory.CreateMqttClient();
            (string host, int port, string? webSocket) = Brokers[brokerIndex];
            try
            {
                next.ApplicationMessageReceivedAsync += OnMessage;
                Log($"Lesson: connecting to {host}:{port} ({(webSocket != null ? "secure WebSocket" : "MQTT over TLS")})...");
                MqttClientOptionsBuilder builder = new MqttClientOptionsBuilder();
                builder = webSocket != null
                    ? builder.WithWebSocketServer(o => o.WithUri(webSocket))
                    : builder.WithTcpServer(host, port);
                MqttClientOptions options = builder
                    .WithTlsOptions(o => o.UseTls())
                    .WithClientId(clientId)
                    .WithCleanSession()
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(20))
                    .Build();
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await next.ConnectAsync(options, timeout.Token);
                MqttClientSubscribeOptions subscribe = factory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => f.WithTopic(OtherTopic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce))
                    .Build();
                await next.SubscribeAsync(subscribe, timeout.Token);
                if (stop.IsCancellationRequested)
                {
                    next.ApplicationMessageReceivedAsync -= OnMessage;
                    next.Dispose();
                    return;
                }
                IMqttClient? old = client;
                client = next;
                if (old != null)
                {
                    old.ApplicationMessageReceivedAsync -= OnMessage;
                    old.Dispose();
                }
                connectedAt = DateTime.UtcNow;
                Log($"Lesson: connected to {host}:{port} as {Role}");
            }
            catch (Exception error)
            {
                Log($"Lesson: failed ({error.GetType().Name}: {error.Message})");
                next.ApplicationMessageReceivedAsync -= OnMessage;
                next.Dispose();
                // No network or relay not reachable: the loop tries again, with the next relay.
                brokerIndex = (brokerIndex + 1) % Brokers.Length;
            }
        }

        private async Task Disconnect()
        {
            IMqttClient? old = Interlocked.Exchange(ref client, null);
            if (old == null) return;
            try
            {
                if (old.IsConnected) await old.DisconnectAsync();
            }
            catch (Exception)
            {
            }
            old.ApplicationMessageReceivedAsync -= OnMessage;
            old.Dispose();
        }

        /// <summary>Sends a message to the other side (without waiting; dropped if the previous one is still on its way).</summary>
        public void Send(string payload)
        {
            IMqttClient? current = client;
            if (current?.IsConnected != true) return;
            if (Interlocked.Exchange(ref sending, 1) == 1) return;
            _ = Task.Run(async () =>
            {
                try
                {
                    MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                        .WithTopic(OwnTopic)
                        .WithPayload(payload)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                        .Build();
                    using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    await current.PublishAsync(message, timeout.Token);
                }
                catch (Exception)
                {
                    // Lost: the next one follows in a second.
                }
                finally
                {
                    Interlocked.Exchange(ref sending, 0);
                }
            });
        }

        private Task OnMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                if (e.ApplicationMessage.Topic == OtherTopic)
                {
                    Interlocked.Exchange(ref lastHeardTicks, DateTime.UtcNow.Ticks);
                    Received?.Invoke(Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
                }
            }
            catch (Exception)
            {
                // A malformed message is ignored.
            }
            return Task.CompletedTask;
        }

        /// <summary>What the link does, in Logs\Link.log (kept small): to understand a problem of the network.</summary>
        private static readonly object logLock = new();
        private static void Log(string text)
        {
            try
            {
                lock (logLock)
                {
                    string path = System.IO.Path.Combine(Paths.LogsFolder, "Link.log");
                    System.IO.Directory.CreateDirectory(Paths.LogsFolder);
                    if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 200_000) System.IO.File.Move(path, path + ".old", overwrite: true);
                    System.IO.File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {text}{Environment.NewLine}");
                }
            }
            catch (Exception)
            {
                // (No log: the link goes on.)
            }
        }

        public void Dispose()
        {
            stop.Cancel();
            Log("Lesson: stop");
            try
            {
                // On a pool thread: waiting on the UI thread for a task that resumes there would hang.
                Task.Run(Disconnect).Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
            }
        }
    }
}
