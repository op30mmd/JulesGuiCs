using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using JulesClient.Services;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Diagnostics;

namespace JulesClient;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;
    public static MainWindow? MainWindow { get; private set; }
    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
        this.InitializeComponent();

        this.UnhandledException += (s, e) =>
        {
            Debug.WriteLine($"[CRASH] Unhandled Exception: {e.Message}");
            Debug.WriteLine(e.Exception.ToString());
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Debug.WriteLine($"[CRASH] AppDomain Unhandled Exception: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Debug.WriteLine($"[CRASH] Unobserved Task Exception: {e.Exception}");
            e.SetObserved();
        };
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IPollingService, PollingService>();

        services.AddSingleton<IJulesApiClient>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();

            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                MaxConnectionsPerServer = 4,
                EnableMultipleHttp2Connections = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli
            };

            if (settings.ProxyMode == ProxyMode.None)
            {
                handler.UseProxy = false;
                handler.Proxy = null;
            }
            else if (settings.ProxyMode == ProxyMode.System)
            {
                handler.Proxy = HttpClient.DefaultProxy;
                handler.UseProxy = true;
            }
            else if (settings.ProxyMode == ProxyMode.Manual && !string.IsNullOrEmpty(settings.ProxyHost))
            {
                handler.ConnectCallback = async (context, ct) =>
                {
                    if (settings.ProxyBypassLocal && IsLocal(context.DnsEndPoint.Host))
                    {
                        var directSocket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                        await directSocket.ConnectAsync(context.DnsEndPoint, ct);
                        return new NetworkStream(directSocket, true);
                    }

                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(settings.ProxyHost, settings.ProxyPort, ct);
                        var stream = new NetworkStream(socket, true);
                        await Socks5Handshaker.HandshakeAsync(stream, context.DnsEndPoint.Host, context.DnsEndPoint.Port, settings.ProxyUsername, settings.ProxyPassword, ct);
                        return stream;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PROXY] SOCKS5 Connection failed: {ex.Message}");
                        socket.Dispose();

                        Debug.WriteLine($"[PROXY] Falling back to direct connection for {context.DnsEndPoint.Host}:{context.DnsEndPoint.Port}");
                        var fallbackSocket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                        await fallbackSocket.ConnectAsync(context.DnsEndPoint, ct);
                        return new NetworkStream(fallbackSocket, true);
                    }
                };
            }

            return new JulesApiClient(settings, handler);
        });

        services.AddSingleton<ICachedJulesApiClient>(sp =>
        {
            var inner = sp.GetRequiredService<IJulesApiClient>();
            var cache = sp.GetRequiredService<ICacheService>();
            return new CachedJulesApiClient(inner, cache);
        });

        return services.BuildServiceProvider();
    }

    private static bool IsLocal(string host)
    {
        if (string.IsNullOrEmpty(host)) return false;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Equals("127.0.0.1") || host.Equals("::1")) return true;

        // If it's an IP address, check for loopback
        if (IPAddress.TryParse(host, out var ip))
        {
            return IPAddress.IsLoopback(ip);
        }

        // Hostnames without dots are usually local (Intranet)
        if (!host.Contains('.')) return true;

        return false;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
