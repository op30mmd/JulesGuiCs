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
        services.AddSingleton<IPollingService, PollingService>();

        services.AddSingleton<IJulesApiClient>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();

            HttpMessageHandler handler;

            if (settings.ProxyEnabled && !string.IsNullOrEmpty(settings.ProxyHost))
            {
                handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                    MaxConnectionsPerServer = 2,
                    EnableMultipleHttp2Connections = false,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli,
                    ConnectCallback = async (context, ct) =>
                    {
                        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = false };
                        try
                        {
                            await socket.ConnectAsync(settings.ProxyHost, settings.ProxyPort, ct);
                            var stream = new NetworkStream(socket, true);
                            await Socks5Handshaker.HandshakeAsync(stream, context.DnsEndPoint.Host, context.DnsEndPoint.Port, settings.ProxyUsername, settings.ProxyPassword, ct);
                            return stream;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PROXY] Connection failed: {ex.Message}");
                            Debug.WriteLine($"[PROXY] Falling back to direct connection for {context.DnsEndPoint.Host}:{context.DnsEndPoint.Port}");
                            socket.Dispose();

                            var directSocket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = false };
                            await directSocket.ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, ct);
                            return new NetworkStream(directSocket, true);
                        }
                    }
                };
            }
            else
            {
                handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                    MaxConnectionsPerServer = 2,
                    EnableMultipleHttp2Connections = false,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli
                };
            }

            return new JulesApiClient(settings, handler);
        });

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
