using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using SRT2Speech.ProxyService.Enums;
using SRT2Speech.ProxyService.Models;

namespace SRT2Speech.ProxyService.HttpHandlers;

/// <summary>
/// Factory để tạo SocketsHttpHandler với hỗ trợ SOCKS5 thực sự
/// Khắc phục vấn đề WebProxy không hỗ trợ SOCKS5 natively
/// </summary>
public static class Socks5HttpHandlerFactory
{
    /// <summary>
    /// Tạo SocketsHttpHandler được cấu hình để sử dụng SOCKS5 proxy
    /// </summary>
    public static SocketsHttpHandler CreateSocks5Handler(ProxyInfo proxyInfo)
    {
        if (proxyInfo == null)
            throw new ArgumentNullException(nameof(proxyInfo));

        if (proxyInfo.Type != ProxyType.SOCKS5)
        {
            throw new ArgumentException($"This handler only supports SOCKS5 proxies. Got: {proxyInfo.Type}", nameof(proxyInfo));
        }

        var handler = new SocketsHttpHandler
        {
            UseProxy = false, // Không dùng WebProxy (vì không support SOCKS5)
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = true,
            MaxConnectionsPerServer = 10,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            
            // Set custom connection callback để route qua SOCKS5
            ConnectCallback = async (context, cancellationToken) =>
            {
                try
                {
                    // Kết nối qua SOCKS5 proxy
                    var socket = await ConnectViaSocks5Async(
                        proxyInfo,
                        context.DnsEndPoint.Host,
                        context.DnsEndPoint.Port,
                        cancellationToken);

                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SOCKS5_ERROR] Failed to connect via SOCKS5 proxy {proxyInfo.Host}:{proxyInfo.Port}: {ex.Message}");
                    throw new ProxyConnectionException($"SOCKS5 connection failed: {ex.Message}", ex);
                }
            }
        };

        return handler;
    }

    /// <summary>
    /// Kết nối đến target host qua SOCKS5 proxy
    /// </summary>
    private static async Task<Socket> ConnectViaSocks5Async(
        ProxyInfo proxyInfo,
        string targetHost,
        int targetPort,
        CancellationToken cancellationToken)
    {
        Socket? socket = null;
        try
        {
            Console.WriteLine($"[SOCKS5_CONNECT] Connecting to {targetHost}:{targetPort} via SOCKS5 proxy {proxyInfo.Host}:{proxyInfo.Port}");

            // Tạo socket mới
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = 30000,
                ReceiveTimeout = 30000
            };

            // Kết nối đến SOCKS5 proxy server
            await socket.ConnectAsync(proxyInfo.Host, proxyInfo.Port, cancellationToken);

            // Thực hiện SOCKS5 handshake
            await PerformSocks5HandshakeAsync(socket, proxyInfo, targetHost, targetPort, cancellationToken);

            Console.WriteLine($"[SOCKS5_SUCCESS] Successfully connected to {targetHost}:{targetPort} via SOCKS5 proxy");

            return socket;
        }
        catch (Exception ex)
        {
            socket?.Dispose();
            Console.WriteLine($"[SOCKS5_FAIL] Failed to connect via SOCKS5: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Thực hiện SOCKS5 handshake protocol
    /// </summary>
    private static async Task PerformSocks5HandshakeAsync(
        Socket socket,
        ProxyInfo proxyInfo,
        string targetHost,
        int targetPort,
        CancellationToken cancellationToken)
    {
        // SOCKS5 Handshake Step 1: Send greeting
        byte[] greeting;
        if (!string.IsNullOrEmpty(proxyInfo.Username))
        {
            // With authentication
            greeting = new byte[] { 0x05, 0x02, 0x00, 0x02 }; // Version 5, 2 methods: No auth, Username/Password
        }
        else
        {
            // No authentication
            greeting = new byte[] { 0x05, 0x01, 0x00 }; // Version 5, 1 method: No auth
        }

        await SendAsync(socket, greeting, cancellationToken);

        // Receive server's method selection
        var methodSelection = new byte[2];
        await ReceiveAsync(socket, methodSelection, cancellationToken);

        if (methodSelection[0] != 0x05)
            throw new ProxyConnectionException("Invalid SOCKS5 server response");

        // SOCKS5 Handshake Step 2: Authentication if needed
        if (methodSelection[1] == 0x02) // Username/Password authentication
        {
            if (string.IsNullOrEmpty(proxyInfo.Username))
                throw new ProxyConnectionException("SOCKS5 proxy requires authentication but no credentials provided");

            await PerformUsernamePasswordAuthAsync(socket, proxyInfo, cancellationToken);
        }
        else if (methodSelection[1] != 0x00)
        {
            throw new ProxyConnectionException($"SOCKS5 authentication method not supported: 0x{methodSelection[1]:X2}");
        }

        // SOCKS5 Handshake Step 3: Send connection request
        var request = BuildConnectionRequest(targetHost, targetPort);
        await SendAsync(socket, request, cancellationToken);

        // Receive connection response (at least 10 bytes)
        var response = new byte[256];
        var received = await ReceiveAsync(socket, response, cancellationToken, minBytes: 10);

        if (response[0] != 0x05)
            throw new ProxyConnectionException("Invalid SOCKS5 connection response");

        if (response[1] != 0x00)
        {
            var errorMessage = GetSocks5ErrorMessage(response[1]);
            throw new ProxyConnectionException($"SOCKS5 connection failed: {errorMessage}");
        }

        // Connection established successfully
    }

    /// <summary>
    /// Thực hiện username/password authentication
    /// </summary>
    private static async Task PerformUsernamePasswordAuthAsync(
        Socket socket,
        ProxyInfo proxyInfo,
        CancellationToken cancellationToken)
    {
        var username = System.Text.Encoding.UTF8.GetBytes(proxyInfo.Username!);
        var password = System.Text.Encoding.UTF8.GetBytes(proxyInfo.Password ?? "");

        var authRequest = new byte[3 + username.Length + password.Length];
        authRequest[0] = 0x01; // Version of username/password auth
        authRequest[1] = (byte)username.Length;
        Array.Copy(username, 0, authRequest, 2, username.Length);
        authRequest[2 + username.Length] = (byte)password.Length;
        Array.Copy(password, 0, authRequest, 3 + username.Length, password.Length);

        await SendAsync(socket, authRequest, cancellationToken);

        var authResponse = new byte[2];
        await ReceiveAsync(socket, authResponse, cancellationToken);

        if (authResponse[1] != 0x00)
            throw new ProxyConnectionException("SOCKS5 authentication failed: Invalid credentials");
    }

    /// <summary>
    /// Xây dựng SOCKS5 connection request
    /// </summary>
    private static byte[] BuildConnectionRequest(string targetHost, int targetPort)
    {
        var hostBytes = System.Text.Encoding.UTF8.GetBytes(targetHost);
        var request = new byte[7 + hostBytes.Length];

        request[0] = 0x05; // SOCKS version
        request[1] = 0x01; // Command: CONNECT
        request[2] = 0x00; // Reserved
        request[3] = 0x03; // Address type: Domain name
        request[4] = (byte)hostBytes.Length;
        Array.Copy(hostBytes, 0, request, 5, hostBytes.Length);
        request[5 + hostBytes.Length] = (byte)(targetPort >> 8); // Port high byte
        request[6 + hostBytes.Length] = (byte)(targetPort & 0xFF); // Port low byte

        return request;
    }

    /// <summary>
    /// Send data với timeout và cancellation support
    /// </summary>
    private static async Task SendAsync(Socket socket, byte[] data, CancellationToken cancellationToken)
    {
        var sent = 0;
        while (sent < data.Length)
        {
            var count = await socket.SendAsync(new ArraySegment<byte>(data, sent, data.Length - sent), SocketFlags.None, cancellationToken);
            if (count == 0)
                throw new ProxyConnectionException("Socket connection closed during send");
            sent += count;
        }
    }

    /// <summary>
    /// Receive data với timeout và cancellation support
    /// </summary>
    private static async Task<int> ReceiveAsync(Socket socket, byte[] buffer, CancellationToken cancellationToken, int minBytes = -1)
    {
        if (minBytes == -1)
            minBytes = buffer.Length;

        var received = 0;
        while (received < minBytes)
        {
            var count = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, received, buffer.Length - received), SocketFlags.None, cancellationToken);
            if (count == 0)
                throw new ProxyConnectionException("Socket connection closed during receive");
            received += count;
        }
        return received;
    }

    /// <summary>
    /// Lấy error message từ SOCKS5 reply code
    /// </summary>
    private static string GetSocks5ErrorMessage(byte replyCode)
    {
        return replyCode switch
        {
            0x01 => "General SOCKS server failure",
            0x02 => "Connection not allowed by ruleset",
            0x03 => "Network unreachable",
            0x04 => "Host unreachable",
            0x05 => "Connection refused",
            0x06 => "TTL expired",
            0x07 => "Command not supported",
            0x08 => "Address type not supported",
            _ => $"Unknown error (code: 0x{replyCode:X2})"
        };
    }
}

/// <summary>
/// Exception cho các lỗi kết nối proxy
/// </summary>
public class ProxyConnectionException : Exception
{
    public ProxyConnectionException(string message) : base(message) { }
    public ProxyConnectionException(string message, Exception innerException) : base(message, innerException) { }
}