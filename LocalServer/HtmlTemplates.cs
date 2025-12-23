using System.Web;

namespace ChatApp
{
    public static class HtmlTemplates
    {
        public static string GetHomePage()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Giriş</title>
    <style>
        body { font-family: 'Segoe UI', sans-serif; background: #f0f2f5; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; }
        .login-box { background: white; padding: 40px; border-radius: 10px; box-shadow: 0 4px 15px rgba(0,0,0,0.1); text-align: center; width: 300px; }
        h1 { color: #0078d4; margin-bottom: 20px; }
        input { width: 100%; padding: 12px; margin-bottom: 20px; border: 1px solid #ccc; border-radius: 5px; box-sizing: border-box; }
        button { width: 100%; padding: 12px; background: #0078d4; color: white; border: none; border-radius: 5px; cursor: pointer; font-weight: bold; }
        button:hover { background: #005a9e; }
    </style>
</head>
<body>
    <div class='login-box'>
        <h1>Chat Giriş</h1>
        <form method='POST' action='/chat'>
            <input type='text' name='nick' placeholder='Nickiniz' required autofocus>
            <button type='submit'>Giriş Yap</button>
        </form>
    </div>
</body>
</html>";
        }

        public static string GetChatPage(string nick)
        {
            // Mesajları DbHelper'dan istiyoruz
            string messagesHtml = DbHelper.GetMessagesHtml(50);

            return $@"<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Chat</title>
    <style>
        body {{ font-family: 'Segoe UI', sans-serif; background: #f0f2f5; margin: 0; display: flex; justify-content: center; height: 100vh; }}
        .chat-container {{ width: 100%; max-width: 800px; background: white; display: flex; flex-direction: column; height: 100%; box-shadow: 0 4px 15px rgba(0,0,0,0.2); }}
        .header {{ background: #0078d4; color: white; padding: 15px; text-align: center; display:flex; justify-content:space-between; align-items:center; }}
        .header h1 {{ margin:0; font-size:1.2em; }}
        .logout-btn {{ color: white; text-decoration: none; border: 1px solid white; padding: 5px 10px; border-radius: 5px; font-size: 0.9em; }}
        .messages {{ flex: 1; padding: 20px; overflow-y: auto; background: #e5ddd5; display: flex; flex-direction: column-reverse; align-items: flex-start; scroll-behavior: smooth; }} 
        .message {{ background: white; padding: 8px 12px; padding-right: 30px !important; margin-bottom: 10px; border-radius: 8px; position: relative; max-width: 80%; width: fit-content; word-wrap: break-word; min-width: 60px; box-shadow: 0 1px 2px rgba(0,0,0,0.1); }}
        .message.media-msg {{ padding: 4px !important; padding-right: 30px !important; display: flex; flex-direction: column; }}
        .message .delete-btn {{ position: absolute; top: 2px; right: 2px; background: rgba(255,255,255,0.8); border: none; color: #999; cursor: pointer; width: 24px; height: 24px; border-radius: 50%; font-weight: bold; z-index: 10; display: flex; align-items: center; justify-content: center; }}
        .message .delete-btn:hover {{ color: red; background: #ffeaea; }}
        .message.media-msg video, .message.media-msg img {{ max-width: 300px !important; width: 100%; height: auto; border-radius: 6px; display: block; }}
        .audio-container {{ display: flex; align-items: center; gap: 10px; padding: 5px; width: 100%; min-width: 280px; }}
        .audio-container audio {{ height: 32px; flex: 1; outline: none; }}
        .media-header {{ font-size: 0.75em; margin-bottom: 4px; padding-left: 4px; color: #555; }}
        .nick {{ font-weight: bold; color: #d93025; }}
        .timestamp {{ font-size: 0.75em; color: #888; margin-right: 5px; }}
        .footer {{ padding: 15px; background: #f0f0f0; border-top: 1px solid #ddd; }}
        .input-group {{ display: flex; gap: 10px; align-items: center; }}
        #messageInput {{ flex: 1; padding: 10px; border: 1px solid #ccc; border-radius: 20px; outline: none; }}
        .submit-btn {{ padding: 10px 20px; background: #0078d4; color: white; border: none; border-radius: 20px; cursor: pointer; }}
        .submit-btn:disabled {{ background: #ccc; }}
        .mic-btn {{ background: #fff; border: 1px solid #ccc; border-radius: 50%; width: 40px; height: 40px; cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 20px; }}
        .mic-btn.recording {{ background: #ff4444; color: white; animation: pulse 1.5s infinite; }}
        @keyframes pulse {{ 0% {{ box-shadow: 0 0 0 0 rgba(255, 68, 68, 0.7); }} 70% {{ box-shadow: 0 0 0 10px rgba(255, 68, 68, 0); }} 100% {{ box-shadow: 0 0 0 0 rgba(255, 68, 68, 0); }} }}
        .file-upload-container {{ margin-top: 10px; border: 2px dashed #ccc; padding: 10px; text-align: center; border-radius: 10px; position: relative; background: #fafafa; display: none; }} 
        #fileInput {{ position: absolute; top: 0; left: 0; width: 100%; height: 100%; opacity: 0; cursor: pointer; }}
        .preview-box {{ display: none; margin-top: 10px; align-items: center; flex-direction: column; position: relative; }}
        .preview-img {{ max-height: 100px; border-radius: 5px; }}
        .file-icon {{ font-size: 40px; }}
        .cancel-upload-btn {{ position: absolute; top: -10px; right: -10px; background: #ff4444; color: white; border: none; border-radius: 50%; width: 25px; height: 25px; cursor: pointer; font-weight: bold; z-index: 10; }}
        .file-attachment {{ display: flex; align-items: center; gap: 10px; background: #f9f9f9; padding: 10px; border-radius: 5px; border: 1px solid #eee; margin-top: 5px; }}
        .file-attachment a {{ text-decoration: none; color: #0078d4; font-weight: bold; }}
        .show-upload-btn {{ background: #eee; border: 1px solid #ccc; width: 40px; height: 40px; border-radius: 50%; cursor: pointer; font-size: 20px; display:flex; align-items:center; justify-content:center; }}
        .show-upload-btn:hover {{ background: #ddd; }}
    </style>
</head>
<body>
    <div class='chat-container'>
        <div class='header'>
            <h1>Chat ({HttpUtility.HtmlEncode(nick)})</h1>
            <a href='/' class='logout-btn'>Çıkış</a>
        </div>
        <div class='messages' id='messages'>{messagesHtml}</div>
        <div class='footer'>
            <form id='chatForm' method='POST' enctype='multipart/form-data'>
                <input type='hidden' name='nick' value='{HttpUtility.HtmlEncode(nick)}'>
                <input type='hidden' name='ajax' value='true'>
                <div class='input-group'>
                    <button type='button' id='toggleUploadBtn' class='show-upload-btn' title='Dosya Ekle'>+</button>
                    <input type='text' name='message' id='messageInput' placeholder='Mesaj yaz...' autocomplete='off' autofocus>
                    <button type='button' id='micBtn' class='mic-btn' title='Ses Kaydet'>🎤</button>
                    <button type='submit' class='submit-btn'>Gönder</button>
                </div>
                <div class='file-upload-container' id='dropArea'>
                    <span id='dropText'>Dosya seç veya sürükle</span>
                    <input type='file' name='image' id='fileInput'>
                    <div class='preview-box' id='previewBox'>
                        <button type='button' id='cancelFileBtn' class='cancel-upload-btn'>×</button>
                        <img id='imagePreview' class='preview-img' style='display:none;'>
                        <div id='iconPreview' class='file-icon' style='display:none;'></div>
                        <div id='fileNamePreview'></div>
                    </div>
                </div>
            </form>
        </div>
    </div>
    <script>
        const MAX_FILE_SIZE_MB = 1000;
        const fileInput = document.getElementById('fileInput');
        const previewBox = document.getElementById('previewBox');
        const imagePreview = document.getElementById('imagePreview');
        const iconPreview = document.getElementById('iconPreview');
        const fileNamePreview = document.getElementById('fileNamePreview');
        const dropText = document.getElementById('dropText');
        const micBtn = document.getElementById('micBtn');
        const cancelFileBtn = document.getElementById('cancelFileBtn');
        const toggleUploadBtn = document.getElementById('toggleUploadBtn');
        const dropArea = document.getElementById('dropArea');
        const messagesDiv = document.getElementById('messages');
        const messageInput = document.getElementById('messageInput');
        let mediaRecorder, audioChunks = [], isRecording = false;

        window.onload = function() {{ messageInput.focus(); scrollToBottom(); }};
        function scrollToBottom() {{ messagesDiv.scrollTop = messagesDiv.scrollHeight; }}
        toggleUploadBtn.addEventListener('click', () => {{ dropArea.style.display = dropArea.style.display === 'block' ? 'none' : 'block'; }});
        cancelFileBtn.addEventListener('click', () => {{ resetFile(); }});
        function resetFile() {{
            fileInput.value = ''; window.recordedAudioFile = null;
            previewBox.style.display = 'none'; dropText.style.display = 'block';
            dropArea.style.display = 'none'; 
            if(isRecording && mediaRecorder) {{ mediaRecorder.stop(); isRecording=false; micBtn.classList.remove('recording'); }}
        }}
        micBtn.addEventListener('click', async () => {{
            if (!isRecording) {{
                try {{
                    const stream = await navigator.mediaDevices.getUserMedia({{ audio: true }});
                    mediaRecorder = new MediaRecorder(stream);
                    audioChunks = [];
                    mediaRecorder.ondataavailable = e => audioChunks.push(e.data);
                    mediaRecorder.onstop = () => {{
                        if(fileInput.value === '' && previewBox.style.display === 'none') return;
                        const file = new File([new Blob(audioChunks, {{ type: 'audio/webm' }})], 'ses.webm', {{ type: 'audio/webm' }});
                        window.recordedAudioFile = file; handleFile(file);
                        dropArea.style.display = 'block'; 
                    }};
                    resetFile(); 
                    previewBox.style.display = 'flex'; dropText.style.display = 'none';
                    imagePreview.style.display = 'none'; iconPreview.style.display = 'block'; iconPreview.textContent = '🎤';
                    fileNamePreview.textContent = 'Kaydediliyor...';
                    dropArea.style.display = 'block'; 
                    mediaRecorder.start(); isRecording = true; micBtn.classList.add('recording');
                }} catch (e) {{ alert('Mikrofon hatası: ' + e.message); }}
            }} else {{ mediaRecorder.stop(); isRecording = false; micBtn.classList.remove('recording'); }}
        }});
        fileInput.addEventListener('change', function() {{ window.recordedAudioFile = null; handleFile(this.files[0]); }});
        function handleFile(file) {{
            if (!file) return;
            if (file.size / 1024 / 1024 > MAX_FILE_SIZE_MB) {{ alert('Dosya > 1GB!'); fileInput.value = ''; return; }}
            previewBox.style.display = 'flex'; dropText.style.display = 'none';
            fileNamePreview.textContent = file.name;
            if (file.type.startsWith('image/')) {{
                const reader = new FileReader();
                reader.onload = e => {{ imagePreview.src = e.target.result; imagePreview.style.display = 'block'; iconPreview.style.display = 'none'; }};
                reader.readAsDataURL(file);
            }} else {{
                imagePreview.style.display = 'none'; iconPreview.style.display = 'block';
                iconPreview.textContent = file.name.endsWith('pdf') ? '📕' : (file.name.match(/zip|rar/) ? '📦' : '📄');
            }}
        }}
        document.getElementById('chatForm').addEventListener('submit', function(e) {{
            e.preventDefault();
            const fd = new FormData(this);
            const btn = document.querySelector('.submit-btn');
            if(window.recordedAudioFile) fd.set('image', window.recordedAudioFile);
            if(!messageInput.value.trim() && !fileInput.files.length && !window.recordedAudioFile) return;
            btn.disabled = true; btn.innerText = '...';
            fetch('/chat', {{ method: 'POST', body: fd }})
            .then(r => r.json()).then(d => {{
                btn.disabled = false; btn.innerText = 'Gönder';
                if(d.success) {{
                    if(d.html) {{ messagesDiv.insertAdjacentHTML('afterbegin', d.html); }}
                    scrollToBottom(); messageInput.value = ''; resetFile(); setTimeout(() => {{ messageInput.focus(); }}, 100); bindDeleteButtons();
                }} else {{ alert('Hata: ' + d.error); }}
            }}).catch(() => {{ alert('Bağlantı hatası!'); btn.disabled = false; btn.innerText = 'Gönder'; }});
        }});
        function bindDeleteButtons() {{
            document.querySelectorAll('.delete-btn').forEach(b => {{
                const newB = b.cloneNode(true); b.parentNode.replaceChild(newB, b);
                newB.addEventListener('click', function() {{
                    if(!confirm('Silinsin mi?')) return;
                    const id = this.getAttribute('data-id');
                    const msgDiv = this.closest('.message'); 
                    const fd = new FormData(); fd.append('messageId', id);
                    fetch('/delete', {{ method: 'POST', body: fd }}).then(r => r.json()).then(d => {{ 
                        if(d.success) {{ msgDiv.remove(); }} else {{ alert('Silinemedi.'); }}
                    }});
                }});
            }});
        }}
        bindDeleteButtons();
    </script>
</body>
</html>";
        }
    }
}