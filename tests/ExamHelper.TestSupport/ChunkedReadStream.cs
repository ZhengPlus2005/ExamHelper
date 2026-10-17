namespace ExamHelper.TestSupport;
public sealed class ChunkedReadStream(byte[] bytes, int chunkSize = 1) : Stream
{
    private int _position;
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => bytes.Length; public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer,int offset,int count)
    {
        var length = Math.Min(Math.Min(count,chunkSize),bytes.Length - _position);
        bytes.AsSpan(_position,length).CopyTo(buffer.AsSpan(offset,length)); _position += length; return length;
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var length = Math.Min(Math.Min(buffer.Length,chunkSize),bytes.Length - _position);
        bytes.AsMemory(_position,length).CopyTo(buffer); _position += length; return ValueTask.FromResult(length);
    }
    public override void Flush() { }
    public override long Seek(long offset,SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer,int offset,int count) => throw new NotSupportedException();
}