using System.Net;
using System.Net.Sockets;
using System.Text;
using GpaWindows.Models;

namespace GpaWindows.Services;

public sealed class DnsFilterService : IDisposable
{
    private readonly HashSet<string> _allowedDomains;
    private readonly IPAddress _upstreamDns;
    private UdpClient? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public DnsFilterService(PolicyConfig config)
    {
        _allowedDomains = config.AllowedDomains
            .Select(NormalizeDomain)
            .Where(d => d.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!IPAddress.TryParse(config.UpstreamDns, out _upstreamDns!))
            throw new ArgumentException("DNS upstream inválido.");
    }

    public void Start()
    {
        if (_listener is not null)
            return;

        _cts = new CancellationTokenSource();
        _listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 53));
        _loop = Task.Run(() => ListenAsync(_cts.Token));
    }

    public static bool IsPortAvailable(int port = 53)
    {
        using var listener = new UdpClient();
        try
        {
            listener.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static string NormalizeDomainForPolicy(string domain) => NormalizeDomain(domain);

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Close();
            _listener?.Dispose();
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
        finally
        {
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            _loop = null;
        }
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult request;

            try
            {
                request = await _listener!.ReceiveAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                await Task.Delay(250, token);
                continue;
            }

            _ = Task.Run(() => HandleRequestAsync(request, token), token);
        }
    }

    private async Task HandleRequestAsync(UdpReceiveResult request, CancellationToken token)
    {
        try
        {
            var domain = TryReadQuestionDomain(request.Buffer);

            byte[] response;
            if (domain is not null && IsAllowed(domain))
                response = await ForwardToUpstreamAsync(request.Buffer, token);
            else
                response = BuildNxDomainResponse(request.Buffer);

            if (!token.IsCancellationRequested && _listener is not null)
                await _listener.SendAsync(response, response.Length, request.RemoteEndPoint);
        }
        catch
        {
            // O cliente fará nova tentativa; o serviço permanece ativo.
        }
    }

    private bool IsAllowed(string domain)
    {
        var normalized = NormalizeDomain(domain);

        foreach (var allowed in _allowedDomains)
        {
            if (normalized.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<byte[]> ForwardToUpstreamAsync(byte[] request, CancellationToken token)
    {
        using var upstream = new UdpClient(AddressFamily.InterNetwork);
        upstream.Connect(_upstreamDns, 53);

        await upstream.SendAsync(request, request.Length);

        var receiveTask = upstream.ReceiveAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(4), token);
        var completed = await Task.WhenAny(receiveTask, timeoutTask);

        if (completed != receiveTask)
            return BuildServerFailureResponse(request);

        return (await receiveTask).Buffer;
    }

    private static string? TryReadQuestionDomain(byte[] packet)
    {
        if (packet.Length < 13)
            return null;

        var labels = new List<string>();
        var offset = 12;

        while (offset < packet.Length)
        {
            var length = packet[offset++];

            if (length == 0)
                break;

            if ((length & 0xC0) != 0 || offset + length > packet.Length)
                return null;

            labels.Add(Encoding.ASCII.GetString(packet, offset, length));
            offset += length;
        }

        return labels.Count == 0 ? null : string.Join(".", labels);
    }

    private static byte[] BuildNxDomainResponse(byte[] request) =>
        BuildErrorResponse(request, rcode: 3);

    private static byte[] BuildServerFailureResponse(byte[] request) =>
        BuildErrorResponse(request, rcode: 2);

    private static byte[] BuildErrorResponse(byte[] request, int rcode)
    {
        var response = request.ToArray();

        if (response.Length < 12)
            return response;

        response[2] = (byte)(response[2] | 0x80);
        response[3] = (byte)((response[3] & 0xF0) | (rcode & 0x0F));

        // Answer, Authority e Additional = 0. Mantém a seção Question.
        for (var i = 6; i <= 11; i++)
            response[i] = 0;

        return response;
    }

    private static string NormalizeDomain(string domain) =>
        domain.Trim().TrimEnd('.').ToLowerInvariant();
}
