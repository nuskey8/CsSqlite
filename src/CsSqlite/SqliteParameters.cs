using System.Runtime.CompilerServices;
using static CsSqlite.NativeMethods;

namespace CsSqlite;

public readonly unsafe ref struct SqliteParameters
{
    readonly SqliteConnection connection;
    readonly sqlite3_stmt* stmt;

    internal SqliteParameters(SqliteConnection connection, sqlite3_stmt* stmt)
    {
        this.connection = connection;
        this.stmt = stmt;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            connection.ThrowIfDisposed();
            return sqlite3_bind_parameter_count(stmt);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        connection.ThrowIfDisposed();
        sqlite3_clear_bindings(stmt);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int index, int value)
    {
        BindParameter(index, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int index, long value)
    {
        BindParameter(index, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int index, ReadOnlySpan<char> text)
    {
        using var utf8Text = new PooledUtf8String(text);
        BindText(index, utf8Text.AsSpan(), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int index, ReadOnlySpan<byte> utf8Text)
    {
        BindText(index, utf8Text, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLiteral(int index,
#if NET8_0_OR_GREATER
[System.Diagnostics.CodeAnalysis.ConstantExpected]
#endif
    string text)
    {
        BindText(index, text, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLiteral(int index,
#if NET8_0_OR_GREATER
[System.Diagnostics.CodeAnalysis.ConstantExpected]
#endif
    ReadOnlySpan<byte> utf8Text)
    {
        BindText(index, utf8Text, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<char> name, int value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindParameter(GetParameterIndex(utf8Name.AsSpan()), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<byte> utf8Name, int value)
    {
        connection.ThrowIfDisposed();
        BindParameter(GetParameterIndex(utf8Name), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<char> name, long value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindParameter(GetParameterIndex(utf8Name.AsSpan()), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<byte> utf8Name, long value)
    {
        connection.ThrowIfDisposed();
        BindParameter(GetParameterIndex(utf8Name), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<char> name, double value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindParameter(GetParameterIndex(utf8Name.AsSpan()), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<byte> utf8Name, double value)
    {
        connection.ThrowIfDisposed();
        BindParameter(GetParameterIndex(utf8Name), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<char> name, ReadOnlySpan<char> value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindText(GetParameterIndex(utf8Name.AsSpan()), value, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLiteral(ReadOnlySpan<char> name,
#if NET8_0_OR_GREATER
[System.Diagnostics.CodeAnalysis.ConstantExpected]
#endif
string value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindText(GetParameterIndex(utf8Name.AsSpan()), value, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<byte> value)
    {
        connection.ThrowIfDisposed();
        BindText(GetParameterIndex(utf8Name), value, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLiteral(ReadOnlySpan<byte> utf8Name,
#if NET8_0_OR_GREATER
[System.Diagnostics.CodeAnalysis.ConstantExpected]
#endif
    ReadOnlySpan<byte> value)
    {
        connection.ThrowIfDisposed();
        BindText(GetParameterIndex(utf8Name), value, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddBytes(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<byte> value)
    {
        connection.ThrowIfDisposed();
        BindBlob(GetParameterIndex(utf8Name), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddBytes(ReadOnlySpan<char> name, ReadOnlySpan<byte> value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindBlob(GetParameterIndex(utf8Name.AsSpan()), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int GetParameterIndex(ReadOnlySpan<byte> utf8Name)
    {
        fixed (byte* name = utf8Name)
        {
            return sqlite3_bind_parameter_index(stmt, name);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindParameter(int index, int value)
    {
        var code = sqlite3_bind_int(stmt, index, value);
        HandleErrorCode(code);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindParameter(int index, long value)
    {
        var code = sqlite3_bind_int64(stmt, index, value);
        HandleErrorCode(code);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindParameter(int index, double value)
    {
        var code = sqlite3_bind_double(stmt, index, value);
        HandleErrorCode(code);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindText(int index, ReadOnlySpan<byte> utf8Text, bool isStatic)
    {
        fixed (byte* ptr = utf8Text)
        {
            var code = sqlite3_bind_text(stmt, index, ptr, utf8Text.Length, isStatic ? Constants.SQLITE_STATIC : Constants.SQLITE_TRANSIENT);
            HandleErrorCode(code);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindText(int index, ReadOnlySpan<char> text, bool isStatic)
    {
        fixed (char* ptr = text)
        {
            var code = sqlite3_bind_text16(stmt, index, ptr, text.Length * 2, isStatic ? Constants.SQLITE_STATIC : Constants.SQLITE_TRANSIENT);
            HandleErrorCode(code);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindBlob(int index, ReadOnlySpan<byte> blob)
    {
        fixed (byte* ptr = blob)
        {
            var code = sqlite3_bind_blob(stmt, index, ptr, blob.Length, Constants.SQLITE_TRANSIENT);
            HandleErrorCode(code);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void HandleErrorCode(int code)
    {
        if (code != Constants.SQLITE_OK)
        {
            throw new SqliteException(code, "Could not add SQL parameter.");
        }
    }
}