using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.MailBridge.Contracts.Models;

namespace OpenClaw.Core.Pages;

public sealed class IndexModel : PageModel
{
    private readonly CoreCacheRepository repository;
    private readonly CoreHealthState healthState;

    public IndexModel(IServiceProvider services)
    {
        repository = services.GetRequiredService<CoreCacheRepository>();
        healthState = services.GetRequiredService<CoreHealthState>();
        HealthSnapshot = healthState.GetSnapshot();
    }

    public IReadOnlyList<MessageDto> RecentMessages { get; private set; } =
        Array.Empty<MessageDto>();

    public IReadOnlyList<MessageDto> MeetingRequests { get; private set; } =
        Array.Empty<MessageDto>();

    public IReadOnlyList<EventDto> Events { get; private set; } = Array.Empty<EventDto>();

    public CoreHealthSnapshot HealthSnapshot { get; private set; }

    public async Task OnGetAsync()
    {
        HealthSnapshot = healthState.GetSnapshot();
        RecentMessages = await repository.ListMessagesAsync("mail", null, 20);
        MeetingRequests = await repository.ListMessagesAsync("meeting", null, 20);
        Events = await repository.ListEventsAsync(
            DateTimeOffset.UtcNow.AddDays(-14),
            DateTimeOffset.UtcNow.AddDays(30),
            20
        );
    }
}
