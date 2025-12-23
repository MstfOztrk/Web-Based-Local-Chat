using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Threading.Tasks;
using HttpMultipartParser;

namespace ChatApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Veritabanını hazırla
            DbHelper.InitializeDatabase();

            const string prefix = "http://+:8080/";
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine("Sunucu Aktif: " + prefix);
                Console.WriteLine("Dosya Yapısı: Modüler (DbHelper, HtmlTemplates)");
                Console.WriteLine("--------------------------------------------------");
                ListenForRequests(listener);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Kritik Hata: " + ex.Message);
            }
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
                catch (Exception ex) { Console.WriteLine("Listener Error: " + ex.Message); }
            }
        }

        static void HandleRequest(HttpListenerContext context)
        {
            try 
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
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
                    default:
                        SendResponse(response, "<html><body><h1>404</h1></body></html>");
                        break;
                }
            }
            catch(Exception ex) { Console.WriteLine("Handle Error: " + ex.Message); }
        }

        static void ProcessChatRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            string nick = "Anonim";
            bool isAjax = request.Headers["X-Requested-With"] == "XMLHttpRequest";
            string newMsgHtml = "";
            string userIp = request.RemoteEndPoint.Address.ToString();

            try
            {
                if (request.ContentType != null && request.ContentType.StartsWith("multipart/form-data"))
                {
                    var parser = MultipartFormDataParser.Parse(request.InputStream);
                    
                    foreach (var p in parser.Parameters)
                    {
                        if (p.Name == "nick") nick = p.Data;
                        if (p.Name == "ajax" && p.Data == "true") isAjax = true;
                    }

                    string message = "";
                    foreach(var p in parser.Parameters) { if(p.Name=="message") { message = p.Data; break; } }

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        string id = Guid.NewGuid().ToString();
                        string time = DateTime.Now.ToString("HH:mm");
                        string contentHtml = $"<span class='timestamp'>[{time}]</span> <span class='nick'>{nick}:</span> {HttpUtility.HtmlEncode(message)}";
                        
                        DbHelper.SaveMessage(id, nick, contentHtml, userIp);
                        
                        newMsgHtml = $@"<div class='message' data-id='{id}'>{contentHtml}<button class='delete-btn' data-id='{id}'>×</button></div>";
                    }

                    if (parser.Files != null && parser.Files.Count > 0)
                    {
                        var file = parser.Files.First();
                        if (file.Data.Length > 0)
                        {
                            string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads");
                            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

                            string ext = Path.GetExtension(file.FileName).ToLower();
                            string newName = Guid.NewGuid().ToString() + ext;
                            
                            using (var fs = new FileStream(Path.Combine(uploadsDir, newName), FileMode.Create))
                            {
                                file.Data.CopyTo(fs);
                            }

                            string id = Guid.NewGuid().ToString();
                            string time = DateTime.Now.ToString("HH:mm");
                            string header = $"<div class='media-header'><span class='timestamp'>[{time}]</span> <span class='nick'>{nick}</span></div>";
                            string content = "";
                            string cssClass = "message";

                            if (ext == ".jpg" || ext == ".png" || ext == ".gif" || ext == ".jpeg" || ext == ".webp")
                            {
                                content = $"{header}<img src='/uploads/{newName}' alt='img'/>";
                                cssClass = "message media-msg";
                            }
                            else if (ext == ".mp4" || ext == ".webm" || ext == ".mov")
                            {
                                content = $"{header}<video controls><source src='/uploads/{newName}' type='video/mp4'></video>";
                                cssClass = "message media-msg";
                            }
                            else if (ext == ".mp3" || ext == ".wav" || ext == ".ogg")
                            {
                                content = $"<div class='audio-container'><div style='display:flex;flex-direction:column;font-size:10px;margin-right:5px;'><span class='nick'>{nick}</span></div><audio controls><source src='/uploads/{newName}'></audio></div>";
                                cssClass = "message media-msg";
                            }
                            else
                            {
                                content = $"<span class='timestamp'>[{time}]</span> <span class='nick'>{nick}:</span> <br/><div class='file-attachment'>Dosya: <a href='/uploads/{newName}' download>{file.FileName}</a></div>";
                            }

                            DbHelper.SaveMessage(id, nick, content, userIp);
                            
                            string fileMsgHtml = $@"<div class='{cssClass}' data-id='{id}'>{content}<button class='delete-btn' data-id='{id}'>×</button></div>";
                            newMsgHtml += fileMsgHtml;
                        }
                    }
                }
                else
                {
                    var formData = ParseFormData(request);
                    if(formData.ContainsKey("nick")) nick = formData["nick"];
                    if(formData.ContainsKey("ajax")) isAjax = true;
                    
                    if (formData.ContainsKey("message") && !string.IsNullOrWhiteSpace(formData["message"]))
                    {
                        string id = Guid.NewGuid().ToString();
                        string time = DateTime.Now.ToString("HH:mm");
                        string contentHtml = $"<span class='timestamp'>[{time}]</span> <span class='nick'>{nick}:</span> {HttpUtility.HtmlEncode(formData["message"])}";
                        
                        DbHelper.SaveMessage(id, nick, contentHtml, userIp);
                        
                        newMsgHtml = $@"<div class='message' data-id='{id}'>{contentHtml}<button class='delete-btn' data-id='{id}'>×</button></div>";
                    }
                }

                if (isAjax) 
                {
                    string safeHtml = HttpUtility.JavaScriptStringEncode(newMsgHtml);
                    SendJsonResponse(response, $"{{\"success\": true, \"html\": \"{safeHtml}\"}}");
                }
                else SendResponse(response, HtmlTemplates.GetChatPage(nick));
            }
            catch (Exception ex)
            {
                if (isAjax) SendJsonResponse(response, "{\"success\": false, \"error\": \"" + ex.Message + "\"}");
                else SendResponse(response, "Error: " + ex.Message);
            }
        }

        static void HandleDeleteRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string id = "";
                if (request.ContentType != null && request.ContentType.StartsWith("multipart/form-data"))
                {
                    var parser = MultipartFormDataParser.Parse(request.InputStream);
                    foreach(var p in parser.Parameters) { if(p.Name == "messageId") { id = p.Data; break; } }
                }
                else
                {
                    var formData = ParseFormData(request);
                    if(formData.ContainsKey("messageId")) id = formData["messageId"];
                }

                if (!string.IsNullOrEmpty(id))
                {
                    DbHelper.DeleteMessage(id);
                    SendJsonResponse(response, "{\"success\": true}");
                }
                else SendJsonResponse(response, "{\"success\": false, \"error\": \"No ID\"}");
            }
            catch (Exception ex)
            {
                SendJsonResponse(response, "{\"success\": false, \"error\": \"" + ex.Message + "\"}");
            }
        }

        // Yardımcı Metodlar (Burada kalabilir)
        static Dictionary<string, string> ParseFormData(HttpListenerRequest request)
        {
            var formData = new Dictionary<string, string>();
            using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string body = reader.ReadToEnd();
                var parsed = HttpUtility.ParseQueryString(body);
                for(int i=0; i<parsed.Count; i++) formData[parsed.Keys[i]] = parsed[i];
            }
            return formData;
        }

        static void ServeUploadedFile(HttpListenerResponse response, string path)
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.TrimStart('/'));
                if (!File.Exists(filePath)) { response.StatusCode = 404; response.Close(); return; }

                string ext = Path.GetExtension(filePath).ToLower();
                string contentType = "application/octet-stream";
                if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
                else if (ext == ".png") contentType = "image/png";
                else if (ext == ".mp4") contentType = "video/mp4";
                else if (ext == ".mp3") contentType = "audio/mpeg";
                else if (ext == ".webm") contentType = "video/webm";

                byte[] bytes = File.ReadAllBytes(filePath);
                response.ContentType = contentType;
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.OutputStream.Close();
            }
            catch { response.StatusCode = 500; response.Close(); }
        }

        static void SendResponse(HttpListenerResponse response, string html)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        static void SendJsonResponse(HttpListenerResponse response, string json)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}