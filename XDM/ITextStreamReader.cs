namespace Shulkmaster.XDM;

public interface ITextStreamReader : IEnumerable<char>, IDisposable
{
    bool EOS { get; }
    Task<bool> FetchNextChunkAsync(CancellationToken cancellationToken = default);
}
