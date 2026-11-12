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
        private readonly byte[] key;
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

        /// <summary>The port of the direct link (VPN or home network): the instructor's PC runs its own small relay on it.</summary>
        public const int DirectPort = 18831;

        /// <summary>
        /// The direct link (the user, 10 Nov: a backup when the public relays are slow or blocked): null = the public relays;
        /// for the instructor "" = his own small relay on <see cref="DirectPort"/>; for the student the instructor's IP address.
        /// </summary>
        public string? Direct { get; }
        private MQTTnet.Server.MqttServer? server;

        /// <summary>An IPv4 address written in full (four numbers 0–255 with dots), as the student must type it.</summary>
        public static bool IsAddress(string text)
        {
            string[] parts = (text ?? "").Trim().Split('.');
            return parts.Length == 4 && parts.All(p => p.Length is > 0 and <= 3 && p.All(char.IsAsciiDigit) && int.Parse(p, System.Globalization.CultureInfo.InvariantCulture) <= 255);
        }

        /// <summary>The IPv4 addresses of this PC (a VPN first: Hamachi 25.x, Radmin 26.x, Tailscale 100.x), for the instructor to tell.</summary>
        public static List<string> LocalAddresses()
        {
            List<string> found = [];
            try
            {
                foreach (System.Net.NetworkInformation.NetworkInterface nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up || nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation address in nic.GetIPProperties().UnicastAddresses)
                        if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(address.Address))
                            found.Add(address.Address.ToString());
                }
            }
            catch (Exception)
            {
                // (No list: the instructor reads the address in his VPN program.)
            }
            static int Rank(string ip) => ip.StartsWith("25.", StringComparison.Ordinal) || ip.StartsWith("26.", StringComparison.Ordinal) || ip.StartsWith("100.", StringComparison.Ordinal) ? 0
                : ip.StartsWith("169.254.", StringComparison.Ordinal) ? 2 : 1;
            return found.Distinct().OrderBy(Rank).ToList();
        }

        public LessonLink(LessonRole role, string code, string? direct = null)
        {
            Role = role;
            Code = code;
            Direct = direct;
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes("TWR Trainer lesson " + code));
            channel = "atcroom/twr/lesson/v1/" + Convert.ToHexString(hash)[..20].ToLowerInvariant();
            // (The relays are public: every message encrypted with a key from the code, the review of 9 Nov; the map never readable by others.)
            key = SHA256.HashData(Encoding.UTF8.GetBytes("TWR Trainer lesson key " + code));
            Log($"Lesson: start as {role}");
            _ = Task.Run(Run);
        }

        private string OwnTopic => channel + (Role == LessonRole.Instructor ? "/i" : "/s");
        private string OtherTopic => channel + (Role == LessonRole.Instructor ? "/s" : "/i");

        public bool Connected => client?.IsConnected == true;
        /// <summary>Relay in use, or null while not connected.</summary>
        public string? RelayName => !Connected ? null : Direct != null ? "direct" : Brokers[brokerIndex].Host;

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
            // The instructor of a direct lesson: his own small relay, on all the networks of this PC (Windows asks once
            // to allow it in the firewall).
            if (Role == LessonRole.Instructor && Direct != null)
            {
                MQTTnet.Server.MqttServer? started = null;
                try
                {
                    MQTTnet.Server.MqttServerOptions options = new MQTTnet.Server.MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(DirectPort).Build();
                    started = factory.CreateMqttServer(options);
                    await started.StartAsync();
                    server = started;
                    if (stop.IsCancellationRequested && Interlocked.Exchange(ref server, null) is MQTTnet.Server.MqttServer late)
                    {
                        late.Dispose();
                        return;
                    }
                    Log($"Lesson: own relay on port {DirectPort}");
                }
                catch (Exception error)
                {
                    started?.Dispose();
                    DirectError = $"The direct link could not start on port {DirectPort} (another program uses it, or it is not allowed): stop the lesson and choose Internet relays.";
                    Log($"Lesson: own relay failed ({error.GetType().Name}: {error.Message})");
                }
            }
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    if (!Connected)
                    {
                        await Connect();
                    }
                    else if (Direct == null)
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
            // (The instructor's own relay did not start: nothing to connect to; the window says what to do.)
            if (Role == LessonRole.Instructor && DirectError != null) return;
            IMqttClient next = factory.CreateMqttClient();
            (string host, int port, string? webSocket) = Brokers[brokerIndex];
            bool direct = Direct != null;
            if (direct)
            {
                // (Direct: the instructor's own relay, no TLS: the messages are encrypted with the code anyway.)
                host = Role == LessonRole.Instructor || Direct!.Length == 0 ? "127.0.0.1" : Direct!;
                port = DirectPort;
                webSocket = null;
            }
            try
            {
                next.ApplicationMessageReceivedAsync += OnMessage;
                Log($"Lesson: connecting to {host}:{port} ({(webSocket != null ? "secure WebSocket" : "MQTT over TLS")})...");
                MqttClientOptionsBuilder builder = new MqttClientOptionsBuilder();
                builder = webSocket != null
                    ? builder.WithWebSocketServer(o => o.WithUri(webSocket))
                    : builder.WithTcpServer(host, port);
                if (!direct) builder = builder.WithTlsOptions(o => o.UseTls());
                MqttClientOptions options = builder
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
                // No network or relay not reachable: the loop tries again, with the next relay (direct: the same address).
                if (!direct) brokerIndex = (brokerIndex + 1) % Brokers.Length;
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
                        .WithPayload(Seal(payload))
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
                    if (Open(e.ApplicationMessage.PayloadSegment) is string text)
                    {
                        Interlocked.Exchange(ref lastHeardTicks, DateTime.UtcNow.Ticks);
                        Received?.Invoke(text);
                    }
                }
            }
            catch (Exception)
            {
                // A malformed message is ignored.
            }
            return Task.CompletedTask;
        }

        /// <summary>A message encrypted (AES-GCM): its nonce, its tag, then the text.</summary>
        private byte[] Seal(string payload)
        {
            byte[] plain = Encoding.UTF8.GetBytes(payload);
            byte[] sealedMessage = new byte[12 + 16 + plain.Length];
            Span<byte> nonce = sealedMessage.AsSpan(0, 12), tag = sealedMessage.AsSpan(12, 16), cipher = sealedMessage.AsSpan(28);
            RandomNumberGenerator.Fill(nonce);
            using AesGcm aes = new(key, 16);
            aes.Encrypt(nonce, plain, cipher, tag);
            return sealedMessage;
        }

        /// <summary>A message of the other side opened; null when it is not one of this lesson (another code, or damaged).</summary>
        private string? Open(ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count < 28) return null;
            ReadOnlySpan<byte> all = data.AsSpan();
            byte[] plain = new byte[all.Length - 28];
            try
            {
                using AesGcm aes = new(key, 16);
                aes.Decrypt(all[..12], all[28..], all[12..28], plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException)
            {
                return null;
            }
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

        /// <summary>The instructor's own relay could not start (the port taken, or not allowed), or null.</summary>
        public string? DirectError { get; private set; }

        public void Dispose()
        {
            stop.Cancel();
            Log("Lesson: stop");
            try
            {
                // On a pool thread: waiting on the UI thread for a task that resumes there would hang.
                Task.Run(async () =>
                {
                    await Disconnect();
                    MQTTnet.Server.MqttServer? own = Interlocked.Exchange(ref server, null);
                    if (own != null)
                    {
                        // (Dispose stops it: StopAsync wants options in this version of MQTTnet.)
                        try { own.Dispose(); } catch (Exception) { }
                    }
                }).Wait(TimeSpan.FromSeconds(3));
            }
            catch (Exception)
            {
            }
        }
    }
}
