using Microsoft.Data.Sqlite;

namespace manHours;

class Database : IDisposable
{
    readonly SqliteConnection _conn;
    public string FilePath { get; }

    public Database(string path)
    {
        FilePath = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
    }

    public bool TableExists(string table)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@t";
        cmd.Parameters.AddWithValue("@t", table);
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    public (string[] Cols, object?[][] Rows) Select(string table, string where = "")
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{table}\"" + (where.Length > 0 ? " WHERE " + where : "");
        using var rdr = cmd.ExecuteReader();
        var cols = Enumerable.Range(0, rdr.FieldCount).Select(rdr.GetName).ToArray();
        var rows = new List<object?[]>();
        while (rdr.Read())
        {
            var row = new object?[rdr.FieldCount];
            for (int i = 0; i < rdr.FieldCount; i++)
                row[i] = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
            rows.Add(row);
        }
        return (cols, rows.ToArray());
    }

    public (string[] Cols, string[][] Rows) SelectStrings(string table, string where = "")
    {
        var (cols, rows) = Select(table, where);
        return (cols, rows.Select(r => r.Select(v => v?.ToString() ?? "").ToArray()).ToArray());
    }

    public int Execute(string sql, params object?[] args)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        for (int i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    public object? Scalar(string sql, params object?[] args)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        for (int i = 0; i < args.Length; i++)
            cmd.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);
        var v = cmd.ExecuteScalar();
        return v == DBNull.Value ? null : v;
    }

    public string? GetSetting(string key) =>
        Scalar("SELECT \"값\" FROM \"설정\" WHERE \"키\"=@p0", key)?.ToString();

    public void SetSetting(string key, string value)
    {
        Execute("DELETE FROM \"설정\" WHERE \"키\"=@p0", key);
        Execute("INSERT INTO \"설정\" (\"키\",\"값\") VALUES (@p0,@p1)", key, value);
    }

    public void EnsureTable(string table, string[] cols)
    {
        var colDefs = string.Join(", ", cols.Select(c => $"\"{c}\" TEXT"));
        Execute($"CREATE TABLE IF NOT EXISTS \"{table}\" ({colDefs})");

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read()) existing.Add(rdr.GetString(1));
        }
        foreach (var col in cols)
            if (!existing.Contains(col))
                Execute($"ALTER TABLE \"{table}\" ADD COLUMN \"{col}\" TEXT DEFAULT \"\"");
    }

    public void InsertRow(string table, string[] cols, string[] values)
    {
        var colPart   = string.Join(", ", cols.Select(c => $"\"{c}\""));
        var paramPart = string.Join(", ", Enumerable.Range(0, cols.Length).Select(i => $"@p{i}"));
        Execute($"INSERT INTO \"{table}\" ({colPart}) VALUES ({paramPart})",
            values.Cast<object?>().ToArray());
    }

    public void ClearAndInsert(string table, string[] cols, IEnumerable<string[]> rows)
    {
        Execute($"DELETE FROM \"{table}\"");
        foreach (var row in rows) InsertRow(table, cols, row);
    }

    public void Dispose() => _conn.Dispose();
}
