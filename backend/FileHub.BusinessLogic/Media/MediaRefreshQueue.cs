using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FileHub.BusinessLogic.Media;

/// <summary>
/// The look-ups share link previews asked for, waiting for <see cref="MediaRefreshWorker"/>. A
/// folder already waiting or running is not queued twice, so a link pasted into a busy chat costs
/// one look-up rather than one per unfurl. Bounded, and full means dropped: the next unfurl asks again.
/// </summary>
public sealed class MediaRefreshQueue
{
    private const int Capacity = 64;

    private readonly Channel<MediaRefreshRequest> m_channel = Channel.CreateBounded<MediaRefreshRequest>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<MediaRefreshRequest, byte> m_pending = new();

    /// <summary>Never blocks: it is called on an anonymous request that must answer at once.</summary>
    public bool TryEnqueue(MediaRefreshRequest request)
    {
        if (!m_pending.TryAdd(request, 0))
        {
            return false;
        }

        if (m_channel.Writer.TryWrite(request))
        {
            return true;
        }

        m_pending.TryRemove(request, out _);
        return false;
    }

    public ValueTask<MediaRefreshRequest> ReadAsync(CancellationToken cancellationToken) =>
        m_channel.Reader.ReadAsync(cancellationToken);

    public bool TryRead(out MediaRefreshRequest request) => m_channel.Reader.TryRead(out request!);

    /// <summary>Called once the look-up has finished, so the folder may be queued again.</summary>
    public void Done(MediaRefreshRequest request) => m_pending.TryRemove(request, out _);
}
