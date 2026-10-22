using LeverDeck.Domain.Errors;

namespace LeverDeck.Domain.Actions;

/// <summary>Opens an absolute http or https URL of at most 2048 characters in the default browser.</summary>
public sealed record OpenUrl : Action
{
    public const int MaxLength = 2048;

    public OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        Invariant.Require(
            url.IsAbsoluteUri && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps),
            DomainErrorCode.ValidationFailed,
            "URL must be absolute and use http or https.");
        Invariant.Require(
            url.AbsoluteUri.Length <= MaxLength,
            DomainErrorCode.ValidationFailed,
            $"URL must be at most {MaxLength} characters.");

        Url = url;
    }

    public Uri Url { get; }

    private protected override ExecutionTarget RunsOn => ExecutionTarget.Agent;
}
