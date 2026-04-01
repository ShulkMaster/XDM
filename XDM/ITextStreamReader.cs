using System.Text;

namespace Shulkmaster.XDM;

public interface ITextStreamReader: IDisposable
{
    bool EOS { get; }
    Task<bool> FetchNextChunkAsync(CancellationToken cancellationToken = default);
    
    bool TryNext(out Rune rune);
}
