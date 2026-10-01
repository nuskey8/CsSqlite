using System.Buffers;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static CsSqlite.NativeMethods;

namespace CsSqlite;

[StructLayout(LayoutKind.Auto)]
public unsafe struct SqliteReader : IDisposable
{
    readonly SqliteConnection connection;
    readonly PreparedStatements statements;
    readonly bool finalizeStatements;
    readonly bool returnStatements;
    int stmtIndex;
    sqlite3_stmt* stmt;

    internal SqliteReader(
        SqliteConnection connection,
        PreparedStatements statements,
        bool finalizeStatements,
        bool returnStatements
    )
    {
        this.connection = connection;
        this.statements = statements;
        this.finalizeStatements = finalizeStatements;
        this.returnStatements = returnStatements;
        stmtIndex = 0;
        stmt = null;
    }

    public readonly int ColumnCount
    {
        get
        {
            connection.ThrowIfDisposed();
            return sqlite3_column_count(stmt);
        }
    }

    public readonly bool Read()
    {
        connection.ThrowIfDisposed();
        if (stmt == null)
            return false;

        var code = sqlite3_step(stmt);
        switch (code)
        {
            case Constants.SQLITE_DONE:
                return false;
            case Constants.SQLITE_ROW:
                return true;
            case Constants.SQLITE_ERROR:
                var msg = sqlite3_errmsg(connection.db);
                var message = Marshal.PtrToStringAnsi((nint)msg);
                sqlite3_free(msg);
                throw new SqliteException(Constants.SQLITE_ERROR, message);
            case Constants.SQLITE_MISUSE:
                throw new SqliteException(Constants.SQLITE_MISUSE, "Invalid SQL statement");
            default:
                return false;
        }
    }

    public bool NextResult()
    {
        connection.ThrowIfDisposed();

        while (stmtIndex < statements.Count)
        {
            stmt = (sqlite3_stmt*)statements.Buffer[stmtIndex++];
            if (sqlite3_column_count(stmt) > 0)
            {
                return true;
            }

            while (true)
            {
                var code = sqlite3_step(stmt);
                switch (code)
                {
                    case Constants.SQLITE_DONE:
                        goto NextStatement;
                    case Constants.SQLITE_ROW:
                        break;
                    case Constants.SQLITE_ERROR:
                        var msg = sqlite3_errmsg(connection.db);
                        var message = Marshal.PtrToStringAnsi((nint)msg);
                        throw new SqliteException(Constants.SQLITE_ERROR, message);
                    case Constants.SQLITE_MISUSE:
                        throw new SqliteException(Constants.SQLITE_MISUSE, "Invalid SQL statement");
                    default:
                        throw new SqliteException(code, "Could not execute SQL statement.");
                }
            }

            NextStatement:
            continue;
        }

        stmt = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly SqliteType GetColumnType(int column)
    {
        connection.ThrowIfDisposed();
        return (SqliteType)sqlite3_column_type(stmt, column);
    }

    public readonly bool TryGetBytes(int column, Span<byte> destination, out int bytesWritten)
    {
        connection.ThrowIfDisposed();
        var ptr = sqlite3_column_blob(stmt, column);
        var count = sqlite3_column_bytes(stmt, column);

        if (destination.Length <= count)
        {
            bytesWritten = 0;
            return false;
        }

#if NET8_0_OR_GREATER
        Unsafe.CopyBlock(ref destination[0], ref Unsafe.AsRef<byte>(ptr), (uint)count);
#else
        new Span<byte>(ptr, count).CopyTo(destination);
#endif

        bytesWritten = count;
        return true;
    }

    public readonly bool TryGetString(int column, Span<byte> utf8Destination, out int bytesWritten)
    {
        connection.ThrowIfDisposed();
        var ptr = sqlite3_column_text(stmt, column);
        var count = sqlite3_column_bytes(stmt, column);

        if (utf8Destination.Length <= count)
        {
            bytesWritten = 0;
            return false;
        }

#if NET8_0_OR_GREATER
        Unsafe.CopyBlock(ref utf8Destination[0], ref Unsafe.AsRef<byte>(ptr), (uint)count);
#else
        new Span<byte>(ptr, count).CopyTo(utf8Destination);
#endif

        bytesWritten = count;
        return true;
    }

    public readonly bool TryGetString(int column, Span<char> destination, out int charsWritten)
    {
        connection.ThrowIfDisposed();
        var ptr = (char*)sqlite3_column_text16(stmt, column);
        var count = sqlite3_column_bytes16(stmt, column) / 2;

        if (destination.Length <= count)
        {
            charsWritten = 0;
            return false;
        }

        new Span<char>(ptr, count).CopyTo(destination);
        charsWritten = count;
        return true;
    }

    public readonly string GetString(int column)
    {
        connection.ThrowIfDisposed();
        var ptr = (char*)sqlite3_column_text16(stmt, column);
        var count = sqlite3_column_bytes16(stmt, column) / 2;

        var buffer = ArrayPool<char>.Shared.Rent(count);
        try
        {
            new Span<char>(ptr, count).CopyTo(buffer);
            return buffer.AsSpan(0, count).ToString();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    public readonly string GetName(int column)
    {
        connection.ThrowIfDisposed();
        var ptr = (char*)sqlite3_column_name16(stmt, column);
        if (ptr == null)
            return "";

        var count = 0;
        while (ptr[count] != 0)
        {
            count++;
        }

        var buffer = ArrayPool<char>.Shared.Rent(count);
        try
        {
            new Span<char>(ptr, count).CopyTo(buffer);
            return buffer.AsSpan(0, count).ToString();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Returns metadata for the columns in the current result set.
    /// </summary>
    public readonly DataTable GetSchemaTable()
    {
        connection.ThrowIfDisposed();

        var result = CreateSchemaTable();
        var hasColumnMetadata = HasColumnMetadata();
        var columnCount = sqlite3_column_count(stmt);

        for (var column = 0; column < columnCount; column++)
        {
            var columnName =
                GetNullableString((char*)sqlite3_column_name16(stmt, column)) ?? string.Empty;
            var baseCatalogName = hasColumnMetadata
                ? GetNullableString((char*)sqlite3_column_database_name16(stmt, column))
                : null;
            var baseTableName = hasColumnMetadata
                ? GetNullableString((char*)sqlite3_column_table_name16(stmt, column))
                : null;
            var baseColumnName = hasColumnMetadata
                ? GetNullableString((char*)sqlite3_column_origin_name16(stmt, column))
                : null;

            var metadata =
                baseTableName is not null && baseColumnName is not null
                    ? GetTableColumnMetadata(column)
                    : default;
            var dataTypeName =
                metadata.DataTypeName
                ?? GetNullableString((char*)sqlite3_column_decltype16(stmt, column))
                ?? "BLOB";

            var row = result.NewRow();
            row[SchemaTableColumn.AllowDBNull] = !metadata.NotNull;
            row["BaseCatalogName"] = (object?)baseCatalogName ?? DBNull.Value;
            row[SchemaTableColumn.BaseColumnName] = (object?)baseColumnName ?? DBNull.Value;
            row[SchemaTableColumn.BaseSchemaName] = DBNull.Value;
            row["BaseServerName"] = connection.DatabasePath;
            row[SchemaTableColumn.BaseTableName] = (object?)baseTableName ?? DBNull.Value;
            row[SchemaTableColumn.ColumnName] = columnName;
            row[SchemaTableColumn.ColumnOrdinal] = column;
            row[SchemaTableColumn.ColumnSize] = -1;
            row[SchemaTableColumn.DataType] = GetDefaultClrType(dataTypeName);
            row["DataTypeName"] = dataTypeName;
            row["IsAliased"] = baseColumnName is null || columnName != baseColumnName;
            row[SchemaTableOptionalColumn.IsAutoIncrement] = metadata.AutoIncrement;
            row[SchemaTableColumn.IsExpression] = baseColumnName is null;
            row[SchemaTableColumn.IsKey] = metadata.PrimaryKey;
            row[SchemaTableColumn.IsUnique] = metadata.PrimaryKey;
            row[SchemaTableColumn.NumericPrecision] = DBNull.Value;
            row[SchemaTableColumn.NumericScale] = DBNull.Value;
            result.Rows.Add(row);
        }

        return result;
    }

    static DataTable CreateSchemaTable()
    {
        var result = new DataTable("SchemaTable");
        result.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool));
        result.Columns.Add("BaseCatalogName", typeof(string));
        result.Columns.Add(SchemaTableColumn.BaseColumnName, typeof(string));
        result.Columns.Add(SchemaTableColumn.BaseSchemaName, typeof(string));
        result.Columns.Add("BaseServerName", typeof(string));
        result.Columns.Add(SchemaTableColumn.BaseTableName, typeof(string));
        result.Columns.Add(SchemaTableColumn.ColumnName, typeof(string));
        result.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));
        result.Columns.Add(SchemaTableColumn.ColumnSize, typeof(int));
        result.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
        result.Columns.Add("DataTypeName", typeof(string));
        result.Columns.Add("IsAliased", typeof(bool));
        result.Columns.Add(SchemaTableOptionalColumn.IsAutoIncrement, typeof(bool));
        result.Columns.Add(SchemaTableColumn.IsExpression, typeof(bool));
        result.Columns.Add(SchemaTableColumn.IsKey, typeof(bool));
        result.Columns.Add(SchemaTableColumn.IsUnique, typeof(bool));
        result.Columns.Add(SchemaTableColumn.NumericPrecision, typeof(short));
        result.Columns.Add(SchemaTableColumn.NumericScale, typeof(short));
        return result;
    }

    readonly bool HasColumnMetadata()
    {
        ReadOnlySpan<byte> option = "ENABLE_COLUMN_METADATA"u8;
        fixed (byte* optionPtr = option)
        {
            return sqlite3_compileoption_used(optionPtr) != 0;
        }
    }

    /// <summary>
    /// Gets metadata for a result-set column.
    /// </summary>
    public readonly SqliteColumnMetadata GetColumnMetadata(int column)
    {
        connection.ThrowIfDisposed();
        return new SqliteColumnMetadata(connection.db, stmt, column, HasColumnMetadata());
    }

    readonly TableColumnMetadata GetTableColumnMetadata(int column)
    {
        var databasePtr = sqlite3_column_database_name(stmt, column);
        var tablePtr = sqlite3_column_table_name(stmt, column);
        var columnPtr = sqlite3_column_origin_name(stmt, column);
        if (tablePtr == null || columnPtr == null)
            return default;

        byte* dataTypePtr;
        byte* collationPtr;
        int notNull;
        int primaryKey;
        int autoIncrement;
        var code = sqlite3_table_column_metadata(
            connection.db,
            databasePtr,
            tablePtr,
            columnPtr,
            &dataTypePtr,
            &collationPtr,
            &notNull,
            &primaryKey,
            &autoIncrement
        );
        return code == Constants.SQLITE_OK
            ? new TableColumnMetadata(
                Marshal.PtrToStringUTF8((nint)dataTypePtr),
                notNull != 0,
                primaryKey != 0,
                autoIncrement != 0
            )
            : default;
    }

    static string? GetNullableString(char* ptr)
    {
        if (ptr == null)
            return null;
        var length = 0;
        while (ptr[length] != 0)
            length++;
        return new string(ptr, 0, length);
    }

    static Type GetDefaultClrType(string dataTypeName)
    {
        var typeName = dataTypeName.ToUpperInvariant();
        if (typeName.Contains("INT"))
            return typeof(long);
        if (typeName.Contains("CHAR") || typeName.Contains("CLOB") || typeName.Contains("TEXT"))
            return typeof(string);
        if (typeName.Contains("BLOB") || typeName.Length == 0)
            return typeof(byte[]);
        if (typeName.Contains("REAL") || typeName.Contains("FLOA") || typeName.Contains("DOUB"))
            return typeof(double);
        return typeof(decimal);
    }

    readonly struct TableColumnMetadata
    {
        public readonly string? DataTypeName;
        public readonly bool NotNull;
        public readonly bool PrimaryKey;
        public readonly bool AutoIncrement;

        public TableColumnMetadata(
            string? dataTypeName,
            bool notNull,
            bool primaryKey,
            bool autoIncrement
        )
        {
            DataTypeName = dataTypeName;
            NotNull = notNull;
            PrimaryKey = primaryKey;
            AutoIncrement = autoIncrement;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetInt(int column)
    {
        connection.ThrowIfDisposed();
        return sqlite3_column_int(stmt, column);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long GetInt64(int column)
    {
        connection.ThrowIfDisposed();
        return sqlite3_column_int64(stmt, column);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetDouble(int column)
    {
        connection.ThrowIfDisposed();
        return sqlite3_column_double(stmt, column);
    }

    public readonly void Dispose()
    {
        if (connection.IsDisposed)
            return;

        if (finalizeStatements)
        {
            for (var i = 0; i < statements.Count; i++)
            {
                if (statements.Buffer[i] == IntPtr.Zero)
                    continue;

                sqlite3_finalize((sqlite3_stmt*)statements.Buffer[i]);
                statements.Buffer[i] = IntPtr.Zero;
            }
        }
        else
        {
            for (var i = 0; i < statements.Count; i++)
            {
                if (statements.Buffer[i] != IntPtr.Zero)
                {
                    sqlite3_reset((sqlite3_stmt*)statements.Buffer[i]);
                }
            }
        }

        if (returnStatements)
        {
            ArrayPool<IntPtr>.Shared.Return(statements.Buffer, clearArray: true);
        }
    }
}
