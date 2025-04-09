using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using HttpMultipartParser;

namespace ChatApp
{
    class Program
    {
        // Mesajlar listesi ve son güncellenme zamanı
        static readonly List<MessageItem> messages = new List<MessageItem>();
        static int messageCount = 0; // Mesaj sayısını takip etmek için

        // Her mesaja unique ID atamak için
        class MessageItem
        {
            public string Id { get; } = Guid.NewGuid().ToString();
            public string Content { get; set; }
            public DateTime Timestamp { get; } = DateTime.Now;

            // ID'yi constructor'da set etme
            public MessageItem(string content)
            {
                Content = content;
            }
        }

        static void Main(string[] args)
        {
            const string prefix = "http://192.168.1.20:8080/";
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            listener.Start();
            Console.WriteLine("Sunucu çalışıyor: " + prefix);

            ListenForRequests(listener);
        }

        static void ListenForRequests(HttpListener listener)
        {
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HandleRequest(context);
            }
        }

        static void HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            string path = request.Url.AbsolutePath.ToLower();

            if (path.StartsWith("/uploads/"))
            {
                ServeUploadedFile(response, path);
                return;
            }

            // Statik dosyaları sunmak için
            if (path.StartsWith("/js/") || path.StartsWith("/css/"))
            {
                ServeStaticFile(response, path);
                return;
            }

            switch (path)
            {
                case "/":
                    SendResponse(response, GetHomePage());
                    break;
                case "/chat":
                    if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                        ProcessChatRequest(request, response);
                    else
                        SendResponse(response, GetChatPage(""));
                    break;
                case "/messages":
                    // Ajax ile yeni mesajları almak için endpoint
                    if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        // İstemciden gelen "lastCount" parametresini kontrol et
                        var queryParams = HttpUtility.ParseQueryString(request.Url.Query);
                        string lastCountStr = queryParams["lastCount"];

                        int lastCount = 0;
                        if (!string.IsNullOrEmpty(lastCountStr) && int.TryParse(lastCountStr, out lastCount))
                        {
                            // Yeni mesaj var mı kontrol et
                            if (lastCount < messageCount)
                            {
                                // Yeni mesajları JSON formatına benzer şekilde döndür
                                SendJsonResponse(response, GetNewMessagesJson(lastCount));
                            }
                            else
                            {
                                // Yeni mesaj yoksa boş dizi döndür
                                SendJsonResponse(response, "{ \"messages\": [], \"count\": " + messageCount + " }");
                            }
                        }
                        else
                        {
                            // Geçerli bir lastCount yoksa tüm mesajları döndür
                            SendJsonResponse(response, GetAllMessagesJson());
                        }
                    }
                    break;
                case "/delete":
                    if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        DeleteMessage(request, response);
                    }
                    else
                    {
                        response.StatusCode = 405; // Method Not Allowed
                        response.Close();
                    }
                    break;
                default:
                    SendResponse(response, GetNotFoundPage());
                    break;
            }
        }

        static void ServeStaticFile(HttpListenerResponse response, string path)
        {
            try
            {
                // Dosya yolunu düzeltiyoruz - path zaten /css/ veya /js/ ile başlıyor
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.TrimStart('/'));
                Console.WriteLine($"Dosya yolu: {filePath}"); // Hata ayıklama için

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Dosya bulunamadı: {filePath}"); // Hata ayıklama için
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                string contentType = "text/plain";
                string extension = Path.GetExtension(filePath).ToLower();
                if (extension == ".js") contentType = "application/javascript";
                else if (extension == ".css") contentType = "text/css";

                byte[] fileBytes = File.ReadAllBytes(filePath);
                response.ContentType = contentType;
                response.ContentLength64 = fileBytes.Length;
                using (var output = response.OutputStream)
                {
                    output.Write(fileBytes, 0, fileBytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServeStaticFile hata: " + ex.Message);
                response.StatusCode = 500;
                response.Close();
            }
        }

        static void DeleteMessage(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                var formData = ParseFormData(request);
                if (formData.ContainsKey("messageId"))
                {
                    string messageId = formData["messageId"];
                    // Case-sensitive karşılaştırma yap
                    int index = messages.FindIndex(m =>
                        m.Id.Equals(messageId, StringComparison.Ordinal)); // OrdinalIgnoreCase yerine Ordinal
                    if (index >= 0)
                    {
                        messages.RemoveAt(index);
                        messageCount--;
                        SendJsonResponse(response,
                            $@"{{""success"":true,""count"":{messageCount},""message"":""Mesaj silindi""}}");
                        return;
                    }
                }
                SendJsonResponse(response,
                    $@"{{""success"":false,""error"":""Mesaj bulunamadı""}}");
            }
            catch (Exception ex)
            {
                SendJsonResponse(response,
                    $@"{{""success"":false,""error"":""Sunucu hatası: {ex.Message.Replace("\"", "\\\"")}""}}");
            }
        }

        static void ServeUploadedFile(HttpListenerResponse response, string relativePath)
        {
            try
            {
                string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads");
                string fileName = relativePath.Substring("/uploads/".Length);
                string fullPath = Path.Combine(uploadsDir, fileName);

                if (!File.Exists(fullPath))
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                string contentType = "application/octet-stream";
                string extension = Path.GetExtension(fullPath).ToLower();
                if (extension == ".jpg" || extension == ".jpeg") contentType = "image/jpeg";
                else if (extension == ".png") contentType = "image/png";
                else if (extension == ".gif") contentType = "image/gif";

                byte[] fileBytes = File.ReadAllBytes(fullPath);
                response.ContentType = contentType;
                response.ContentLength64 = fileBytes.Length;
                using (var output = response.OutputStream)
                {
                    output.Write(fileBytes, 0, fileBytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServeStaticFile hata: " + ex.Message);
                response.StatusCode = 500;
                response.Close();
            }
        }

        static void ProcessChatRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            string nick = "";
            string message = "";
            bool isAjaxRequest = false;

            // Ajax isteği mi kontrol et
            var headers = request.Headers;
            if (headers["X-Requested-With"] == "XMLHttpRequest")
            {
                isAjaxRequest = true;
            }

            if (request.ContentType != null && request.ContentType.StartsWith("multipart/form-data"))
            {
                try
                {
                    var parser = MultipartFormDataParser.Parse(request.InputStream);

                    var paramNick = parser.Parameters.FirstOrDefault(p => p.Name == "nick");
                    if (paramNick == null || string.IsNullOrWhiteSpace(paramNick.Data))
                    {
                        SendResponse(response, "<html><body><h1>Hata: Nick alanı boş!</h1></body></html>");
                        return;
                    }
                    nick = paramNick.Data;

                    // Ajax isteği olup olmadığını kontrol et
                    var paramAjax = parser.Parameters.FirstOrDefault(p => p.Name == "ajax");
                    if (paramAjax != null && paramAjax.Data == "true")
                    {
                        isAjaxRequest = true;
                    }

                    var paramMessage = parser.Parameters.FirstOrDefault(p => p.Name == "message");
                    if (paramMessage != null && !string.IsNullOrEmpty(paramMessage.Data))
                    {
                        message = paramMessage.Data;
                        string timestamp = DateTime.Now.ToString("HH:mm:ss");
                        string msgContent = $"<span class='timestamp'>[{timestamp}]</span> <span class='nick'>{nick}:</span> {HttpUtility.HtmlEncode(message)}";
                        var msgItem = new MessageItem(msgContent);
                        messages.Insert(0, msgItem);
                        messageCount++;
                    }

                    if (parser.Files != null && parser.Files.Count > 0)
                    {
                        var file = parser.Files.First();

                        if (string.IsNullOrWhiteSpace(file.FileName) || file.Data.Length == 0)
                        {
                            if (isAjaxRequest)
                                SendJsonResponse(response, "{ \"success\": false, \"error\": \"Dosya boş\" }");
                            else
                                SendResponse(response, GetChatPage(nick));
                            return;
                        }

                        string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads");
                        if (!Directory.Exists(uploadsDir))
                            Directory.CreateDirectory(uploadsDir);

                        string extension = Path.GetExtension(file.FileName);
                        string newFileName = Guid.NewGuid().ToString() + extension;
                        string filePath = Path.Combine(uploadsDir, newFileName);

                        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        {
                            file.Data.CopyTo(fs);
                        }

                        string timestamp = DateTime.Now.ToString("HH:mm:ss");
                        string msgContent = $"<span class='timestamp'>[{timestamp}]</span> <span class='nick'>{nick}:</span> <br/><img src='/uploads/{newFileName}' style='max-width:100%' alt='image'/>";
                        var msgItem = new MessageItem(msgContent);
                        messages.Insert(0, msgItem);
                        messageCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Dosya yükleme hatası: {ex.Message}");
                    if (isAjaxRequest)
                        SendJsonResponse(response, "{ \"success\": false, \"error\": \"" + ex.Message + "\" }");
                    else
                        SendResponse(response, "<html><body><h1>Hata: Dosya yükleme başarısız!</h1></body></html>");
                    return;
                }
            }
            else
            {
                var formData = ParseFormData(request);
                if (!formData.ContainsKey("nick") || string.IsNullOrWhiteSpace(formData["nick"]))
                {
                    SendResponse(response, "<html><body><h1>Hata: Nick alanı boş!</h1></body></html>");
                    return;
                }
                nick = formData["nick"];

                if (formData.ContainsKey("ajax") && formData["ajax"] == "true")
                {
                    isAjaxRequest = true;
                }

                if (formData.ContainsKey("message") && !string.IsNullOrWhiteSpace(formData["message"]))
                {
                    message = formData["message"];
                    string timestamp = DateTime.Now.ToString("HH:mm:ss");
                    string msgContent = $"<span class='timestamp'>[{timestamp}]</span> <span class='nick'>{nick}:</span> {HttpUtility.HtmlEncode(message)}";
                    var msgItem = new MessageItem(msgContent);
                    messages.Insert(0, msgItem);
                    messageCount++;
                }
            }

            if (isAjaxRequest)
            {
                SendJsonResponse(response, "{ \"success\": true, \"count\": " + messageCount + " }");
            }
            else
            {
                SendResponse(response, GetChatPage(nick));
            }
        }

        static string GetNewMessagesJson(int lastCount)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{ \"messages\": [");

            int newCount = messageCount - lastCount;
            int messagesToReturn = Math.Min(newCount, messages.Count);

            for (int i = 0; i < messagesToReturn; i++)
            {
                if (i > 0) sb.Append(",");
                string escapedMessage = messages[i].Content.Replace("\"", "\\\"");
                sb.Append("{\"id\":\"" + messages[i].Id + "\",\"content\":\"" + escapedMessage + "\"}");
            }

            sb.Append("], \"count\": " + messageCount + " }");
            return sb.ToString();
        }

        static string GetAllMessagesJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{ \"messages\": [");

            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                string escapedMessage = messages[i].Content.Replace("\"", "\\\"");
                sb.Append("{\"id\":\"" + messages[i].Id + "\",\"content\":\"" + escapedMessage + "\"}");
            }

            sb.Append("], \"count\": " + messageCount + " }");
            return sb.ToString();
        }

        static Dictionary<string, string> ParseFormData(HttpListenerRequest request)
        {
            var formData = new Dictionary<string, string>();
            using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string body = reader.ReadToEnd();
                var parsedForm = HttpUtility.ParseQueryString(body);
                foreach (string key in parsedForm)
                {
                    formData[key] = parsedForm[key];
                }
            }
            return formData;
        }

        static string GetHomePage()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Chat Sistemi</title>
    <link rel='stylesheet' href='/css/styles.css'>
</head>
<body>
    <div class='login-container'>
        <h1>Chat Sistemi</h1>
        <form method='POST' action='/chat'>
            <input type='text' name='nick' required placeholder='Kullanıcı adınızı girin'>
            <input type='submit' value='Başla'>
        </form>
    </div>
</body>
</html>";
        }

        static string GetChatPage(string nick)
        {
            return $@"<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Chat Uygulaması</title>
    <link rel='stylesheet' href='/css/styles.css'>
</head>
<body>
    <div class='chat-container'>
        <div class='header'>
            <h1>Chat Odası</h1>
        </div>
        <div class='messages' id='messages'>
            {GetMessagesHtml()}
        </div>
        <div class='footer'>
            <form id='chatForm' class='message-form' method='POST' action='/chat' enctype='multipart/form-data'>
                <input type='hidden' name='nick' id='nickInput' value='{HttpUtility.HtmlEncode(nick)}'>
                <input type='hidden' name='ajax' id='ajaxFlag' value='false'>
                <div class='input-group'>
                    <input type='text' name='message' id='messageInput' placeholder='Mesajınızı yazın...' autocomplete='off'>
                    <button type='submit' class='submit-btn'>Gönder</button>
                </div>
                <div class='file-upload-container' id='dropzone'>
                    <span>Dosya yüklemek için tıklayın veya sürükleyip bırakın</span>
                    <input type='file' name='image' id='fileInput' accept='image/*'>
                    <div class='preview-container'>
                        <img id='uploadPreview' src='' alt='Önizleme'>
                    </div>
                </div>
            </form>
        </div>
    </div>

    <script>
        // Global değişkenler
        const currentNick = '{HttpUtility.JavaScriptStringEncode(nick)}';
        const initialMessageCount = {messageCount};
    </script>
    <script src='/js/chat.js'></script>
</body>
</html>";
        }

        static string GetMessagesHtml()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var msg in messages)
            {
                sb.AppendLine($@"
        <div class='message' data-id='{msg.Id}'>
            {msg.Content}
            <button class='delete-btn' data-id='{msg.Id}'>×</button>
        </div>");
            }
            return sb.ToString();
        }

        static string GetNotFoundPage()
        {
            return "<html><body><h1>404 - Sayfa bulunamadı</h1></body></html>";
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