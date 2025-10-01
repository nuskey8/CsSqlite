using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static CsSqlite.NativeMethods;

namespace CsSqlite;

public unsafe sealed class SqliteConnection(string path) : IDisposable
{
    enum State : byte
    {
        None,
        Open,
        Disposed,
    }

    State state;
    internal sqlite3* db;

    public bool IsDisposed => state == State.Disposed;

    public void Open()
    {
        ThrowIfDisposed();
        if (state == State.Open)
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(path.Length * 3);
        try
        {
            var bytesWritten = Encoding.UTF8.GetBytes(path, buffer);
            var fileName = buffer.AsSpan(0, bytesWritten);

            fixed (byte* fileNamePtr = fileName)
            fixed (sqlite3** p = &db)
            {
                var code = sqlite3_open(fileNamePtr, p);
                if (code != Constants.SQLITE_OK)
                {
                    throw new SqliteException(code, "Could not open database file: " + path);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        state = State.Open;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SqliteCommand CreateCommand(ReadOnlySpan<byte> utf8CommandText)
    {
        ThrowIfDisposed();
        Open();
        return new SqliteCommand(this, Prepare(utf8CommandText));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SqliteCommand CreateCommand(ReadOnlySpan<char> commandText)
    {
        ThrowIfDisposed();
        Open();
        return new SqliteCommand(this, Prepare(commandText));
    }

    sqlite3_stmt* Prepare(ReadOnlySpan<byte> utf8CommandText)
    {
        sqlite3_stmt* stmt = default;
        fixed (byte* sql = utf8CommandText)
        {
            var code = sqlite3_prepare_v2(db, sql, utf8CommandText.Length, &stmt, null);
            if (code != Constants.SQLITE_OK)
            {
                var errmsg = sqlite3_errmsg(db);
                var message = Marshal.PtrToStringAnsi((nint)errmsg);
                sqlite3_free(errmsg);
                throw new SqliteException(code, message);
            }
        }
        return stmt;
    }

    sqlite3_stmt* Prepare(ReadOnlySpan<char> commandText)
    {
        sqlite3_stmt* stmt = default;
        fixed (char* sql = commandText)
        {
            var code = sqlite3_prepare16_v2(db, sql, commandText.Length * 2, &stmt, null);
            if (code != Constants.SQLITE_OK)
            {
                var errmsg = sqlite3_errmsg(db);
                throw new SqliteException(code, Marshal.PtrToStringAnsi(new IntPtr(errmsg)));
            }
        }
        return stmt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ExecuteNonQuery(ReadOnlySpan<byte> utf8CommandText)
    {
        using var command = CreateCommand(utf8CommandText);
        return command.ExecuteNonQuery();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ExecuteNonQuery(ReadOnlySpan<char> commandText)
    {
        using var command = CreateCommand(commandText);
        return command.ExecuteNonQuery();
    }

    public int ExecuteNonQuery(ref ExecuteInterporatedStringHandler commandHandler)
    {
        using var command = CreateCommand(commandHandler.CommandText);
        var handlerParameters = commandHandler.Parameters;
        var commandParameters = command.Parameters;
        for (int i = 0; i < handlerParameters.Length; i++)
        {
            var p = handlerParameters[i];
            switch (p.Kind)
            {
                case SqlitePramKind.Integer:
                    commandParameters.Add(i + 1, p.Payload.Long);
                    break;
                case SqlitePramKind.Double:
                    commandParameters.Add(i + 1, p.Payload.Double);
                    break;
                case SqlitePramKind.String:
                    commandParameters.Add(i + 1, p.Payload.String.Span);
                    break;
                case SqlitePramKind.Utf8String:
                    commandParameters.Add(i + 1, p.Payload.BlobOrUtf8String.Span);
                    break;
                case SqlitePramKind.Blob:
                    commandParameters.AddBytes(i + 1, p.Payload.BlobOrUtf8String.Span);
                    break;
            }
        }
        return command.ExecuteNonQuery();
    }

    public int ExecuteNonQuery(Span<char> commandText, [InterpolatedStringHandlerArgument("commandText")] ref ExecuteInterporatedStringHandler commandHandler)
    {
        return ExecuteNonQuery(ref commandHandler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SqliteReader ExecuteReader(ReadOnlySpan<byte> utf8CommandText)
    {
        ThrowIfDisposed();
        Open();
        return new(this, Prepare(utf8CommandText), true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SqliteReader ExecuteReader(ReadOnlySpan<char> commandText)
    {
        ThrowIfDisposed();
        Open();
        return new(this, Prepare(commandText), true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SqliteReader ExecuteReader(ref ExecuteInterporatedStringHandler commandHandler)
    {
        using var command = CreateCommand(commandHandler.CommandText);
        var handlerParameters = commandHandler.Parameters;
        var commandParameters = command.Parameters;
        for (int i = 0; i < handlerParameters.Length; i++)
        {
            var p = handlerParameters[i];
            switch (p.Kind)
            {
                case SqlitePramKind.String:
                    commandParameters.Add(i + 1, p.Payload.String.Span);
                    break;
                case SqlitePramKind.Utf8String:
                    commandParameters.Add(i + 1, p.Payload.BlobOrUtf8String.Span);
                    break;
                case SqlitePramKind.Integer:
                    commandParameters.Add(i + 1, p.Payload.Long);
                    break;
                case SqlitePramKind.Double:
                    commandParameters.Add(i + 1, p.Payload.Double);
                    break;
                case SqlitePramKind.Blob:
                    commandParameters.AddBytes(i + 1, p.Payload.BlobOrUtf8String.Span);
                    break;
            }
        }
        return command.ExecuteReader();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SqliteReader ExecuteReader(Span<char> commandText, [InterpolatedStringHandlerArgument("commandText")] ref ExecuteInterporatedStringHandler commandHandler)
    {
        return ExecuteReader(ref commandHandler);
    }

    internal void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(SqliteConnection));
        }
    }

    public void Dispose()
    {
        ThrowIfDisposed();

        if (state == State.Open)
        {
            sqlite3_close(db);
            db = null;
        }

        state = State.Disposed;
    }
}