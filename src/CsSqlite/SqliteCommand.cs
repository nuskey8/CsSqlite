using System.Buffers;
using System.Runtime.CompilerServices;
using static CsSqlite.NativeMethods;

namespace CsSqlite;

public readonly unsafe struct SqliteCommand : IDisposable
{
    readonly SqliteConnection connection;
    readonly PreparedStatements statements;

    internal SqliteCommand(SqliteConnection connection, PreparedStatements statements)
    {
        this.connection = connection;
        this.statements = statements;
    }

    public SqliteParameters Parameters =>
        new(connection, statements);

    public int ExecuteNonQuery()
    {
        connection.ThrowIfDisposed();
        using var reader = ExecuteReader();
        var count = 0;
        do
        {
            while (reader.Read())
            {
                count++;
            }
        } while (reader.NextResult());

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SqliteReader ExecuteReader()
    {
        var reader = new SqliteReader(connection, statements, false, false);
        reader.NextResult();
        return reader;
    }

    public void Dispose()
    {
        connection.ThrowIfDisposed();

        try
        {
            for (var i = 0; i < statements.Count; i++)
            {
                if (statements.Buffer[i] == IntPtr.Zero)
                    continue;

                var stmt = (sqlite3_stmt*)statements.Buffer[i];
                statements.Buffer[i] = IntPtr.Zero;
                var code = sqlite3_finalize(stmt);
                if (code != Constants.SQLITE_OK)
                {
                    throw new SqliteException(code, "Could not finalize SQL statement.");
                }
            }
        }
        finally
        {
            ArrayPool<IntPtr>.Shared.Return(statements.Buffer, clearArray: true);
        }
    }
}
