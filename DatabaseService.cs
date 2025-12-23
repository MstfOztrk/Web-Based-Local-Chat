using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace ChatApp.Services
{
    public class ChannelModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Desc { get; set; }
    }

    public class MessageModel
    {
        public string Id { get; set; }
        public string Nick { get; set; }
        public string Content { get; set; }
        public string Timestamp { get; set; }
    }

    public class DatabaseService
    {
        private string _connStr = "Data Source=chat.db";
        private readonly object _lock = new object();

        public DatabaseService()
        {
            Initialize();
        }

        private void Initialize()
        {
            lock (_lock)
            {
                using var conn = new SqliteConnection(_connStr);
                conn.Open();
                Execute(conn, "CREATE TABLE IF NOT EXISTS Channels (Id TEXT PRIMARY KEY, Name TEXT, Icon TEXT, Description TEXT)");
                Execute(conn, "CREATE TABLE IF NOT EXISTS Messages (Id TEXT PRIMARY KEY, ChannelId TEXT, Nick TEXT, Content TEXT, UserIp TEXT, Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP)");

                using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Channels", conn);
                long count = (long)cmd.ExecuteScalar();
                if (count == 0)
                {
                    CreateChannel("Genel", "💬", "Genel Sohbet");
                }
            }
        }

        private void Execute(SqliteConnection conn, string sql)
        {
            using var cmd = new SqliteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        public void CreateChannel(string name, string icon, string desc)
        {
            lock (_lock)
            {
                using var conn = new SqliteConnection(_connStr);
                conn.Open();
                using var cmd = new SqliteCommand("INSERT INTO Channels VALUES (@Id, @Name, @Icon, @Desc)", conn);
                cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Icon", icon ?? "💬");
                cmd.Parameters.AddWithValue("@Desc", desc ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteChannel(string id)
        {
            lock (_lock)
            {
                using var conn = new SqliteConnection(_connStr);
                conn.Open();
                using var cmdMsg = new SqliteCommand("DELETE FROM Messages WHERE ChannelId=@Id", conn);
                cmdMsg.Parameters.AddWithValue("@Id", id);
                cmdMsg.ExecuteNonQuery();

                using var cmdCh = new SqliteCommand("DELETE FROM Channels WHERE Id=@Id", conn);
                cmdCh.Parameters.AddWithValue("@Id", id);
                cmdCh.ExecuteNonQuery();
            }
        }

        public List<ChannelModel> GetChannels()
        {
            var list = new List<ChannelModel>();
            lock (_lock)
            {
                using var conn = new SqliteConnection(_connStr);
                conn.Open();
                using var cmd = new SqliteCommand("SELECT * FROM Channels", conn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new ChannelModel
                    {
                        Id = rdr.GetString(0),
                        Name = rdr.GetString(1),
                        Icon = rdr.GetString(2),
                        Desc = rdr.GetString(3)
                    });
                }
            }
            return list;
        }

        public void SaveMessage(string cid, string nick, string content, string ip)
        {
            lock (_lock)
            {
                using var conn = new SqliteConnection(_connStr);
                conn.Open();
                using var cmd = new SqliteCommand("INSERT INTO Messages (Id, ChannelId, Nick, Content, UserIp) VALUES (@Id, @Cid, @Nick, @Content, @Ip)", conn);
                cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@Cid", cid);
                cmd.Parameters.AddWithValue("@Nick", nick);
                cmd.Parameters.AddWithValue("@Content", content);
                cmd.Parameters.AddWithValue("@Ip", ip);
                cmd.ExecuteNonQuery();
            }
        }

        public List<MessageModel> GetMessages(string cid)
        {
            var list = new List<MessageModel>();
            lock (_lock)
            {
                using var conn = new SqliteConnection(_connStr);
                conn.Open();
                using var cmd = new SqliteCommand("SELECT Id, Nick, Content, Timestamp FROM Messages WHERE ChannelId=@Cid ORDER BY Timestamp DESC LIMIT 50", conn);
                cmd.Parameters.AddWithValue("@Cid", cid);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    list.Add(new MessageModel
                    {
                        Id = rdr.GetString(0),
                        Nick = rdr.GetString(1),
                        Content = rdr.GetString(2),
                        Timestamp = rdr.GetDateTime(3).ToString("HH:mm")
                    });
                }
            }
            // Reverse not available on List via explicit loop requirement, using standard reverse
            var reversed = new List<MessageModel>();
            for(int i = list.Count - 1; i >= 0; i--)
            {
                reversed.Add(list[i]);
            }
            return reversed;
        }

        public void DeleteMessage(string id)
        {
            lock (_lock)
            {
                using var conn = new SqliteConnection(_connStr);
                conn.Open();
                using var cmd = new SqliteCommand("DELETE FROM Messages WHERE Id=@Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }
}