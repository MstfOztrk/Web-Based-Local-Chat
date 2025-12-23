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
        static readonly List<MessageItem> messages = new List<MessageItem>();
        static int messageCount = 0;

        class MessageItem
        {
            public string Id { get; } = Guid.NewGuid().ToString();
            public string Content { get; set; }
            public DateTime Timestamp { get; } = DateTime.Now;

            public MessageItem(string content)
            {
                Content = content;
            }
        }

        static void Main(string[] args)
        {
            const string prefix = "http://+:8080/";
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                Console.WriteLine("Sunucu çalışıyor: " + prefix);
                ListenForRequests(listener);
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine("Hata: Visual Studio'yu Yönetici Olarak çalıştırman lazım!");
                Console.WriteLine("Detay: " + ex.Message);
            }
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
                    if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        var queryParams = HttpUtility.ParseQueryString(request.Url.Query);
                        string lastCountStr = queryParams["lastCount"];

                        int lastCount = 0;
                        if (!string.IsNullOrEmpty(lastCountStr) && int.TryParse(lastCountStr, out lastCount))
                        {
                            if (lastCount < messageCount)
                            {
                                SendJsonResponse(response, GetNewMessagesJson(lastCount));
                            }
                            else
                            {
                                SendJsonResponse(response, "{ \"messages\": [], \"count\": " + messageCount + " }");
                            }
                        }
                        else
                        {
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
                        response.StatusCode = 405;
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
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.TrimStart('/'));

                if (!File.Exists(filePath))
                {
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
                Console.WriteLine("ServeStaticFile error: " + ex.Message);
                response.StatusCode = 500;
                response.Close();
            }
        }

        /// <summary>
        /// Handles message deletion request supporting multipart/form-data to fix the bug.
        /// </summary>
        static void DeleteMessage(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string messageId = null;

                if (request.ContentType != null && request.ContentType.StartsWith("multipart/form-data"))
                {
                    var parser = MultipartFormDataParser.Parse(request.InputStream);
                    for (int i = 0; i < parser.Parameters.Count; i++)
                    {
                        if (parser.Parameters[i].Name == "messageId")
                        {
                            messageId = parser.Parameters[i].Data;
                            break;
                        }
                    }
                }
                else
                {
                    var formData = ParseFormData(request);
                    if (formData.ContainsKey("messageId"))
                    {
                        messageId = formData["messageId"];
                    }
                }

                if (!string.IsNullOrEmpty(messageId))
                {
                    int index = -1;
                    for (int i = 0; i < messages.Count; i++)
                    {
                        if (messages[i].Id.Equals(messageId, StringComparison.Ordinal))
                        {
                            index = i;
                            break;
                        }
                    }

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

                string extension = Path.GetExtension(fullPath).ToLower();
                string contentType = "application/octet-stream";

                if (extension == ".jpg" || extension == ".jpeg") contentType = "image/jpeg";
                else if (extension == ".png") contentType = "image/png";
                else if (extension == ".gif") contentType = "image/gif";
                else if (extension == ".webp") contentType = "image/webp";
                else if (extension == ".txt") contentType = "text/plain";
                else if (extension == ".html") contentType = "text/html";
                else if (extension == ".pdf") contentType = "application/pdf";
                else if (extension == ".zip") contentType = "application/zip";
                else if (extension == ".rar") contentType = "application/x-rar-compressed";
                else if (extension == ".mp4") contentType = "video/mp4";
                else if (extension == ".webm") contentType = "video/webm";
                else if (extension == ".mp3") contentType = "audio/mpeg";
                else if (extension == ".wav") contentType = "audio/wav";

                byte[] fileBytes = File.ReadAllBytes(fullPath);
                response.ContentType = contentType;
                response.ContentLength64 = fileBytes.Length;

                if (!contentType.StartsWith("image/") && !contentType.StartsWith("video/") && !contentType.StartsWith("audio/"))
                {
                    response.AddHeader("Content-Disposition", $"inline; filename=\"{fileName}\"");
                }

                using (var output = response.OutputStream)
                {
                    output.Write(fileBytes, 0, fileBytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ServeUploadedFile error: " + ex.Message);
                response.StatusCode = 500;
                response.Close();
            }
        }

        // Program.cs içindeki bu metodu komple değiştir:

        static void ProcessChatRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            string nick = "";
            string message = "";
            bool isAjaxRequest = false;

            if (request.Headers["X-Requested-With"] == "XMLHttpRequest") isAjaxRequest = true;

            if (request.ContentType != null && request.ContentType.StartsWith("multipart/form-data"))
            {
                try
                {
                    var parser = MultipartFormDataParser.Parse(request.InputStream);

                    for (int i = 0; i < parser.Parameters.Count; i++)
                    {
                        var p = parser.Parameters[i];
                        if (p.Name == "nick") nick = p.Data;
                        if (p.Name == "ajax" && p.Data == "true") isAjaxRequest = true;
                        if (p.Name == "message") message = p.Data;
                    }

                    if (string.IsNullOrWhiteSpace(nick))
                    {
                        SendResponse(response, "<html><body><h1>Hata: Nick alanı boş!</h1></body></html>");
                        return;
                    }

                    if (!string.IsNullOrEmpty(message))
                    {
                        string timestamp = DateTime.Now.ToString("HH:mm:ss");
                        string msgContent = $"<span class='timestamp'>[{timestamp}]</span> <span class='nick'>{nick}:</span> {HttpUtility.HtmlEncode(message)}";
                        messages.Insert(0, new MessageItem(msgContent));
                        messageCount++;
                    }

                    if (parser.Files != null && parser.Files.Count > 0)
                    {
                        var file = parser.Files.First();
                        if (!string.IsNullOrWhiteSpace(file.FileName) && file.Data.Length > 0)
                        {
                            string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads");
                            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

                            string originalExt = Path.GetExtension(file.FileName).ToLower();
                            string newFileName = Guid.NewGuid().ToString() + originalExt;
                            string filePath = Path.Combine(uploadsDir, newFileName);

                            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                            {
                                file.Data.CopyTo(fs);
                            }

                            string timestamp = DateTime.Now.ToString("HH:mm");
                            string msgContent = "";
                            string header = $"<div class='media-header'><span class='timestamp'>[{timestamp}]</span> <span class='nick'>{nick}</span></div>";

                            if (originalExt == ".jpg" || originalExt == ".jpeg" || originalExt == ".png" || originalExt == ".gif" || originalExt == ".webp")
                            {
                                msgContent = $"{header}<img src='/uploads/{newFileName}' alt='image'/>";
                            }
                            else if (originalExt == ".mp4" || originalExt == ".webm" || originalExt == ".mov")
                            {
                                msgContent = $"{header}<video controls><source src='/uploads/{newFileName}' type='video/mp4'>Desteklenmiyor.</video>";
                            }
                            else if (originalExt == ".mp3" || originalExt == ".wav" || originalExt == ".ogg")
                            {
                                msgContent = $"<div class='audio-container'><div style='display:flex; flex-direction:column; font-size:10px; margin-right:5px;'><span class='nick'>{nick}</span><span>{timestamp}</span></div><audio controls><source src='/uploads/{newFileName}'>Desteklenmiyor.</audio></div>";
                            }
                            else
                            {
                                string icon = "📄";
                                if (originalExt == ".zip" || originalExt == ".rar") icon = "📦";
                                else if (originalExt == ".pdf") icon = "📕";
                                else if (originalExt == ".exe") icon = "⚙️";

                                msgContent = $"<span class='timestamp'>[{timestamp}]</span> <span class='nick'>{nick}:</span> <br/>" +
                                             $"<div class='file-attachment'>{icon} Dosya: <a href='/uploads/{newFileName}' download='{file.FileName}'>{file.FileName}</a></div>";
                            }

                            messages.Insert(0, new MessageItem(msgContent));
                            messageCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (isAjaxRequest) SendJsonResponse(response, "{ \"success\": false, \"error\": \"" + ex.Message + "\" }");
                    else SendResponse(response, "<html><body><h1>Hata!</h1></body></html>");
                    return;
                }
            }
            else
            {
                var formData = ParseFormData(request);

                if (formData.ContainsKey("nick")) nick = formData["nick"];
                if (formData.ContainsKey("ajax") && formData["ajax"] == "true") isAjaxRequest = true;

                if (formData.ContainsKey("message") && !string.IsNullOrWhiteSpace(formData["message"]))
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss");
                    string msgContent = $"<span class='timestamp'>[{timestamp}]</span> <span class='nick'>{nick}:</span> {HttpUtility.HtmlEncode(formData["message"])}";
                    messages.Insert(0, new MessageItem(msgContent));
                    messageCount++;
                }
            }

            if (isAjaxRequest) SendJsonResponse(response, "{ \"success\": true, \"count\": " + messageCount + " }");
            else SendResponse(response, GetChatPage(nick));
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
                for (int i = 0; i < parsedForm.AllKeys.Length; i++)
                {
                    string key = parsedForm.AllKeys[i];
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
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f0f2f5; margin: 0; display: flex; justify-content: center; height: 100vh; }}
        .chat-container {{ width: 100%; max-width: 800px; background: white; display: flex; flex-direction: column; box-shadow: 0 4px 15px rgba(0,0,0,0.2); height: 100%; }}
        .header {{ background-color: #0078d4; color: white; padding: 15px; text-align: center; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }}
        .messages {{ flex: 1; padding: 20px; overflow-y: auto; background-color: #e5ddd5; display: flex; flex-direction: column-reverse; align-items: flex-start; }} 
        
        /* --- MESAJ STİLLERİ GÜNCELLENDİ --- */
        .message {{ 
            background: white; 
            padding: 8px 12px; 
            padding-right: 30px !important; /* Çarpı butonu için sağdan boşluk bıraktık */
            margin-bottom: 10px; 
            border-radius: 8px; 
            box-shadow: 0 1px 2px rgba(0,0,0,0.1); 
            position: relative; 
            max-width: 80%; 
            width: fit-content; 
            word-wrap: break-word;
            min-width: 60px; /* Buton sığsın diye min genişlik */
        }}

        /* Medya Mesajı (Video, Ses, Resim) */
        .message.media-msg {{
            padding: 4px !important; 
            padding-right: 30px !important; /* Medyada da buton için yer açtık */
            background: #fff; 
            display: flex;
            flex-direction: column;
        }}

        /* SİLME BUTONU GÜNCELLENDİ */
        .message .delete-btn {{ 
            position: absolute; 
            top: 2px; 
            right: 2px; 
            background: rgba(255, 255, 255, 0.8); /* Arkası hafif beyaz olsun okunsun */
            border: none; 
            color: #999; 
            cursor: pointer; 
            font-size: 18px; 
            font-weight: bold; 
            width: 24px;
            height: 24px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 10;
        }}
        .message .delete-btn:hover {{ color: red; background: #ffeaea; }}

        /* Diğer Stiller */
        .message.media-msg video, 
        .message.media-msg img {{
            max-width: 300px !important;
            width: 100%;
            height: auto;
            border-radius: 6px;
            display: block;
        }}

        .audio-container {{
            display: flex;
            align-items: center;
            gap: 10px;
            padding: 5px;
            width: 100%;
            min-width: 280px; 
        }}
        .audio-container audio {{ height: 32px; flex: 1; outline: none; }}

        .media-header {{ font-size: 0.75em; margin-bottom: 4px; padding-left: 4px; color: #555; }}
        .nick {{ font-weight: bold; color: #d93025; }}
        .timestamp {{ font-size: 0.75em; color: #888; margin-right: 5px; }}
        .footer {{ padding: 15px; background: #f0f0f0; border-top: 1px solid #ddd; }}
        .input-group {{ display: flex; gap: 10px; margin-bottom: 10px; align-items: center; }}
        #messageInput {{ flex: 1; padding: 10px; border: 1px solid #ccc; border-radius: 20px; outline: none; }}
        .submit-btn {{ padding: 10px 20px; background-color: #0078d4; color: white; border: none; border-radius: 20px; cursor: pointer; transition: background 0.3s; white-space: nowrap; }}
        .submit-btn:disabled {{ background-color: #ccc; cursor: not-allowed; }}
        
        .mic-btn {{ background: #fff; border: 1px solid #ccc; border-radius: 50%; width: 40px; height: 40px; cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 20px; transition: all 0.3s; }}
        .mic-btn:hover {{ background: #f0f0f0; }}
        .mic-btn.recording {{ background: #ff4444; color: white; border-color: #ff4444; animation: pulse 1.5s infinite; }}
        
        @keyframes pulse {{ 0% {{ box-shadow: 0 0 0 0 rgba(255, 68, 68, 0.7); }} 70% {{ box-shadow: 0 0 0 10px rgba(255, 68, 68, 0); }} 100% {{ box-shadow: 0 0 0 0 rgba(255, 68, 68, 0); }} }}

        .file-upload-container {{ border: 2px dashed #ccc; padding: 15px; text-align: center; border-radius: 10px; position: relative; background: #fafafa; transition: all 0.3s; cursor: pointer; }}
        .file-upload-container:hover {{ border-color: #0078d4; background: #eef6fb; }}
        #fileInput {{ position: absolute; top: 0; left: 0; width: 100%; height: 100%; opacity: 0; cursor: pointer; }}
        
        .preview-box {{ display: none; margin-top: 10px; align-items: center; justify-content: center; flex-direction: column; background: #fff; padding: 10px; border-radius: 8px; border: 1px solid #e0e0e0; position: relative; }}
        .preview-img {{ max-height: 100px; border-radius: 5px; border: 1px solid #ddd; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }}
        .file-icon {{ font-size: 48px; margin-bottom: 5px; }}
        .file-name {{ font-size: 14px; color: #333; font-weight: 500; word-break: break-all; margin-top: 5px; }}
        
        .cancel-upload-btn {{
            position: absolute; top: -10px; right: -10px; background: #ff4444; color: white; border: none; border-radius: 50%; width: 25px; height: 25px; cursor: pointer; font-weight: bold; box-shadow: 0 2px 5px rgba(0,0,0,0.2); z-index: 10;
        }}
        .cancel-upload-btn:hover {{ background: #cc0000; }}

        .file-attachment {{ display: flex; align-items: center; gap: 10px; background: #f9f9f9; padding: 10px; border-radius: 5px; border: 1px solid #eee; margin-top: 5px; }}
        .file-attachment a {{ text-decoration: none; color: #0078d4; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='chat-container'>
        <div class='header'>
            <h1>Chat Odası ({HttpUtility.HtmlEncode(nick)})</h1>
        </div>
        <div class='messages' id='messages'>
            {GetMessagesHtml()}
        </div>
        <div class='footer'>
            <form id='chatForm' method='POST' action='/chat' enctype='multipart/form-data'>
                <input type='hidden' name='nick' value='{HttpUtility.HtmlEncode(nick)}'>
                <input type='hidden' name='ajax' value='true'>
                
                <div class='input-group'>
                    <input type='text' name='message' id='messageInput' placeholder='Mesaj yaz...' autocomplete='off'>
                    <button type='button' id='micBtn' class='mic-btn' title='Ses Kaydet'>🎤</button>
                    <button type='submit' class='submit-btn'>Gönder</button>
                </div>

                <div class='file-upload-container' id='dropArea'>
                    <span id='dropText'>Dosya eklemek için tıkla veya sürükle</span>
                    <input type='file' name='image' id='fileInput'>
                    
                    <div class='preview-box' id='previewBox'>
                        <button type='button' id='cancelFileBtn' class='cancel-upload-btn' title='İptal Et'>×</button>
                        <img id='imagePreview' class='preview-img' style='display:none;'>
                        <div id='iconPreview' class='file-icon' style='display:none;'></div>
                        <div id='fileNamePreview' class='file-name'></div>
                    </div>
                </div>
            </form>
        </div>
    </div>

    <script>
        const currentNick = '{HttpUtility.JavaScriptStringEncode(nick)}';
        const initialMessageCount = {messageCount};
        const MAX_FILE_SIZE_MB = 500;

        const fileInput = document.getElementById('fileInput');
        const previewBox = document.getElementById('previewBox');
        const imagePreview = document.getElementById('imagePreview');
        const iconPreview = document.getElementById('iconPreview');
        const fileNamePreview = document.getElementById('fileNamePreview');
        const dropText = document.getElementById('dropText');
        const dropArea = document.getElementById('dropArea');
        const micBtn = document.getElementById('micBtn');
        const cancelFileBtn = document.getElementById('cancelFileBtn');

        let mediaRecorder;
        let audioChunks = [];
        let isRecording = false;

        cancelFileBtn.addEventListener('click', () => {{
            fileInput.value = '';
            window.recordedAudioFile = null;
            previewBox.style.display = 'none';
            dropText.style.display = 'block';
            if (isRecording && mediaRecorder) {{ mediaRecorder.stop(); isRecording = false; micBtn.classList.remove('recording'); }}
        }});

        micBtn.addEventListener('click', async () => {{
            if (!isRecording) {{
                try {{
                    const stream = await navigator.mediaDevices.getUserMedia({{ audio: true }});
                    mediaRecorder = new MediaRecorder(stream);
                    audioChunks = [];
                    
                    mediaRecorder.ondataavailable = event => {{ audioChunks.push(event.data); }};
                    mediaRecorder.onstop = () => {{
                        if(fileInput.value === '' && previewBox.style.display === 'none') return;
                        const audioBlob = new Blob(audioChunks, {{ type: 'audio/webm' }});
                        const file = new File([audioBlob], 'ses_kaydi_' + new Date().getTime() + '.webm', {{ type: 'audio/webm' }});
                        window.recordedAudioFile = file;
                        handleFile(file);
                    }};

                    fileInput.value = ''; 
                    window.recordedAudioFile = null;
                    previewBox.style.display = 'flex';
                    dropText.style.display = 'none';
                    imagePreview.style.display = 'none';
                    iconPreview.style.display = 'block';
                    iconPreview.textContent = '🎤';
                    fileNamePreview.textContent = 'Kaydediliyor...';

                    mediaRecorder.start();
                    isRecording = true;
                    micBtn.classList.add('recording');
                }} catch (err) {{
                    alert('Mikrofon hatası: ' + err.message + '\\n\\nEğer IP üzerinden bağlanıyorsanız tarayıcınız engelliyor olabilir. chrome://flags ayarından izin vermeniz gerekebilir.');
                }}
            }} else {{
                mediaRecorder.stop();
                isRecording = false;
                micBtn.classList.remove('recording');
            }}
        }});

        fileInput.addEventListener('change', function(e) {{
            window.recordedAudioFile = null; 
            handleFile(this.files[0]);
        }});

        function handleFile(file) {{
            if (!file) return;
            const fileSizeMB = file.size / 1024 / 1024;
            if (fileSizeMB > MAX_FILE_SIZE_MB) {{ alert('Dosya çok büyük!'); fileInput.value = ''; return; }}

            previewBox.style.display = 'flex';
            dropText.style.display = 'none';
            fileNamePreview.textContent = file.name + ' (' + fileSizeMB.toFixed(2) + ' MB)';

            if (file.type.startsWith('image/')) {{
                const reader = new FileReader();
                reader.onload = function(e) {{
                    imagePreview.src = e.target.result;
                    imagePreview.style.display = 'block';
                    iconPreview.style.display = 'none';
                }};
                reader.readAsDataURL(file);
            }} else {{
                imagePreview.style.display = 'none';
                iconPreview.style.display = 'block';
                const ext = file.name.split('.').pop().toLowerCase();
                let icon = '📄'; 
                if (ext === 'zip' || ext === 'rar') icon = '📦';
                else if (ext === 'pdf') icon = '📕';
                else if (ext === 'mp4' || ext === 'webm') icon = '🎬';
                else if (ext === 'mp3' || ext === 'wav' || ext === 'webm') icon = '🎵';
                iconPreview.textContent = icon;
            }}
        }}

        dropArea.addEventListener('dragover', (e) => {{ e.preventDefault(); dropArea.style.borderColor = '#0078d4'; }});
        dropArea.addEventListener('dragleave', (e) => {{ e.preventDefault(); dropArea.style.borderColor = '#ccc'; }});
        dropArea.addEventListener('drop', (e) => {{
            e.preventDefault();
            dropArea.style.borderColor = '#ccc';
            window.recordedAudioFile = null;
            fileInput.files = e.dataTransfer.files;
            handleFile(e.dataTransfer.files[0]);
        }});

        document.getElementById('chatForm').addEventListener('submit', function(e) {{
            e.preventDefault();
            const formData = new FormData(this);
            const btn = document.querySelector('.submit-btn');
            
            if (window.recordedAudioFile) {{ formData.set('image', window.recordedAudioFile); }}
            if (!document.getElementById('messageInput').value.trim() && !fileInput.files.length && !window.recordedAudioFile) return;

            btn.disabled = true;
            btn.innerText = '...';

            fetch('/chat', {{ method: 'POST', body: formData }})
            .then(response => response.json())
            .then(data => {{
                if (data.success) {{ location.reload(); }} 
                else {{ alert('Hata: ' + (data.error || 'Bilinmeyen hata')); btn.disabled = false; btn.innerText = 'Gönder'; }}
            }})
            .catch(err => {{ console.error(err); alert('Hata oluştu.'); btn.disabled = false; btn.innerText = 'Gönder'; }});
        }});
        
        document.querySelectorAll('.delete-btn').forEach(btn => {{
            btn.addEventListener('click', function() {{
                const id = this.getAttribute('data-id');
                if(!confirm('Silmek istediğine emin misin?')) return;
                const formData = new FormData();
                formData.append('messageId', id);
                fetch('/delete', {{ method: 'POST', body: formData }}).then(r => r.json()).then(d => {{ if(d.success) location.reload(); }});
            }});
        }});
    </script>
</body>
</html>";
        }

        static string GetMessagesHtml()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];

                // Mesajın içinde medya var mı kontrol et
                bool isMedia = msg.Content.Contains("<img") ||
                               msg.Content.Contains("<video") ||
                               msg.Content.Contains("<audio");

                // Varsa 'media-msg' sınıfını ekle
                string cssClass = isMedia ? "message media-msg" : "message";

                sb.AppendLine($@"
<div class='{cssClass}' data-id='{msg.Id}'>
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