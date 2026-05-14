using Windows.Storage.Streams;
using System.Net;
using Windows.Networking.Sockets;
namespace JulesClient.Services;

public class Socks5ProxyHandler : HttpMessageHandler
{
    private readonly string _host; private readonly int _port;
    private readonly string? _user; private readonly string? _pass;
    private readonly HttpClientHandler _inner = new();
    public Socks5ProxyHandler(string host, int port, string? user = null, string? pass = null) { _host = host; _port = port; _user = user; _pass = pass; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var socket = new StreamSocket();
        try
        {
            await socket.ConnectAsync(new Windows.Networking.HostName(_host), _port.ToString(), SocketProtectionLevel.SslAllowNullEncryption);
            await Handshake(socket, req.RequestUri!, ct);
            var bytes = await BuildRequestAsync(req);
            using var os = socket.OutputStream.AsStreamForWrite();
            await os.WriteAsync(bytes, 0, bytes.Length, ct); await os.FlushAsync(ct);
            return await ReadResponseAsync(socket.InputStream.AsStreamForRead(), ct);
        }
        finally { socket?.Dispose(); }
    }
    private async Task Handshake(StreamSocket s, Uri dest, CancellationToken ct)
    {
        var w = new DataWriter(s.OutputStream); var r = new DataReader(s.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
        w.WriteByte(5); w.WriteByte(!string.IsNullOrEmpty(_user) ? (byte)2 : (byte)1); w.WriteByte(0); await w.StoreAsync();
        await r.LoadAsync(2); var auth = r.ReadByte();
        if (auth == 2 && !string.IsNullOrEmpty(_user)) { w.WriteByte(1); w.WriteByte((byte)_user!.Length); foreach (var c in _user) w.WriteByte((byte)c); w.WriteByte((byte)_pass!.Length); foreach (var c in _pass) w.WriteByte((byte)c); await w.StoreAsync(); await r.LoadAsync(2); if (r.ReadByte() != 0) throw new UnauthorizedAccessException("Proxy auth failed"); }
        else if (auth != 0) throw new InvalidOperationException("SOCKS5 auth unsupported");
        w.WriteByte(5); w.WriteByte(1); w.WriteByte(0); w.WriteByte(3); var h = dest.Host; w.WriteByte((byte)h.Length); foreach (var c in h) w.WriteByte((byte)c); w.WriteUInt16((ushort)dest.Port); await w.StoreAsync();
        await r.LoadAsync(10); if (r.ReadByte() != 0) throw new InvalidOperationException("SOCKS5 CONNECT failed");
    }
    private async Task<byte[]> BuildRequestAsync(HttpRequestMessage req)
    {
        using var ms = new MemoryStream(); using var sw = new StreamWriter(ms, leaveOpen: true);
        await sw.WriteAsync($"{req.Method} {req.RequestUri?.PathAndQuery} HTTP/1.1\r\nHost: {req.RequestUri?.Host}\r\n");
        foreach (var h in req.Headers) await sw.WriteAsync($"{h.Key}: {string.Join(",", h.Value)}\r\n");
        if (req.Content != null) { var c = await req.Content.ReadAsStringAsync(); await sw.WriteAsync($"Content-Length: {c.Length}\r\n\r\n{c}"); } else await sw.WriteAsync("\r\n");
        await sw.FlushAsync(); return ms.ToArray();
    }
    private async Task<HttpResponseMessage> ReadResponseAsync(Stream st, CancellationToken ct)
    {
        using var sr = new StreamReader(st, leaveOpen: true); var sl = await sr.ReadLineAsync(); if (sl == null) throw new InvalidDataException("Empty");
        var p = sl.Split(' ', 3); var res = new HttpResponseMessage((HttpStatusCode)int.Parse(p[1])) { ReasonPhrase = p.Length > 2 ? p[2] : null };
        string? ln; while (!string.IsNullOrEmpty(ln = await sr.ReadLineAsync())) { var c = ln.IndexOf(':'); if (c > 0) res.Headers.TryAddWithoutValidation(ln[..c].Trim(), ln[(c + 1)..].Trim()); }
        if (res.Content.Headers.ContentLength.HasValue) { var len = (int)res.Content.Headers.ContentLength.Value; var buf = new byte[len]; int rd = 0; while (rd < len) { var b = await st.ReadAsync(buf, rd, len - rd, ct); if (b == 0) break; rd += b; } res.Content = new ByteArrayContent(buf); }
        return res;
    }
    protected override void Dispose(bool d) { if (d) _inner.Dispose(); base.Dispose(d); }
}
