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
    SqliteParam[]? sqliteParams;
    Span<SqliteParam> parameters;
    int parameterWritten;
    public readonly ReadOnlySpan<SqliteParam> Parameter => parameters[..parameterWritten];

    public ExecuteInterporatedStringHandler(int literalLength, int formattedCount)
    {
        if (literalLength > 0)
        {
            this.commandText = this.chars = ArrayPool<char>.Shared.Rent(literalLength + formattedCount * 3);
        }

        if (formattedCount > 0)
        {
            this.parameters = this.sqliteParams = ArrayPool<SqliteParam>.Shared.Rent(formattedCount);
        }
    }

    public ExecuteInterporatedStringHandler(int literalLength, int formattedCount, Span<char> commandText)
    {
        this.commandText = literalLength > commandText.Length ? this.chars = ArrayPool<char>.Shared.Rent(literalLength + formattedCount * 3) : commandText;

        if (formattedCount > 0)
        {
            this.parameters = this.sqliteParams = ArrayPool<SqliteParam>.Shared.Rent(formattedCount);
        }
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

    public void AppendFormatted(ReadOnlyMemory<byte> s)
    {
        parameters[parameterWritten++] = new(SqlitePramKind.Utf8String, new(s));
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

        if (sqliteParams != null)
        {
            ArrayPool<SqliteParam>.Shared.Return(sqliteParams);
            sqliteParams = null;
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
}

[StructLayout(LayoutKind.Explicit)]
public struct SqlitePramPayload
{
    [FieldOffset(0)] public readonly long Long;
    [FieldOffset(0)] public readonly double Double;
    [FieldOffset(8)] public readonly ReadOnlyMemory<char> String;
    [FieldOffset(8)] public readonly ReadOnlyMemory<byte> Utf8String;

    public SqlitePramPayload(long l) { this.Long = l; }
    public SqlitePramPayload(double d) { this.Double = d; }
    public SqlitePramPayload(ReadOnlyMemory<char> t) { this.String = t; }
    public SqlitePramPayload(ReadOnlyMemory<byte> b) { this.Utf8String = b; }
}
