using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ATCRoom.TwrTrainer
{
    /// <summary>An aircraft of the network (IVAO) as Aurora gives it: track (°), altitude (ft), speed (kt), place, squawk.</summary>
    internal sealed record IvaoAircraft(string Callsign, double Track, double AltitudeFt, double SpeedKt, double Latitude, double Longitude, string? Squawk);

    /// <summary>
    /// The link with Aurora on this PC (the user, 7 Nov; the code of AuroraPAR v2, Aurora.cs, copied): the traffic of the
    /// network, the callsign Aurora is connected with, the METAR. Only read: nothing of the simulation goes to the network.
    /// </summary>
    internal sealed class Aurora
    {
        /// <summary>The longest wait for an answer of Aurora before the connection counts as lost.</summary>
        private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(2);
        /// <summary>Unanswered requests in a row after which the connection is opened again.</summary>
        private const int MaxConsecutiveTimeouts = 3;

        private TcpClient? client;
        private NetworkStream? stream;
        private StreamReader? reader;
        private StreamWriter? writer;
        private readonly SemaphoreSlim semaphore = new(1, 1);
        private volatile bool connected;
        private volatile bool closed;
        private int consecutiveTimeouts;

        public bool Connected => connected;

        /// <summary>One attempt to connect (Aurora's port 1130 on this PC); called again while not connected.</summary>
        public async Task<bool> TryConnect()
        {
            if (connected) return true;
            if (closed) return false;
            await semaphore.WaitAsync();
            try
            {
                Disconnect();
                consecutiveTimeouts = 0;
                client = new TcpClient();
                using CancellationTokenSource cts = new(ResponseTimeout);
                await client.ConnectAsync(IPAddress.Loopback, 1130, cts.Token);
                stream = client.GetStream();
                reader = new(stream);
                writer = new(stream);
                connected = true;
            }
            catch (Exception)
            {
                // Aurora not running, or not accepting connections yet.
                Disconnect();
            }
            finally
            {
                semaphore.Release();
            }
            return connected;
        }

        /// <summary>
        /// One command and its one-line answer (Aurora repeats the command at the start of its answer; lines that are not
        /// the answer, a late one of an earlier request, are skipped). Null: no answer in time, or the connection lost.
        /// </summary>
        private async Task<string?> Request(string command, Func<string, bool> isAnswer)
        {
            if (!connected) return null;
            await semaphore.WaitAsync();
            try
            {
                if (!connected || writer == null || reader == null) return null;
                using CancellationTokenSource cts = new(ResponseTimeout);
                await writer.WriteLineAsync(command.AsMemory(), cts.Token);
                await writer.FlushAsync(cts.Token);
                while (true)
                {
                    string? message = await reader.ReadLineAsync(cts.Token);
                    if (message == null)
                    {
                        Disconnect();
                        return null;
                    }
                    if (isAnswer(message))
                    {
                        consecutiveTimeouts = 0;
                        return message;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (++consecutiveTimeouts >= MaxConsecutiveTimeouts) Disconnect();
                return null;
            }
            catch (Exception)
            {
                Disconnect();
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static bool StartsWithField(string message, string prefix) =>
            message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (message.Length == prefix.Length || message[prefix.Length] == ';');

        private static string? ParseSquawk(string[] data, int index)
        {
            if (index >= data.Length) return null;
            string code = data[index].Trim();
            return code.Length != 4 || code == "0000" || code.Any(c => c < '0' || c > '7') ? null : code;
        }

        private static bool TryParseNumber(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>The callsigns of the traffic Aurora sees.</summary>
        public async Task<string[]> GetTrafficList()
        {
            string? message = await Request("#TR", m => StartsWithField(m, "#TR"));
            return message != null ? message.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[1..] : [];
        }

        /// <summary>Where an aircraft of the network is now, or null.</summary>
        public async Task<IvaoAircraft?> GetTrafficPosition(string callsign)
        {
            if (string.IsNullOrEmpty(callsign)) return null;
            string? message = await Request($"#TRPOS;{callsign}", m => StartsWithField(m, $"#TRPOS;{callsign}"));
            if (message == null) return null;
            string[] data = message.Split(';');
            if (data.Length >= 8 && TryParseNumber(data[3], out double track) && TryParseNumber(data[4], out double altitude) && TryParseNumber(data[5], out double speed)
                && TryParseNumber(data[6], out double latitude) && TryParseNumber(data[7], out double longitude))
                return new IvaoAircraft(callsign, track, altitude, speed, latitude, longitude, ParseSquawk(data, 8) ?? ParseSquawk(data, 9));
            return null;
        }

        /// <summary>The callsign Aurora is connected to the network with (LIPI_TWR), or null: not connected, no answer.</summary>
        public async Task<string?> GetConnectedCallsign()
        {
            string? message = await Request("#CONN", m => StartsWithField(m, "#CONN") || StartsWithField(m, "#CTRL")
                || m.StartsWith('$') || m.StartsWith("@ERR", StringComparison.OrdinalIgnoreCase));
            if (message == null || !message.StartsWith('#')) return null;
            string[] fields = message.Split(';', StringSplitOptions.TrimEntries);
            return fields.Length >= 2 && fields[1].Length > 0 ? fields[1].ToUpperInvariant() : null;
        }

        /// <summary>The METAR of an aerodrome from Aurora (the line without the command), or null.</summary>
        public async Task<string?> GetMetar(string icao)
        {
            string? message = await Request($"#METAR;{icao}", m => StartsWithField(m, "#METAR"));
            if (message == null) return null;
            string[] fields = message.Split(';', 3, StringSplitOptions.TrimEntries);
            // (#METAR;LIPI;LIPI 071250Z ... or #METAR;LIPI 071250Z ...: the part that starts with the code.)
            string text = fields.Length >= 3 && fields[1].Equals(icao, StringComparison.OrdinalIgnoreCase) ? fields[2] : fields.Length >= 2 ? string.Join(' ', fields[1..]) : "";
            text = text.Replace(';', ' ').Trim();
            return text.Length >= 8 && text.Contains(' ') ? text : null;
        }

        private void Disconnect()
        {
            connected = false;
            try { writer?.Dispose(); } catch (Exception) { }
            try { reader?.Dispose(); } catch (Exception) { }
            try { stream?.Dispose(); } catch (Exception) { }
            try { client?.Dispose(); } catch (Exception) { }
            writer = null;
            reader = null;
            stream = null;
            client = null;
        }

        /// <summary>The connection let go for now (connected again by the next <see cref="TryConnect"/>).</summary>
        public void Release() => Disconnect();

        public void Close()
        {
            closed = true;
            Disconnect();
        }
    }
}
