using System.Text;
using Shulkmaster.XDM;

namespace XDM.Test;

public class TextStreamReaderTests
{
    private class TrackedStream(byte[] buffer) : MemoryStream(buffer)
    {
        public int ReadCount { get; private set; }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ReadCount++;
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private static string ConsumeAll(TextStreamReader reader)
    {
        var sb = new StringBuilder();
        while (reader.TryNext(out var rune))
        {
            sb.Append(rune);
        }

        return sb.ToString();
    }

    [Fact]
    public async Task OnlyFetchesWhenAsked()
    {
        var data = "HelloWorld"u8.ToArray();
        using var stream = new TrackedStream(data);
        using var reader = new TextStreamReader(stream, 5);

        Assert.Equal(0, stream.ReadCount);

        bool fetched = await reader.FetchNextChunkAsync();
        Assert.True(fetched);
        Assert.Equal(1, stream.ReadCount);

        var s = ConsumeAll(reader);
        Assert.Equal("Hello", s);
        Assert.Equal(1, stream.ReadCount);
    }

    [Fact]
    public async Task HandlesPartialCharsAcrossChunks()
    {
        // 🌍 is F0 9F 8C 8D (4 bytes)
        var data = new byte[] { 0xF0, 0x9F, 0x8C, 0x8D, (byte)'A' };
        using var stream = new MemoryStream(data);
        using var reader = new TextStreamReader(stream, 4);

        // Fetch 1: F0 9F 8C 8D
        await reader.FetchNextChunkAsync();

        Assert.Equal("🌍", ConsumeAll(reader));

        // Fetch 2: A
        await reader.FetchNextChunkAsync();
        Assert.Equal("A", ConsumeAll(reader));
    }

    [Fact]
    public async Task StopsWhenBufferExhausted()
    {
        var data = "ABC"u8.ToArray();
        using var stream = new MemoryStream(data);
        using var reader = new TextStreamReader(stream, 4);

        await reader.FetchNextChunkAsync();
        Assert.Equal("ABC", ConsumeAll(reader));

        // The second call to TryNext should be false because we already consumed it
        Assert.False(reader.TryNext(out _));

        await reader.FetchNextChunkAsync();
        Assert.False(reader.TryNext(out _));
    }

    [Fact]
    public async Task IsEndOfStreamTests()
    {
        var data = "A"u8.ToArray();
        using var stream = new MemoryStream(data);
        using var reader = new TextStreamReader(stream, 4);

        Assert.False(reader.EOS);

        await reader.FetchNextChunkAsync();
        Assert.False(reader.EOS); // 'A' in buffer

        _ = ConsumeAll(reader); // consume 'A'
        Assert.False(reader.EOS); // Stream not yet known to be empty (ReadAsync not called yet)

        var fetched = await reader.FetchNextChunkAsync();
        Assert.False(fetched);
        Assert.True(reader.EOS);
    }
}