using System.Runtime.CompilerServices;
using static CsSqlite.NativeMethods;

namespace CsSqlite;

public readonly unsafe struct SqliteCommand(SqliteConnection connection, sqlite3_stmt* stmt) : IDisposable
{
    public SqliteParameters Parameters => new(connection, stmt);

    public int ExecuteNonQuery()
    {
        connection.ThrowIfDisposed();
        using var reader = ExecuteReader();
        var count = 0;
        while (reader.Read())
        {
            count++;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SqliteReader ExecuteReader()
    {
        return new(connection, stmt, false);
    }

    public void Dispose()
    {
        connection.ThrowIfDisposed();

        var code = sqlite3_finalize(stmt);
        if (code != Constants.SQLITE_OK)
        {
            throw new SqliteException(code, "Could not finalize SQL statement.");
        }
    }
}
