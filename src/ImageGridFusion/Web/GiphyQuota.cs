namespace ImageGridFusion.Web;

/// <summary>
/// The GIPHY key's calls of the last hour, as the app counts them: GIPHY's answers say nothing of the quota, so
/// every call the app sends is noted, those older than an hour forgotten — the count right while the key serves
/// this app alone. Kept across sessions by its owner. See workfiles/20261105-online-library-giphy.md § Quota.
/// </summary>
internal sealed class GiphyQuota
{
    /// <summary>The calls a free GIPHY key allows an hour.</summary>
    public const int HourlyCalls = 100;

    private const long HourSeconds = 3600;

    private readonly List<long> _calls;

    /// <param name="calls">When earlier calls were sent, in Unix seconds.</param>
    public GiphyQuota(IEnumerable<long> calls)
    {
        this._calls = [.. calls.Order()];
    }

    /// <summary>When the calls of the last hour were sent, in Unix seconds, the oldest first.</summary>
    public IReadOnlyList<long> Calls => this._calls;

    /// <summary>The calls sent within the hour before <paramref name="now"/>.</summary>
    public int Used(long now)
    {
        this.Forget(now);
        return this._calls.Count;
    }

    /// <summary>A call sent at <paramref name="now"/>.</summary>
    public void Add(long now)
    {
        this.Forget(now);
        this._calls.Add(now);
    }

    /// <summary>The time now, in Unix seconds.</summary>
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void Forget(long now) => this._calls.RemoveAll(call => call <= now - HourSeconds || call > now + HourSeconds);
}
