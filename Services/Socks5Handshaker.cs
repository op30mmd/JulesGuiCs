using System.Net.Sockets;
using System.Text;

namespace JulesClient.Services;

public static class Socks5Handshaker
{
    public static async Task HandshakeAsync(Stream stream, string targetHost, int targetPort, string? username, string? password, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var token = cts.Token;

        try
        {
            // 1. Greeting
            // Ver: 5, NMethods: 1 or 2, Methods: 0 (No auth) [, 2 (User/Pass)]
            bool useAuth = !string.IsNullOrEmpty(username);
            byte[] greeting = useAuth ? new byte[] { 5, 2, 0, 2 } : new byte[] { 5, 1, 0 };
            await stream.WriteAsync(greeting, 0, greeting.Length, token);

            byte[] response = new byte[2];
            await ReadExactAsync(stream, response, token);
            if (response[0] != 5) throw new Exception($"Invalid SOCKS version in greeting response: {response[0]}");
            byte method = response[1];

            // 2. Authentication
            if (method == 2)
            {
                if (!useAuth) throw new Exception("Proxy requested authentication but no credentials provided");

                // Subnegotiation version: 1
                var authRequest = new List<byte> { 1 };
                authRequest.Add((byte)username!.Length);
                authRequest.AddRange(Encoding.UTF8.GetBytes(username));
                authRequest.Add((byte)(password?.Length ?? 0));
                if (password != null) authRequest.AddRange(Encoding.UTF8.GetBytes(password));

                await stream.WriteAsync(authRequest.ToArray(), 0, authRequest.Count, token);
                await ReadExactAsync(stream, response, token);
                if (response[0] != 1) throw new Exception($"Invalid SOCKS auth subnegotiation version: {response[0]}");
                if (response[1] != 0) throw new Exception("SOCKS5 authentication failed");
            }
            else if (method != 0)
            {
                throw new Exception($"SOCKS5 method {method} not supported");
            }

            // 3. Connect
            // Ver: 5, Cmd: 1 (Connect), Rsv: 0, AType: 3 (Domain), Len, Host, Port
            var connectReq = new List<byte> { 5, 1, 0, 3 };
            byte[] hostBytes = Encoding.UTF8.GetBytes(targetHost);
            connectReq.Add((byte)hostBytes.Length);
            connectReq.AddRange(hostBytes);
            connectReq.Add((byte)(targetPort >> 8));
            connectReq.Add((byte)(targetPort & 0xFF));

            await stream.WriteAsync(connectReq.ToArray(), 0, connectReq.Count, token);

            // Read response: Ver, Rep, Rsv, AType, Bnd.Addr, Bnd.Port
            byte[] connectRes = new byte[4];
            await ReadExactAsync(stream, connectRes, token);
            if (connectRes[0] != 5) throw new Exception($"Invalid SOCKS version in connect response: {connectRes[0]}");
            if (connectRes[1] != 0) throw new Exception($"SOCKS5 connect failed with code {connectRes[1]}");

            byte aType = connectRes[3];
            if (aType == 1) await ReadExactAsync(stream, new byte[6], token); // IPv4 + Port
            else if (aType == 3)
            {
                byte[] lenBuf = new byte[1];
                await ReadExactAsync(stream, lenBuf, token);
                await ReadExactAsync(stream, new byte[lenBuf[0] + 2], token); // Domain + Port
            }
            else if (aType == 4) await ReadExactAsync(stream, new byte[18], token); // IPv6 + Port
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("SOCKS5 handshake timed out after 10 seconds.");
        }
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, ct);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}
