using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace OpenHub.Accessor.RealtimeMessaging.Tests.Fakes;

/// <summary>
/// A real, minimal Kestrel host bound to a random localhost port, hosting only RecordingHub —
/// no Redis, no auth, nothing else. Lets accessor tests exercise a real HubConnection over real
/// network I/O without pulling in any of RealtimeMessaging.Api's own infrastructure dependencies.
/// </summary>
public sealed class RecordingHubFixture : IAsyncLifetime
{
    private WebApplication? _app;

    public List<(string Method, object?[] Args)> Invocations { get; } = [];

    public string HubUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(Invocations);

        _app = builder.Build();
        _app.MapHub<RecordingHub>("/hubs/notifications");
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        HubUrl = $"{address}/hubs/notifications";
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
