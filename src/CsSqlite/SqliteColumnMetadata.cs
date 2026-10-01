using static CsSqlite.NativeMethods;

namespace CsSqlite;

/// <summary>
/// A view of metadata for one SQLite result-set column.
/// </summary>
public readonly unsafe ref struct SqliteColumnMetadata
{
    readonly sqlite3* db;
    readonly sqlite3_stmt* statement;
    readonly int column;
    readonly bool hasColumnMetadata;

    internal SqliteColumnMetadata(
        sqlite3* db,
        sqlite3_stmt* statement,
        int column,
        bool hasColumnMetadata
    )
    {
        this.db = db;
        this.statement = statement;
        this.column = column;
        this.hasColumnMetadata = hasColumnMetadata;
    }

    public ReadOnlySpan<char> Name => GetSpan((char*)sqlite3_column_name16(statement, column));
    public ReadOnlySpan<char> DeclaredType =>
        GetSpan((char*)sqlite3_column_decltype16(statement, column));
    public ReadOnlySpan<char> BaseCatalogName =>
        hasColumnMetadata ? GetSpan((char*)sqlite3_column_database_name16(statement, column)) : [];
    public ReadOnlySpan<char> BaseTableName =>
        hasColumnMetadata ? GetSpan((char*)sqlite3_column_table_name16(statement, column)) : [];
    public ReadOnlySpan<char> BaseColumnName =>
        hasColumnMetadata ? GetSpan((char*)sqlite3_column_origin_name16(statement, column)) : [];

    public bool TryGetTableMetadata(out SqliteTableColumnMetadata metadata)
    {
        if (!hasColumnMetadata)
        {
            metadata = default;
            return false;
        }

        var databaseName = sqlite3_column_database_name(statement, column);
        var tableName = sqlite3_column_table_name(statement, column);
        var columnName = sqlite3_column_origin_name(statement, column);
        if (tableName == null || columnName == null)
        {
            metadata = default;
            return false;
        }

        byte* dataType;
        byte* collation;
        int notNull;
        int primaryKey;
        int autoIncrement;
        var code = sqlite3_table_column_metadata(
            db,
            databaseName,
            tableName,
            columnName,
            &dataType,
            &collation,
            &notNull,
            &primaryKey,
            &autoIncrement
        );
        metadata =
            code == Constants.SQLITE_OK
                ? new SqliteTableColumnMetadata(
                    dataType,
                    collation,
                    notNull != 0,
                    primaryKey != 0,
                    autoIncrement != 0
                )
                : default;
        return code == Constants.SQLITE_OK;
    }

    static ReadOnlySpan<char> GetSpan(char* value)
    {
        if (value == null)
            return [];
        var length = 0;
        while (value[length] != 0)
            length++;
        return new ReadOnlySpan<char>(value, length);
    }
}
