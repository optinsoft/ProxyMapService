using Microsoft.Data.Sqlite;

namespace ProxyMapService.Proxy.Cache
{
    public class CacheRepository
    {
        private readonly string _connectionString;

        public string ConnectionString { get => _connectionString; }

        public CacheRepository(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public async Task InitAsync()
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                CREATE TABLE IF NOT EXISTS cache_entries
                (
                    key TEXT PRIMARY KEY,
                    host TEXT,
                    url TEXT,
                    etag TEXT,
                    header_length INTEGER,
                    content_length INTEGER,
                    content_type TEXT,
                    created_at TEXT,
                    last_access TEXT
                );
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task InsertAsync(CacheEntry entry)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText =
                """
                INSERT OR REPLACE INTO cache_entries
                (key,host,url,etag,header_length,content_length,content_type,created_at,last_access)
                VALUES
                ($key,$host,$url,$etag,$hlen,$len,$type,$created,$access)
                """;

            cmd.Parameters.AddWithValue("$key", entry.Key);
            cmd.Parameters.AddWithValue("$host", entry.Host);
            cmd.Parameters.AddWithValue("$url", entry.Url);
            cmd.Parameters.AddWithValue("$etag", entry.ETag ?? "");
            cmd.Parameters.AddWithValue("$hlen", entry.HeaderLength);
            cmd.Parameters.AddWithValue("$len", entry.ContentLength);
            cmd.Parameters.AddWithValue("$type", entry.ContentType ?? "");
            cmd.Parameters.AddWithValue("$created", entry.CreatedAt);
            cmd.Parameters.AddWithValue("$access", entry.LastAccess);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<CacheEntry?> GetAsync(string key, string filePath)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                SELECT key,host,url,etag,header_length,content_length,content_type,created_at,last_access 
                FROM cache_entries 
                WHERE key=$key                
                """;
            cmd.Parameters.AddWithValue("$key", key);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var dbKey = reader.GetString(0);            

            return new CacheEntry
            {
                Key = dbKey,
                Host = reader.GetString(1),
                Url = reader.GetString(2),
                ETag = reader.GetString(3),
                FilePath = filePath,
                HeaderLength = reader.GetInt32(4),
                ContentLength = reader.GetInt64(5),
                ContentType = reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                LastAccess = reader.GetDateTime(8)
            };
        }

        public async Task UpdateAccessAsync(string key)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText =
                "UPDATE cache_entries SET last_access=$access WHERE key=$key";

            cmd.Parameters.AddWithValue("$access", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("$key", key);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(string key)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM cache_entries WHERE key=$key";
            cmd.Parameters.AddWithValue("$key", key);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
