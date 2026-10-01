namespace CsSqlite.Tests;

public sealed class MultiStatementTests
{
    [Fact]
    public void ExecuteNonQuery_CharOverload_ExecutesAllStatements()
    {
        using var connection = new SqliteConnection(":memory:");

        connection.ExecuteNonQuery("""
CREATE TABLE a (id INTEGER);
CREATE TABLE b (id INTEGER);
""");

        using var reader = connection.ExecuteReader("""
SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;
""");

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString(0));
        Assert.True(reader.Read());
        Assert.Equal("b", reader.GetString(0));
        Assert.False(reader.Read());
    }

    [Fact]
    public void ExecuteNonQuery_Utf8Overload_ExecutesAllStatements()
    {
        using var connection = new SqliteConnection(":memory:");
        ReadOnlySpan<byte> sql = """
CREATE TABLE a (id INTEGER);
CREATE TABLE b (id INTEGER);
"""u8;

        connection.ExecuteNonQuery(sql);

        using var reader = connection.ExecuteReader("""
SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;
""");

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString(0));
        Assert.True(reader.Read());
        Assert.Equal("b", reader.GetString(0));
        Assert.False(reader.Read());
    }

    [Fact]
    public void ExecuteNonQuery_IgnoresTrailingWhitespaceAndComments()
    {
        using var connection = new SqliteConnection(":memory:");

        connection.ExecuteNonQuery("""
CREATE TABLE a (id INTEGER);
-- trailing comment

""");

        using var reader = connection.ExecuteReader("SELECT name FROM sqlite_master WHERE type = 'table';");
        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString(0));
        Assert.False(reader.Read());
    }

    [Fact]
    public void ExecuteReader_SupportsNextResult()
    {
        using var connection = new SqliteConnection(":memory:");

        using var reader = connection.ExecuteReader("""
SELECT 1;
SELECT 2;
""");

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt(0));
        Assert.False(reader.Read());

        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt(0));
        Assert.False(reader.Read());
        Assert.False(reader.NextResult());
    }

    [Fact]
    public void Command_ExecutesAllStatements()
    {
        using var connection = new SqliteConnection(":memory:");

        using (var command = connection.CreateCommand("""
CREATE TABLE a (id INTEGER);
CREATE TABLE b (id INTEGER);
"""))
        {
            command.ExecuteNonQuery();
        }

        using var reader = connection.ExecuteReader("""
SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;
""");

        Assert.True(reader.Read());
        Assert.Equal("a", reader.GetString(0));
        Assert.True(reader.Read());
        Assert.Equal("b", reader.GetString(0));
        Assert.False(reader.Read());
    }

    [Fact]
    public void CommandReader_SupportsNextResult()
    {
        using var connection = new SqliteConnection(":memory:");

        using var command = connection.CreateCommand("""
SELECT 1;
SELECT 2;
""");
        using var reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt(0));
        Assert.False(reader.Read());

        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt(0));
        Assert.False(reader.Read());
        Assert.False(reader.NextResult());
    }
    [Fact]
    public void Command_BindsParameterAfterBegin()
    {
        using var connection = new SqliteConnection(":memory:");
        connection.ExecuteNonQuery("CREATE TABLE Test(ID INTEGER);");
        using var command = connection.CreateCommand("BEGIN; INSERT INTO Test(ID) VALUES ($1); COMMIT;");
        Assert.Equal(1, command.Parameters.Count);
        command.Parameters.Add("$1", 42);
        command.ExecuteNonQuery();
        using var reader = connection.ExecuteReader("SELECT ID FROM Test;");
        Assert.True(reader.Read());
        Assert.Equal(42, reader.GetInt(0));
    }

    [Fact]
    public void Command_BindsNamesAcrossResults()
    {
        using var connection = new SqliteConnection(":memory:");
        using var command = connection.CreateCommand("SELECT $1, $2; SELECT $3, $1;");
        Assert.Equal(4, command.Parameters.Count);
        command.Parameters.Add("$1", 10);
        command.Parameters.Add("$2", 20);
        command.Parameters.Add("$3"u8, 30);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(10, reader.GetInt(0));
        Assert.Equal(20, reader.GetInt(1));
        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        Assert.Equal(30, reader.GetInt(0));
        Assert.Equal(10, reader.GetInt(1));
    }

    [Fact]
    public void Command_BindsGlobalIndexesAndClearsAllStatements()
    {
        using var connection = new SqliteConnection(":memory:");
        using var command = connection.CreateCommand("SELECT ?; SELECT ?, ?;");
        Assert.Equal(3, command.Parameters.Count);
        command.Parameters.Add(1, 10);
        command.Parameters.Add(2, 20);
        command.Parameters.Add(3, 30);
        using (var reader = command.ExecuteReader())
        {
            Assert.True(reader.Read());
            Assert.Equal(10, reader.GetInt(0));
            Assert.True(reader.NextResult());
            Assert.True(reader.Read());
            Assert.Equal(20, reader.GetInt(0));
            Assert.Equal(30, reader.GetInt(1));
        }
        command.Parameters.Clear();
        using var cleared = command.ExecuteReader();
        Assert.True(cleared.Read());
        Assert.Equal(SqliteType.Null, cleared.GetColumnType(0));
        Assert.True(cleared.NextResult());
        Assert.True(cleared.Read());
        Assert.Equal(SqliteType.Null, cleared.GetColumnType(0));
        Assert.Equal(SqliteType.Null, cleared.GetColumnType(1));
    }

    [Fact]
    public void Command_RejectsUnknownParameters()
    {
        using var connection = new SqliteConnection(":memory:");
        using var command = connection.CreateCommand("SELECT $1; SELECT $2;");
        Assert.Throws<SqliteException>(() => command.Parameters.Add("$missing", 1));
        Assert.Throws<SqliteException>(() => command.Parameters.Add(0, 1));
        Assert.Throws<SqliteException>(() => command.Parameters.Add(3, 1));
        using var empty = connection.CreateCommand("-- no statements");
        Assert.Equal(0, empty.Parameters.Count);
        empty.Parameters.Clear();
        Assert.Throws<SqliteException>(() => empty.Parameters.Add(1, 1));
    }
    [Fact]
    public void Command_BindsTextAndNumericValuesInLaterStatements()
    {
        using var connection = new SqliteConnection(":memory:");
        using var command = connection.CreateCommand("SELECT 0; SELECT $text, $utf8, $long, $real, length($blob);");
        command.Parameters.Add("$text", "日本語");
        command.Parameters.AddLiteral("$utf8"u8, "hello"u8);
        command.Parameters.Add("$long"u8, 5000000000L);
        command.Parameters.Add("$real", 1.5);
        command.Parameters.AddBytes("$blob", new byte[] { 1, 2, 3 });
        using var reader = command.ExecuteReader();
        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        Assert.Equal("日本語", reader.GetString(0));
        Assert.Equal("hello", reader.GetString(1));
        Assert.Equal(5000000000L, reader.GetInt64(2));
        Assert.Equal(1.5, reader.GetDouble(3));
        Assert.Equal(3, reader.GetInt(4));
    }

    [Fact]
    public void Command_IndexedBindingPreservesNumberedParameterGaps()
    {
        using var connection = new SqliteConnection(":memory:");
        using var command = connection.CreateCommand("SELECT ?3; SELECT ?;");
        Assert.Equal(4, command.Parameters.Count);
        command.Parameters.Add(3, 30);
        command.Parameters.Add(4, 40);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(30, reader.GetInt(0));
        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        Assert.Equal(40, reader.GetInt(0));
    }
}
