using System.Data.Common;

namespace CsSqlite.Tests;

public sealed class SchemaTableTests
{
    [Fact]
    public void GetColumnMetadata_ReturnsZeroAllocationViews()
    {
        using var connection = new SqliteConnection(":memory:");
        connection.ExecuteNonQuery("CREATE TABLE post (id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT);");

        using var reader = connection.ExecuteReader("SELECT id AS post_id FROM post;");
        var metadata = reader.GetColumnMetadata(0);

        Assert.True(metadata.Name.SequenceEqual("post_id"));
        Assert.True(metadata.BaseCatalogName.SequenceEqual("main"));
        Assert.True(metadata.BaseTableName.SequenceEqual("post"));
        Assert.True(metadata.BaseColumnName.SequenceEqual("id"));
        Assert.True(metadata.TryGetTableMetadata(out var tableMetadata));
        Assert.True(tableMetadata.DataType.SequenceEqual("INTEGER"u8));
        Assert.True(tableMetadata.NotNull);
        Assert.True(tableMetadata.PrimaryKey);
        Assert.True(tableMetadata.AutoIncrement);
    }

    [Fact]
    public void GetSchemaTable_ReturnsBaseColumnAndConstraintMetadata()
    {
        using var connection = new SqliteConnection(":memory:");
        connection.ExecuteNonQuery("""
            CREATE TABLE post (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                body TEXT
            );
            """);

        using var reader = connection.ExecuteReader("SELECT id AS post_id, title, body, random() AS random FROM post;");
        var schema = reader.GetSchemaTable();

        Assert.Equal(4, schema.Rows.Count);

        var id = schema.Rows[0];
        Assert.Equal("post_id", id[SchemaTableColumn.ColumnName]);
        Assert.Equal("main", id["BaseCatalogName"]);
        Assert.Equal("post", id[SchemaTableColumn.BaseTableName]);
        Assert.Equal("id", id[SchemaTableColumn.BaseColumnName]);
        Assert.Equal("INTEGER", id["DataTypeName"]);
        Assert.Equal(typeof(long), id[SchemaTableColumn.DataType]);
        Assert.False((bool)id[SchemaTableColumn.AllowDBNull]);
        Assert.True((bool)id[SchemaTableColumn.IsKey]);
        Assert.True((bool)id[SchemaTableOptionalColumn.IsAutoIncrement]);
        Assert.True((bool)id["IsAliased"]);

        var expression = schema.Rows[3];
        Assert.True(expression.IsNull("BaseCatalogName"));
        Assert.True(expression.IsNull(SchemaTableColumn.BaseTableName));
        Assert.True(expression.IsNull(SchemaTableColumn.BaseColumnName));
        Assert.True((bool)expression[SchemaTableColumn.IsExpression]);
    }
}
