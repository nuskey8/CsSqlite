using CsSqlite;

using var connection = new SqliteConnection(":memory:");
connection.Open();

connection.ExecuteNonQuery("""
CREATE TABLE IF NOT EXISTS user (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    age INTEGER NOT NULL,
    name TEXT NOT NULL
);
""");

connection.ExecuteNonQuery("""
INSERT INTO user (id, name, age)
VALUES (1, 'Alice', 18),
       (2, 'Bob', 32),
       (3, 'Charlie', 25);
""");

connection.ExecuteNonQuery(stackalloc char[1024], $"""
    INSERT INTO user (id, name, age)
    VALUES ({4}, {"Darwin"u8.ToArray():text}, {80});
    """);

using var reader = connection.ExecuteReader("""
SELECT name
FROM user
""");


while (reader.Read())
{
    Console.WriteLine($"{reader.GetString(0)}!");
}