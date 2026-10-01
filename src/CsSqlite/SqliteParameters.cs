using System.Buffers;
using System.Runtime.CompilerServices;
using static CsSqlite.NativeMethods;

namespace CsSqlite;

public readonly unsafe ref struct SqliteParameters
{
    readonly SqliteConnection connection;
    readonly PreparedStatements statements;

    internal SqliteParameters(SqliteConnection connection, PreparedStatements statements)
    {
        this.connection = connection;
        this.statements = statements;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            connection.ThrowIfDisposed();
            var count = 0;
            for (var i = 0; i < statements.Count; i++)
            {
                count += sqlite3_bind_parameter_count((sqlite3_stmt*)statements.Buffer[i]);
            }
            return count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        connection.ThrowIfDisposed();
        for (var i = 0; i < statements.Count; i++)
        {
            HandleErrorCode(sqlite3_clear_bindings((sqlite3_stmt*)statements.Buffer[i]));
        }
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
    public void AddLiteral(
        int index,
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.ConstantExpected]
#endif
        string text
    )
    {
        BindText(index, text, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLiteral(
        int index,
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.ConstantExpected]
#endif
        ReadOnlySpan<byte> utf8Text
    )
    {
        BindText(index, utf8Text, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<char> name, int value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindParameter(utf8Name.AsSpan(), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<byte> utf8Name, int value)
    {
        connection.ThrowIfDisposed();
        BindParameter(utf8Name, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<char> name, long value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindParameter(utf8Name.AsSpan(), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<byte> utf8Name, long value)
    {
        connection.ThrowIfDisposed();
        BindParameter(utf8Name, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<char> name, double value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindParameter(utf8Name.AsSpan(), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<byte> utf8Name, double value)
    {
        connection.ThrowIfDisposed();
        BindParameter(utf8Name, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<char> name, ReadOnlySpan<char> value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindText(utf8Name.AsSpan(), value, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLiteral(
        ReadOnlySpan<char> name,
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.ConstantExpected]
#endif
        string value
    )
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindText(utf8Name.AsSpan(), value, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<byte> value)
    {
        connection.ThrowIfDisposed();
        BindText(utf8Name, value, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLiteral(
        ReadOnlySpan<byte> utf8Name,
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.ConstantExpected]
#endif
        ReadOnlySpan<byte> value
    )
    {
        connection.ThrowIfDisposed();
        BindText(utf8Name, value, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddBytes(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<byte> value)
    {
        connection.ThrowIfDisposed();
        BindBlob(utf8Name, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddBytes(ReadOnlySpan<char> name, ReadOnlySpan<byte> value)
    {
        connection.ThrowIfDisposed();
        using var utf8Name = new PooledUtf8String(name);
        BindBlob(utf8Name.AsSpan(), value);
    }

    sqlite3_stmt* GetStatement(ref int index)
    {
        connection.ThrowIfDisposed();
        if (index > 0)
        {
            for (var i = 0; i < statements.Count; i++)
            {
                var stmt = (sqlite3_stmt*)statements.Buffer[i];
                var count = sqlite3_bind_parameter_count(stmt);
                if (index <= count)
                    return stmt;
                index -= count;
            }
        }
        throw new SqliteException(Constants.SQLITE_RANGE, "Could not add SQL parameter.");
    }

    int FindParameter(sqlite3_stmt* stmt, ReadOnlySpan<byte> utf8Name)
    {
        byte[]? rented = null;
        Span<byte> buffer =
            utf8Name.Length < 256
                ? stackalloc byte[utf8Name.Length + 1]
                : (rented = ArrayPool<byte>.Shared.Rent(utf8Name.Length + 1));
        try
        {
            utf8Name.CopyTo(buffer);
            buffer[utf8Name.Length] = 0;
            fixed (byte* ptr = buffer)
            {
                return sqlite3_bind_parameter_index(stmt, ptr);
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindParameter(int index, int value)
    {
        var stmt = GetStatement(ref index);
        BindParameter(stmt, index, value);
    }

    static void BindParameter(sqlite3_stmt* stmt, int index, int value)
    {
        var code = sqlite3_bind_int(stmt, index, value);
        HandleErrorCode(code);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindParameter(int index, long value)
    {
        var stmt = GetStatement(ref index);
        BindParameter(stmt, index, value);
    }

    static void BindParameter(sqlite3_stmt* stmt, int index, long value)
    {
        var code = sqlite3_bind_int64(stmt, index, value);
        HandleErrorCode(code);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindParameter(int index, double value)
    {
        var stmt = GetStatement(ref index);
        BindParameter(stmt, index, value);
    }

    static void BindParameter(sqlite3_stmt* stmt, int index, double value)
    {
        var code = sqlite3_bind_double(stmt, index, value);
        HandleErrorCode(code);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindText(int index, ReadOnlySpan<byte> utf8Text, bool isStatic)
    {
        var stmt = GetStatement(ref index);
        BindText(stmt, index, utf8Text, isStatic);
    }

    static void BindText(sqlite3_stmt* stmt, int index, ReadOnlySpan<byte> utf8Text, bool isStatic)
    {
        fixed (byte* ptr = utf8Text)
        {
            var code = sqlite3_bind_text(
                stmt,
                index,
                ptr,
                utf8Text.Length,
                isStatic ? Constants.SQLITE_STATIC : Constants.SQLITE_TRANSIENT
            );
            HandleErrorCode(code);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindText(int index, ReadOnlySpan<char> text, bool isStatic)
    {
        var stmt = GetStatement(ref index);
        BindText(stmt, index, text, isStatic);
    }

    static void BindText(sqlite3_stmt* stmt, int index, ReadOnlySpan<char> text, bool isStatic)
    {
        fixed (char* ptr = text)
        {
            var code = sqlite3_bind_text16(
                stmt,
                index,
                ptr,
                text.Length * 2,
                isStatic ? Constants.SQLITE_STATIC : Constants.SQLITE_TRANSIENT
            );
            HandleErrorCode(code);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void BindBlob(int index, ReadOnlySpan<byte> blob)
    {
        var stmt = GetStatement(ref index);
        BindBlob(stmt, index, blob);
    }

    static void BindBlob(sqlite3_stmt* stmt, int index, ReadOnlySpan<byte> blob)
    {
        fixed (byte* ptr = blob)
        {
            var code = sqlite3_bind_blob(stmt, index, ptr, blob.Length, Constants.SQLITE_TRANSIENT);
            HandleErrorCode(code);
        }
    }

    void BindParameter(ReadOnlySpan<byte> utf8Name, int value)
    {
        connection.ThrowIfDisposed();
        var found = false;
        for (var i = 0; i < statements.Count; i++)
        {
            var stmt = (sqlite3_stmt*)statements.Buffer[i];
            var index = FindParameter(stmt, utf8Name);
            if (index != 0)
            {
                BindParameter(stmt, index, value);
                found = true;
            }
        }
        if (!found)
            throw new SqliteException(Constants.SQLITE_RANGE, "Could not add SQL parameter.");
    }

    void BindParameter(ReadOnlySpan<byte> utf8Name, long value)
    {
        connection.ThrowIfDisposed();
        var found = false;
        for (var i = 0; i < statements.Count; i++)
        {
            var stmt = (sqlite3_stmt*)statements.Buffer[i];
            var index = FindParameter(stmt, utf8Name);
            if (index != 0)
            {
                BindParameter(stmt, index, value);
                found = true;
            }
        }
        if (!found)
            throw new SqliteException(Constants.SQLITE_RANGE, "Could not add SQL parameter.");
    }

    void BindParameter(ReadOnlySpan<byte> utf8Name, double value)
    {
        connection.ThrowIfDisposed();
        var found = false;
        for (var i = 0; i < statements.Count; i++)
        {
            var stmt = (sqlite3_stmt*)statements.Buffer[i];
            var index = FindParameter(stmt, utf8Name);
            if (index != 0)
            {
                BindParameter(stmt, index, value);
                found = true;
            }
        }
        if (!found)
            throw new SqliteException(Constants.SQLITE_RANGE, "Could not add SQL parameter.");
    }

    void BindText(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<byte> value, bool isStatic)
    {
        connection.ThrowIfDisposed();
        var found = false;
        for (var i = 0; i < statements.Count; i++)
        {
            var stmt = (sqlite3_stmt*)statements.Buffer[i];
            var index = FindParameter(stmt, utf8Name);
            if (index != 0)
            {
                BindText(stmt, index, value, isStatic);
                found = true;
            }
        }
        if (!found)
            throw new SqliteException(Constants.SQLITE_RANGE, "Could not add SQL parameter.");
    }

    void BindText(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<char> value, bool isStatic)
    {
        connection.ThrowIfDisposed();
        var found = false;
        for (var i = 0; i < statements.Count; i++)
        {
            var stmt = (sqlite3_stmt*)statements.Buffer[i];
            var index = FindParameter(stmt, utf8Name);
            if (index != 0)
            {
                BindText(stmt, index, value, isStatic);
                found = true;
            }
        }
        if (!found)
            throw new SqliteException(Constants.SQLITE_RANGE, "Could not add SQL parameter.");
    }

    void BindBlob(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<byte> value)
    {
        connection.ThrowIfDisposed();
        var found = false;
        for (var i = 0; i < statements.Count; i++)
        {
            var stmt = (sqlite3_stmt*)statements.Buffer[i];
            var index = FindParameter(stmt, utf8Name);
            if (index != 0)
            {
                BindBlob(stmt, index, value);
                found = true;
            }
        }
        if (!found)
            throw new SqliteException(Constants.SQLITE_RANGE, "Could not add SQL parameter.");
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
