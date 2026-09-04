using System.IO;
using JulesClient.Services;

namespace JulesClient.Tests;

public class Socks5HandshakerTests
{
    private class ScriptedStream : Stream
    {
        private readonly MemoryStream _written = new();
        private readonly MemoryStream _toRead = new();

        public byte[] GetWrittenBytes() => _written.ToArray();

        public void QueueReadResponse(byte[] data)
        {
            var currentPos = _toRead.Position;
            _toRead.Seek(0, SeekOrigin.End);
            _toRead.Write(data, 0, data.Length);
            _toRead.Seek(currentPos, SeekOrigin.Begin);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            _toRead.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _toRead.ReadAsync(buffer, offset, count, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) =>
            _written.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _written.WriteAsync(buffer, offset, count, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
    }

    [Fact]
    public async Task HandshakeAsync_NoAuth_IPv4Connect_Success()
    {
        var stream = new ScriptedStream();
        // Server response 1: Greeting reply [5, 0] (SOCKS5, No Auth)
        stream.QueueReadResponse(new byte[] { 5, 0 });
        // Server response 2: Connect reply [5, 0, 0, 1] (SOCKS5, Success, Reserved, IPv4) + 6 bytes bound address/port
        stream.QueueReadResponse(new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 4, 210 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Socks5Handshaker.HandshakeAsync(stream, "127.0.0.1", 8080, username: null, password: null, cts.Token);

        byte[] written = stream.GetWrittenBytes();
        Assert.NotEmpty(written);
        Assert.Equal(5, written[0]); // SOCKS version 5
    }

    [Fact]
    public async Task HandshakeAsync_UsernamePasswordAuth_DomainConnect_Success()
    {
        var stream = new ScriptedStream();
        // Server response 1: Greeting reply [5, 2] (SOCKS5, Auth Method 2)
        stream.QueueReadResponse(new byte[] { 5, 2 });
        // Server response 2: Auth subnegotiation reply [1, 0] (Ver 1, Success)
        stream.QueueReadResponse(new byte[] { 1, 0 });
        // Server response 3: Connect reply [5, 0, 0, 3] (Domain name) + length 9 ("localhost") + port 2 bytes
        stream.QueueReadResponse(new byte[] { 5, 0, 0, 3, 9, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', (byte)'h', (byte)'o', (byte)'s', (byte)'t', 0, 80 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Socks5Handshaker.HandshakeAsync(stream, "example.com", 80, "user", "pass", cts.Token);

        byte[] written = stream.GetWrittenBytes();
        Assert.NotEmpty(written);
    }

    [Fact]
    public async Task HandshakeAsync_IPv6Connect_Success()
    {
        var stream = new ScriptedStream();
        stream.QueueReadResponse(new byte[] { 5, 0 });
        // Connect reply [5, 0, 0, 4] (IPv6) + 18 bytes bound address/port
        byte[] connectReply = new byte[22];
        connectReply[0] = 5;
        connectReply[1] = 0;
        connectReply[2] = 0;
        connectReply[3] = 4;
        stream.QueueReadResponse(connectReply);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Socks5Handshaker.HandshakeAsync(stream, "::1", 443, null, null, cts.Token);
    }

    [Fact]
    public async Task HandshakeAsync_InvalidSocksVersion_ThrowsException()
    {
        var stream = new ScriptedStream();
        stream.QueueReadResponse(new byte[] { 4, 0 }); // Invalid version 4

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            Socks5Handshaker.HandshakeAsync(stream, "127.0.0.1", 80, null, null, cts.Token));

        Assert.Contains("Invalid SOCKS version", ex.Message);
    }

    [Fact]
    public async Task HandshakeAsync_AuthFailure_ThrowsException()
    {
        var stream = new ScriptedStream();
        stream.QueueReadResponse(new byte[] { 5, 2 });
        stream.QueueReadResponse(new byte[] { 1, 1 }); // Auth failed (code 1)

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            Socks5Handshaker.HandshakeAsync(stream, "127.0.0.1", 80, "user", "wrongpass", cts.Token));

        Assert.Contains("authentication failed", ex.Message);
    }

    [Fact]
    public async Task HandshakeAsync_ConnectError_ThrowsDescriptiveException()
    {
        var stream = new ScriptedStream();
        stream.QueueReadResponse(new byte[] { 5, 0 });
        stream.QueueReadResponse(new byte[] { 5, 5, 0, 1 }); // Connect failed (code 5 = connection refused)

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            Socks5Handshaker.HandshakeAsync(stream, "127.0.0.1", 80, null, null, cts.Token));

        Assert.Contains("connection refused", ex.Message);
    }
}
