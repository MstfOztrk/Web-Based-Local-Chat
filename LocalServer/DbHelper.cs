using System;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace ChatApp
{
    public static class DbHelper
    {
        private static string dbFile = "chat.db";
        private static string connectionString = $"Data Source={dbFile}";
        private static readonly object _dbLock = new object();

        public static void InitializeDatabase()
        {
            lock (_dbLock)
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"
                        CREATE TABLE IF NOT EXISTS Messages (
                            Id TEXT PRIMARY KEY,
                            Nick TEXT,
                            Content TEXT,
                            UserIp TEXT,
                            Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";
                    using (var cmd = new SqliteCommand(sql, conn)) cmd.ExecuteNonQuery();
                }
            }
        }

        public static void SaveMessage(string id, string nick, string content, string ip)
        {
            lock (_dbLock)
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Messages (Id, Nick, Content, UserIp) VALUES (@Id, @Nick, @Content, @UserIp)";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Nick", nick);
                        cmd.Parameters.AddWithValue("@Content", content);
                        cmd.Parameters.AddWithValue("@UserIp", ip ?? "0.0.0.0");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void DeleteMessage(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            lock (_dbLock)
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand("DELETE FROM Messages WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static string GetMessagesHtml(int limit = 50)
        {
            StringBuilder sb = new StringBuilder();
            lock (_dbLock)
            {
                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();
                        string sql = "SELECT Id, Content FROM Messages ORDER BY Timestamp DESC LIMIT @Limit";
                        using (var cmd = new SqliteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Limit", limit);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string id = reader["Id"].ToString();
                                    string content = reader["Content"].ToString();
                                    
                                    bool isMedia = content.Contains("<img") || content.Contains("<video") || content.Contains("<audio");
                                    string cssClass = isMedia ? "message media-msg" : "message";

                                    sb.Append($@"
                                    <div class='{cssClass}' data-id='{id}'>
                                        {content}
                                        <button class='delete-btn' data-id='{id}'>×</button>
                                    </div>");
                                }
                            }
                        }
                    }
                }
                catch 
                {
                    return "<div class='message'>Veritabanı hatası.</div>";
                }
            }
            return sb.ToString();
        }
    }
}