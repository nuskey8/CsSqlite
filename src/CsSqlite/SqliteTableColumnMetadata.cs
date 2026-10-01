namespace CsSqlite;

/// <summary>
/// A view of constraints and type information for an originating table column.
/// </summary>
public readonly unsafe ref struct SqliteTableColumnMetadata
{
    readonly byte* dataType;
    readonly byte* collation;

    internal SqliteTableColumnMetadata(
        byte* dataType,
        byte* collation,
        bool notNull,
        bool primaryKey,
        bool autoIncrement
    )
    {
        this.dataType = dataType;
        this.collation = collation;
        NotNull = notNull;
        PrimaryKey = primaryKey;
        AutoIncrement = autoIncrement;
    }

    public bool NotNull { get; }
    public bool PrimaryKey { get; }
    public bool AutoIncrement { get; }
    public ReadOnlySpan<byte> DataType => GetSpan(dataType);
    public ReadOnlySpan<byte> Collation => GetSpan(collation);

    static ReadOnlySpan<byte> GetSpan(byte* value)
    {
        if (value == null)
            return [];
        var length = 0;
        while (value[length] != 0)
            length++;
        return new ReadOnlySpan<byte>(value, length);
    }
}
