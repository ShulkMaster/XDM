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

    [Fact]
    public async Task OnlyFetchesWhenAsked()
    {
        var data = Encoding.UTF8.GetBytes("HelloWorld");
        using var stream = new TrackedStream(data);
        using var reader = new TextStreamReader(stream, 5);

        Assert.Equal(0, stream.ReadCount);

        bool fetched = await reader.FetchNextChunkAsync();
        Assert.True(fetched);
        Assert.Equal(1, stream.ReadCount);

        var chars = reader.ToList();
        Assert.Equal("Hello", new string(chars.ToArray()));
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
        string s = new string(reader.ToArray());
        Assert.Equal("🌍", s);

        // Fetch 2: A
        await reader.FetchNextChunkAsync();
        var s2 = new string(reader.ToArray());
        Assert.Equal("A", s2);
    }

    [Fact]
    public async Task StopsWhenBufferExhausted()
    {
        var data = "ABC"u8.ToArray();
        using var stream = new MemoryStream(data);
        using var reader = new TextStreamReader(stream, 4);

        await reader.FetchNextChunkAsync();
        Assert.Equal(3, reader.Count());
        
        // The second call to GetEnumerator should be empty because we already consumed it
        Assert.Empty(reader);
        
        await reader.FetchNextChunkAsync();
        Assert.Empty(reader);
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
        
        foreach(var c in reader) {} // consume 'A'
        Assert.False(reader.EOS); // Stream not yet known to be empty (ReadAsync not called yet)
        
        bool fetched = await reader.FetchNextChunkAsync();
        Assert.False(fetched);
        Assert.True(reader.EOS);
    }
}