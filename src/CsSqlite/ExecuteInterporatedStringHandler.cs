using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CsSqlite;

[InterpolatedStringHandler]
public ref struct ExecuteInterporatedStringHandler
{
    char[]? chars;
    Span<char> commandText;
    public readonly ReadOnlySpan<char> CommandText => commandText[..textWritten];
    int textWritten;
    SqliteParam[] parameters;
    int parameterWritten;
    public readonly ReadOnlySpan<SqliteParam> Parameters => parameters[..parameterWritten];

    public ExecuteInterporatedStringHandler(int literalLength, int formattedCount)
    {
        if (literalLength > 0)
        {
            this.commandText = this.chars = ArrayPool<char>.Shared.Rent(literalLength + formattedCount * 3);
        }

        this.parameters = formattedCount > 0 ? ArrayPool<SqliteParam>.Shared.Rent(formattedCount) : [];
    }

    public ExecuteInterporatedStringHandler(int literalLength, int formattedCount, Span<char> commandText)
    {
        this.commandText = literalLength + formattedCount * 3 > commandText.Length ? this.chars = ArrayPool<char>.Shared.Rent(literalLength + formattedCount * 3) : commandText;
        this.parameters = formattedCount > 0 ? ArrayPool<SqliteParam>.Shared.Rent(formattedCount) : [];
    }

    public void AppendLiteral(string s)
    {
        s.AsSpan().CopyTo(commandText[textWritten..]);
        textWritten += s.Length;
    }

    public void AppendFormatted(string? s)
    {
        parameters[parameterWritten++] = new(SqlitePramKind.String, new(s?.AsMemory() ?? "".AsMemory()));
        WriteParameterPlaceholder();
    }

    public void AppendFormatted(ReadOnlyMemory<char> s)
    {
        parameters[parameterWritten++] = new(SqlitePramKind.String, new(s));
        WriteParameterPlaceholder();
    }

    public void AppendFormatted(ReadOnlyMemory<byte> s) => AppendFormatted(s, default);

    public void AppendFormatted(ReadOnlyMemory<byte> s, ReadOnlySpan<char> format)
    {
        parameters[parameterWritten++] =
            format.Length == 0 || format.SequenceEqual("text") ? new(SqlitePramKind.Utf8String, new(s)) :
            format.SequenceEqual("blob") ? new(SqlitePramKind.Blob, new(s)) :
            throw new ArgumentException($"The {nameof(format)} must be text or blob.", nameof(format));
        WriteParameterPlaceholder();
    }

    public void AppendFormatted(long value)
    {
        parameters[parameterWritten++] = new(SqlitePramKind.Integer, new(value));
        WriteParameterPlaceholder();
    }

    public void AppendFormatted(double value)
    {
        parameters[parameterWritten++] = new(SqlitePramKind.Double, new(value));
        WriteParameterPlaceholder();
    }

    void WriteParameterPlaceholder()
    {
        commandText[textWritten++] = ' ';
        commandText[textWritten++] = '?';
        commandText[textWritten++] = ' ';
    }

    public void Dispose()
    {
        if (chars != null)
        {
            ArrayPool<char>.Shared.Return(chars);
            chars = null;
        }

        if (parameters != null)
        {
            // Clear because SqliteParam can contain managed objects
            ArrayPool<SqliteParam>.Shared.Return(parameters, true);
            parameters = null!;
        }
    }
}

public readonly struct SqliteParam(SqlitePramKind kind, SqlitePramPayload payload)
{
    public readonly SqlitePramKind Kind = kind;
    public readonly SqlitePramPayload Payload = payload;

}

public enum SqlitePramKind : byte
{
    Integer,
    Double,
    String,
    Utf8String,
    Blob,
}

[StructLayout(LayoutKind.Explicit)]
public readonly struct SqlitePramPayload
{
    [FieldOffset(0)] readonly MemoryLike<long> memoryLikeLong;
    public readonly long Long => memoryLikeLong.Long;
    [FieldOffset(0)] readonly MemoryLike<double> memoryLikeDouble;
    public readonly double Double => memoryLikeDouble.Long;
    [FieldOffset(0)] public readonly ReadOnlyMemory<char> String;
    [FieldOffset(0)] public readonly ReadOnlyMemory<byte> BlobOrUtf8String;

    public SqlitePramPayload(long l) { this.memoryLikeLong = new(l); }
    public SqlitePramPayload(double d) { this.memoryLikeDouble = new(d); }
    public SqlitePramPayload(ReadOnlyMemory<char> t) { this.String = t; }
    public SqlitePramPayload(ReadOnlyMemory<byte> b) { this.BlobOrUtf8String = b; }

    private readonly struct MemoryLike<T>(T l)
    {
        public readonly T Long = l;
        private readonly object? dummy = null;
    }
}
