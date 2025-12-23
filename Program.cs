using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Threading.Tasks;
using System.Net.Http;
using HttpMultipartParser;
using System.Collections.Concurrent;

namespace ChatApp
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static ConcurrentDictionary<string, (string ChannelId, DateTime LastSeen)> OnlineUsers = new ConcurrentDictionary<string, (string, DateTime)>();

        static void Main(string[] args)
        {
            DbHelper.InitializeDatabase();

            const string prefix = "http://+:8080/";
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine("Sunucu Aktif: " + prefix);
                Console.WriteLine("Mod: GÜVENLİ (Security Patch v1.0)");
                Console.WriteLine("--------------------------------------------------");
                ListenForRequests(listener);
            }
            catch (Exception ex) { Console.WriteLine("HATA: " + ex.Message); }
        }

        static void ListenForRequests(HttpListener listener)
        {
            while (true)
            {
                try 
                { 
                    HttpListenerContext context = listener.GetContext();
                    Task.Run(() => HandleRequest(context));
                }
                catch (Exception ex) { Console.WriteLine("Listener: " + ex.Message); }
            }
        }

        static void HandleRequest(HttpListenerContext context)
        {
            try 
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                
                string path = request.Url.AbsolutePath.ToLower();

                if (path.StartsWith("/uploads/"))
                {
                    ServeUploadedFile(response, path);
                    return;
                }

                switch (path)
                {
                    case "/":
                        SendResponse(response, HtmlTemplates.GetHomePage());
                        break;
                    case "/chat":
                        if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                            ProcessChatRequest(request, response);
                        else
                            response.Redirect("/");
                        break;
                    case "/delete":
                        if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                            HandleDeleteRequest(request, response);
                        break;

                    case "/api/messages":
                        string cid = request.QueryString["channelId"];
                        string nick = request.QueryString["nick"] ?? "Anonim"; 
                        string userKey = request.RemoteEndPoint.Address.ToString() + "_" + nick;

                        if(!string.IsNullOrEmpty(cid))
                        {
                            OnlineUsers[userKey] = (cid, DateTime.Now);

                            var msgs = DbHelper.GetMessagesList(cid, 100);
                            var jb = new StringBuilder("[");
                            for(int i=0; i < msgs.Count; i++)
                            {
                                var m = msgs[i];
                                // GÜVENLİK: İçeriği ve Nicki encode ediyoruz (XSS Koruması)
                                string sc = HttpUtility.JavaScriptStringEncode(m.Content); // Content zaten HTML içeriyor, DbHelper'da encode edilmişti
                                string sn = HttpUtility.JavaScriptStringEncode(m.Nick);
                                jb.Append($"{{\"Id\":\"{m.Id}\", \"Nick\":\"{sn}\", \"Content\":\"{sc}\", \"Timestamp\":\"{m.Timestamp}\"}}");
                                if(i < msgs.Count - 1) jb.Append(",");
                            }
                            jb.Append("]");
                            SendJsonResponse(response, jb.ToString());
                        }
                        else SendJsonResponse(response, "[]");
                        break;

                    case "/api/channels":
                        var chans = DbHelper.GetChannels();
                        var activeTime = DateTime.Now.AddSeconds(-15);

                        // Temizlik (TryRemove ile daha güvenli)
                        foreach(var key in OnlineUsers.Keys) 
                        {
                            if(OnlineUsers[key].LastSeen < activeTime) 
                                OnlineUsers.TryRemove(key, out _);
                        }

                        var cb = new StringBuilder("[");
                        for(int i=0; i < chans.Count; i++)
                        {
                            var c = chans[i];
                            int count = OnlineUsers.Values.Count(u => u.ChannelId == c.Id && u.LastSeen > activeTime);
                            
                            string safeName = HttpUtility.JavaScriptStringEncode(c.Name);
                            string safeDesc = HttpUtility.JavaScriptStringEncode(c.Desc);
                            cb.Append($"{{\"Id\":\"{c.Id}\", \"Name\":\"{safeName}\", \"Icon\":\"{c.Icon}\", \"Desc\":\"{safeDesc}\", \"UserCount\":{count}}}");
                            if(i < chans.Count - 1) cb.Append(",");
                        }
                        cb.Append("]");
                        SendJsonResponse(response, cb.ToString());
                        break;

                    case "/api/create_channel":
                         if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                            HandleCreateChannel(request, response);
                        break;
                    
                    case "/api/enter_room":
                         if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                            HandleEnterRoom(request, response);
                        break;
                    
                    case "/api/delete_channel":
                         if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                            HandleDeleteChannel(request, response);
                        break;
                    
                    case "/api/leave_room":
                         if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                            HandleLeaveRoom(request, response);
                        break;

                    default:
                        SendResponse(response, "<html><body><h1>404</h1></body></html>");
                        break;
                }
            }
            catch(Exception ex) { Console.WriteLine("Handle: " + ex.Message); }
        }

        // ... (HandleDeleteChannel, HandleEnterRoom, ExtractJsonValue AYNI KALSIN) ...
        // Yer kaplamasın diye tekrar yazmıyorum, önceki kodla aynı.
        static void HandleDeleteChannel(HttpListenerRequest request, HttpListenerResponse response)
        {
            try { var fd = ParseFormData(request); if (fd.ContainsKey("channelId")) { DbHelper.DeleteChannel(fd["channelId"]); SendJsonResponse(response, "{\"success\": true}"); } else SendJsonResponse(response, "{\"success\": false}"); } catch { SendJsonResponse(response, "{\"success\": false}"); }
        }
        static void HandleEnterRoom(HttpListenerRequest request, HttpListenerResponse response) { SendJsonResponse(response, "{\"success\": true}"); } // Basitleştirildi, loglama öncekiyle aynı kalabilir.

        static void HandleLeaveRoom(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                var formData = ParseFormData(request);
                string nick = formData.ContainsKey("nick") ? formData["nick"] : "Anonim";
                string userKey = request.RemoteEndPoint.Address.ToString() + "_" + nick;
                
                // GÜVENLİ SİLME
                OnlineUsers.TryRemove(userKey, out _);
                
                SendJsonResponse(response, "{\"success\": true}");
            }
            catch { SendJsonResponse(response, "{\"success\": false}"); }
        }
        
        static void HandleCreateChannel(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                var parser = MultipartFormDataParser.Parse(request.InputStream);
                string name = "Yeni Oda", icon = "#", desc = "";
                foreach(var p in parser.Parameters) {
                    if(p.Name == "name") name = HttpUtility.HtmlEncode(p.Data); // XSS Koruması
                    if(p.Name == "icon") icon = HttpUtility.HtmlEncode(p.Data);
                    if(p.Name == "desc") desc = HttpUtility.HtmlEncode(p.Data);
                }
                DbHelper.CreateChannel(Guid.NewGuid().ToString(), name, icon, desc);
                SendJsonResponse(response, "{\"success\": true}");
            }
            catch { SendJsonResponse(response, "{\"success\": false}"); }
        }

        static void ProcessChatRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            string nick = "Anonim";
            string channelId = ""; 
            bool isAjax = request.Headers["X-Requested-With"] == "XMLHttpRequest";
            string newMsgHtml = "";
            string userIp = request.RemoteEndPoint.Address.ToString();

            try
            {
                // ... (Multipart parser kısmı aynı, sadece Nick ve Message'ı encode ediyoruz) ...
                if (request.ContentType != null && request.ContentType.StartsWith("multipart/form-data"))
                {
                    var parser = MultipartFormDataParser.Parse(request.InputStream);
                    foreach (var p in parser.Parameters)
                    {
                        if (p.Name == "nick") nick = HttpUtility.HtmlEncode(p.Data); // XSS Koruması: Nicki temizle
                        if (p.Name == "channelId") channelId = p.Data;
                        if (p.Name == "ajax" && p.Data == "true") isAjax = true;
                    }

                    if(string.IsNullOrEmpty(channelId)) { response.Redirect("/"); return; }
                    string message = parser.Parameters.FirstOrDefault(p => p.Name == "message")?.Data;

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        string id = Guid.NewGuid().ToString();
                        string time = DateTime.Now.ToString("HH:mm");
                        // GÜVENLİK: Mesajı ve Nicki HTML Encode yapıyoruz
                        string safeNick = nick; 
                        string safeMsg = HttpUtility.HtmlEncode(message);
                        string contentHtml = $"<span class='timestamp'>[{time}]</span> <span class='nick'>{safeNick}:</span> {safeMsg}";
                        
                        DbHelper.SaveMessage(id, channelId, safeNick, contentHtml, userIp);
                        newMsgHtml = $@"<div class='message' data-id='{id}'>{contentHtml}<button class='delete-btn' data-id='{id}'>×</button></div>";
                    }
                    
                    // (Dosya yükleme kısmı aynı, sadece nick'i safeNick kullan)
                    if (parser.Files != null && parser.Files.Count > 0)
                    {
                         // ... (Dosya kaydetme aynı) ...
                         // Sadece content oluştururken nick'i encode et
                         var file = parser.Files.First();
                         if (file.Data.Length > 0)
                         {
                            string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads");
                            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);
                            string ext = Path.GetExtension(file.FileName).ToLower();
                            string newName = Guid.NewGuid().ToString() + ext;
                            using (var fs = new FileStream(Path.Combine(uploadsDir, newName), FileMode.Create)) { file.Data.CopyTo(fs); }

                            string id = Guid.NewGuid().ToString();
                            string time = DateTime.Now.ToString("HH:mm");
                            string header = $"<div class='media-header'><span class='timestamp'>[{time}]</span> <span class='nick'>{nick}</span></div>"; // Nick zaten yukarıda encode edildi
                            string content = ""; 
                            string cssClass = "message";
                            // ... (Medya tipleri kontrolü aynı) ...
                            if (new[] { ".jpg", ".png", ".gif", ".jpeg", ".webp" }.Contains(ext)) { content = $"{header}<img src='/uploads/{newName}' alt='img'/>"; cssClass = "message media-msg"; }
                            else if (new[] { ".mp4", ".webm", ".mov" }.Contains(ext)) { content = $"{header}<video controls><source src='/uploads/{newName}' type='video/mp4'></video>"; cssClass = "message media-msg"; }
                            else if (new[] { ".mp3", ".wav", ".ogg" }.Contains(ext)) { content = $"<div class='audio-container'><div style='display:flex;flex-direction:column;font-size:10px;margin-right:5px;'><span class='nick'>{nick}</span></div><audio controls><source src='/uploads/{newName}'></audio></div>"; cssClass = "message media-msg"; }
                            else { content = $"<span class='timestamp'>[{time}]</span> <span class='nick'>{nick}:</span> <br/><div class='file-attachment'>Dosya: <a href='/uploads/{newName}' download>{HttpUtility.HtmlEncode(file.FileName)}</a></div>"; }

                            DbHelper.SaveMessage(id, channelId, nick, content, userIp);
                            newMsgHtml += $@"<div class='{cssClass}' data-id='{id}'>{content}<button class='delete-btn' data-id='{id}'>×</button></div>";
                         }
                    }
                }
                else
                {
                    // Fallback kısmı
                    var fd = ParseFormData(request);
                    if (fd.ContainsKey("nick")) nick = HttpUtility.HtmlEncode(fd["nick"]); // XSS
                    if (fd.ContainsKey("channelId")) channelId = fd["channelId"];
                    if (fd.ContainsKey("ajax")) isAjax = true;
                    if(string.IsNullOrEmpty(channelId)) { response.Redirect("/"); return; }
                    if (fd.ContainsKey("message") && !string.IsNullOrWhiteSpace(fd["message"]))
                    {
                        string id = Guid.NewGuid().ToString();
                        string time = DateTime.Now.ToString("HH:mm");
                        string contentHtml = $"<span class='timestamp'>[{time}]</span> <span class='nick'>{nick}:</span> {HttpUtility.HtmlEncode(fd["message"])}";
                        DbHelper.SaveMessage(id, channelId, nick, contentHtml, userIp);
                        newMsgHtml = $@"<div class='message' data-id='{id}'>{contentHtml}<button class='delete-btn' data-id='{id}'>×</button></div>";
                    }
                }

                if (isAjax) { string safeHtml = HttpUtility.JavaScriptStringEncode(newMsgHtml); SendJsonResponse(response, $"{{\"success\": true, \"html\": \"{safeHtml}\"}}"); }
                else SendResponse(response, HtmlTemplates.GetChatPage(nick, channelId));
            }
            catch (Exception ex) { if (isAjax) SendJsonResponse(response, "{\"success\": false, \"error\": \"" + ex.Message + "\"}"); else SendResponse(response, "Error: " + ex.Message); }
        }

        // ... (HandleDeleteRequest, ParseFormData AYNI) ...
        static void HandleDeleteRequest(HttpListenerRequest request, HttpListenerResponse response) { try { string id = ""; if (request.ContentType != null && request.ContentType.StartsWith("multipart/form-data")) { var p = MultipartFormDataParser.Parse(request.InputStream); foreach (var pa in p.Parameters) if (pa.Name == "messageId") { id = pa.Data; break; } } else { var fd = ParseFormData(request); if (fd.ContainsKey("messageId")) id = fd["messageId"]; } if (!string.IsNullOrEmpty(id)) { DbHelper.DeleteMessage(id); SendJsonResponse(response, "{\"success\": true}"); } else SendJsonResponse(response, "{\"success\": false}"); } catch { SendJsonResponse(response, "{\"success\": false}"); } }
        static Dictionary<string, string> ParseFormData(HttpListenerRequest request) { var d = new Dictionary<string, string>(); using (StreamReader r = new StreamReader(request.InputStream, request.ContentEncoding)) { string b = r.ReadToEnd(); var p = HttpUtility.ParseQueryString(b); for (int i = 0; i < p.Count; i++) d[p.Keys[i]] = p[i]; } return d; }
        
        // GÜVENLİ DOSYA SUNUMU
        static void ServeUploadedFile(HttpListenerResponse response, string path)
        {
            try
            {
                string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads");
                // Kullanıcının istediği yol (Normalize edilmiş hali)
                string reqPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.TrimStart('/')));
                
                // GÜVENLİK: Eğer istenen dosya 'uploads' klasörünün dışındaysa 404 ver!
                if (!reqPath.StartsWith(uploadsDir)) 
                {
                    response.StatusCode = 404; response.Close(); return; 
                }

                if (!File.Exists(reqPath)) { response.StatusCode = 404; response.Close(); return; }
                
                string ext = Path.GetExtension(reqPath).ToLower();
                string contentType = "application/octet-stream";
                if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
                else if (ext == ".png") contentType = "image/png";
                else if (ext == ".mp4") contentType = "video/mp4";
                else if (ext == ".mp3") contentType = "audio/mpeg";
                else if (ext == ".webm") contentType = "video/webm";
                
                byte[] bytes = File.ReadAllBytes(reqPath);
                response.ContentType = contentType;
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.OutputStream.Close();
            }
            catch { response.StatusCode = 500; response.Close(); }
        }

        static void SendResponse(HttpListenerResponse response, string html) { byte[] b = Encoding.UTF8.GetBytes(html); response.ContentType = "text/html; charset=utf-8"; response.ContentLength64 = b.Length; response.OutputStream.Write(b, 0, b.Length); response.OutputStream.Close(); }
        static void SendJsonResponse(HttpListenerResponse response, string json) { byte[] b = Encoding.UTF8.GetBytes(json); response.ContentType = "application/json; charset=utf-8"; response.ContentLength64 = b.Length; response.OutputStream.Write(b, 0, b.Length); response.OutputStream.Close(); }
    }
}