using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace ChatApp
{
    public class MessageModel
    {
        public string Id { get; set; }
        public string Nick { get; set; }
        public string Content { get; set; }
        public string UserIp { get; set; }
        public string Timestamp { get; set; }
    }

    public class ChannelModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; } 
        public string Desc { get; set; }
        public int UserCount { get; set; }
    }

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
                    
                    string sqlMsg = @"
                        CREATE TABLE IF NOT EXISTS Messages (
                            Id TEXT PRIMARY KEY,
                            ChannelId TEXT,
                            Nick TEXT,
                            Content TEXT,
                            UserIp TEXT,
                            Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";
                    using (var cmd = new SqliteCommand(sqlMsg, conn)) cmd.ExecuteNonQuery();

                    string sqlChan = @"
                        CREATE TABLE IF NOT EXISTS Channels (
                            Id TEXT PRIMARY KEY,
                            Name TEXT,
                            Icon TEXT,
                            Description TEXT
                        )";
                    using (var cmd = new SqliteCommand(sqlChan, conn)) cmd.ExecuteNonQuery();

                    using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Channels", conn))
                    {
                        long count = (long)cmd.ExecuteScalar();
                        if (count == 0)
                        {
                            CreateChannel(Guid.NewGuid().ToString(), "Genel Sohbet", "💬", "Her telden muhabbet");
                            CreateChannel(Guid.NewGuid().ToString(), "Oyun", "🎮", "CS, LoL, Valo tayfa");
                        }
                    }
                }
            }
        }

        public static void CreateChannel(string id, string name, string icon, string desc)
        {
            lock (_dbLock)
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Channels (Id, Name, Icon, Description) VALUES (@Id, @Name, @Icon, @Desc)";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Icon", icon);
                        cmd.Parameters.AddWithValue("@Desc", desc);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // --- YENİ: KANAL SİLME ---
        public static void DeleteChannel(string id)
        {
            lock (_dbLock)
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    // Önce kanalın mesajlarını sil
                    using (var cmd = new SqliteCommand("DELETE FROM Messages WHERE ChannelId = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                    // Sonra kanalı sil
                    using (var cmd = new SqliteCommand("DELETE FROM Channels WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
        // -------------------------

        public static List<ChannelModel> GetChannels()
        {
            var list = new List<ChannelModel>();
            lock (_dbLock)
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    string sql = "SELECT * FROM Channels";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new ChannelModel
                                {
                                    Id = reader["Id"].ToString(),
                                    Name = reader["Name"].ToString(),
                                    Icon = reader["Icon"].ToString(),
                                    Desc = reader["Description"].ToString(),
                                    UserCount = 0 
                                });
                            }
                        }
                    }
                }
            }
            return list;
        }

        public static string GetChannelName(string id)
        {
            lock (_dbLock)
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand("SELECT Name FROM Channels WHERE Id=@Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        var result = cmd.ExecuteScalar();
                        return result != null ? result.ToString() : "Bilinmeyen Oda";
                    }
                }
            }
        }

        public static void SaveMessage(string id, string channelId, string nick, string content, string ip)
        {
            lock (_dbLock)
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO Messages (Id, ChannelId, Nick, Content, UserIp) VALUES (@Id, @ChannelId, @Nick, @Content, @UserIp)";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@ChannelId", channelId);
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

        public static string GetMessagesHtml(string channelId, int limit = 50)
        {
            StringBuilder sb = new StringBuilder();
            lock (_dbLock)
            {
                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();
                        string sql = "SELECT Id, Content FROM Messages WHERE ChannelId = @ChannelId ORDER BY Timestamp DESC LIMIT @Limit";
                        using (var cmd = new SqliteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@ChannelId", channelId);
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
                catch { return "<div class='message'>Veritabanı hatası.</div>"; }
            }
            return sb.ToString();
        }

        public static List<MessageModel> GetMessagesList(string channelId, int limit = 50)
        {
            var list = new List<MessageModel>();
            lock (_dbLock)
            {
                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();
                        string sql = "SELECT Id, Nick, Content, UserIp, Timestamp FROM Messages WHERE ChannelId = @ChannelId ORDER BY Timestamp DESC LIMIT @Limit";
                        using (var cmd = new SqliteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@ChannelId", channelId);
                            cmd.Parameters.AddWithValue("@Limit", limit);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    list.Add(new MessageModel
                                    {
                                        Id = reader["Id"].ToString(),
                                        Nick = reader["Nick"].ToString(),
                                        Content = reader["Content"].ToString(),
                                        UserIp = reader["UserIp"].ToString(),
                                        Timestamp = Convert.ToDateTime(reader["Timestamp"]).ToString("HH:mm")
                                    });
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return list;
        }
    }
}