using System.Threading.Channels;

namespace Shared;

/// <summary>
/// The one-slot doorbell between "Serilog wrote something" and "tell the admin screens".
///
/// <para>
/// In <c>FileHub.Shared</c> rather than beside the hub because it is the contract between the two
/// halves and has no dependency on either — and because the integration tests reach Shared but
/// deliberately do not reference <c>FileHub.Api</c>, so this is what makes the coalescing testable
/// without standing up the web host.
/// </para>
///
/// <para>
/// A bounded channel of capacity one with <see cref="BoundedChannelFullMode.DropWrite"/>, which is
/// the whole design:
/// </para>
/// <list type="bullet">
/// <item><b>It coalesces.</b> A burst of two hundred lines leaves exactly one token, so the screens
/// get one notification and fetch once, rather than two hundred times.</item>
/// <item><b>It never blocks and never throws.</b> <see cref="Ring"/> is called from inside the
/// logging pipeline, on whatever thread happened to log — a doorbell that can block is a doorbell
/// that can deadlock the application by writing a log line.</item>
/// <item><b>It cannot recurse.</b> Broadcasting is done by a background reader, not by the caller.
/// Sending over SignalR logs (Kestrel, the hub itself), and if that logging rang the bell
/// synchronously the ring would push, which would log, which would push. Here a log line written
/// while broadcasting just refills a slot that is already full, and is dropped.</item>
/// </list>
/// </summary>
public sealed class LogChangeSignal
{
    private readonly Channel<byte> m_channel = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>
    /// Something was logged. Returns immediately; a token already waiting is left as it is.
    /// </summary>
    public void Ring() => m_channel.Writer.TryWrite(0);

    /// <summary>
    /// Waits for the next ring. Completes only when something has actually been logged, which is
    /// what makes this push rather than a poll with extra steps.
    /// </summary>
    public ValueTask<byte> WaitAsync(CancellationToken cancellationToken) =>
        m_channel.Reader.ReadAsync(cancellationToken);
}
