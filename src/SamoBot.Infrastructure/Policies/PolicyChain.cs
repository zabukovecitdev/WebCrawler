using FluentResults;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Abstractions;

namespace SamoBot.Infrastructure.Policies;

public class PolicyChain : ICrawlPolicy
{
    private readonly IEnumerable<ICrawlPolicy> _policies;

    public PolicyChain(IEnumerable<ICrawlPolicy> policies)
    {
        _policies = policies;
    }

    public async Task<Result<UrlContentMetadata>> ExecuteAsync(
        ScheduledUrl scheduledUrl,
        Func<CancellationToken, Task<Result<UrlContentMetadata>>> action,
        CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task<Result<UrlContentMetadata>>> currentAction = action;

        // Build chain in reverse order so policies execute in the order they were provided
        foreach (var policy in _policies.Reverse())
        {
            var nextAction = currentAction;
            currentAction = ct => policy.ExecuteAsync(scheduledUrl, nextAction, ct);
        }

        return await currentAction(cancellationToken);
    }
}
