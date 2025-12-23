using System.Web;

namespace ChatApp
{
    public static class HtmlTemplates
    {
        public static string GetHomePage()
        {
            return @"<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Lobi</title>
    <style>
        body { font-family: 'Segoe UI', sans-serif; background: #121212; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; color: white; }
        .lobby-container { width: 100%; max-width: 500px; padding: 20px; text-align: center; }
        h1 { color: #0078d4; margin-bottom: 5px; font-weight: 800; }
        p { color: #888; margin-bottom: 30px; }
        
        .input-box { width: 100%; padding: 15px; margin-bottom: 25px; border: 1px solid #333; border-radius: 10px; background: #1e1e1e; color: white; font-size: 1.1em; text-align: center; }
        .input-box:focus { outline: 2px solid #0078d4; }
        
        .channel-scroller { display: flex; overflow-x: auto; gap: 15px; padding: 10px; padding-bottom: 15px; scrollbar-width: none; cursor: grab; user-select: none; align-items: flex-start; }
        .channel-scroller:active { cursor: grabbing; }
        .channel-scroller::-webkit-scrollbar { display: none; }
        
        .channel-wrapper { display: flex; flex-direction: column; align-items: center; gap: 8px; flex-shrink: 0; }

        .channel-card { width: 120px; height: 140px; background: #1e1e1e; border-radius: 15px; display: flex; flex-direction: column; justify-content: center; align-items: center; transition: 0.2s; border: 2px solid transparent; box-shadow: 0 4px 6px rgba(0,0,0,0.3); }
        .channel-card:hover { transform: translateY(-3px); background: #252525; }
        .channel-card.selected { border-color: #0078d4; background: #1a252e; box-shadow: 0 0 10px rgba(0, 120, 212, 0.5); }
        
        .ch-icon { font-size: 40px; margin-bottom: 5px; pointer-events: none; }
        .ch-name { font-weight: bold; font-size: 0.9em; pointer-events: none; }
        .ch-desc { font-size: 0.7em; color: #666; margin-top: 5px; max-width: 90%; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; pointer-events: none; }
        .ch-count { font-size: 0.8em; color: #4da6ff; background: rgba(0,0,0,0.5); padding: 2px 6px; border-radius: 4px; margin-top: 5px; transition: 0.3s; }

        .del-chan-btn { background: #330000; color: #ff4444; border: 1px solid #550000; padding: 5px 12px; border-radius: 20px; font-size: 11px; cursor: pointer; opacity: 0.7; transition: 0.2s; font-weight:bold; }
        .del-chan-btn:hover { opacity: 1; background: #ff0000; color: white; border-color: red; }

        .create-card { border: 2px dashed #444; }
        .create-card .ch-icon { color: #0078d4; }
        
        .join-btn { width: 100%; padding: 15px; background: #0078d4; color: white; border: none; border-radius: 10px; cursor: pointer; font-weight: bold; font-size: 1.1em; margin-top: 20px; transition: 0.2s; }
        .join-btn:disabled { background: #333; cursor: not-allowed; color: #555; }
        .join-btn:hover:not(:disabled) { background: #005a9e; }

        .modal { display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.8); align-items: center; justify-content: center; z-index: 1000; }
        .modal-content { background: #1e1e1e; padding: 25px; border-radius: 15px; width: 300px; text-align: left; }
        .modal input { width: 100%; padding: 10px; margin: 10px 0; background: #121212; border: 1px solid #333; color: white; border-radius: 5px; box-sizing: border-box; }
        .modal-btns { display: flex; gap: 10px; margin-top: 10px; }
        .modal-btns button { flex: 1; padding: 10px; border-radius: 5px; border: none; cursor: pointer; }
        .btn-create { background: #0078d4; color: white; }
        .btn-cancel { background: #333; color: white; }
    </style>
</head>
<body>
    <div class='lobby-container'>
        <h1>CHAT LOBİ</h1>
        <p>Girmek istediğin odayı seç</p>

        <form method='POST' action='/chat' id='loginForm'>
            <input type='text' name='nick' id='nickInput' class='input-box' placeholder='Nickiniz' required autocomplete='off'>
            <input type='hidden' name='channelId' id='channelIdInput'>
            
            <div class='channel-scroller' id='channelList'></div>

            <button type='submit' class='join-btn' id='joinBtn' disabled>ODAYA GİR</button>
        </form>
    </div>

    <div class='modal' id='createModal'>
        <div class='modal-content'>
            <h3 style='margin-top:0'>Yeni Oda Kur</h3>
            <input type='text' id='newChName' placeholder='Oda Adı (Örn: Geyik)'>
            <input type='text' id='newChIcon' placeholder='Simge (Emoji)'>
            <input type='text' id='newChDesc' placeholder='Kısa Açıklama'>
            <div class='modal-btns'>
                <button class='btn-cancel' onclick='closeModal()'>İptal</button>
                <button class='btn-create' onclick='createChannel()'>Oluştur</button>
            </div>
        </div>
    </div>

    <script>
        let selectedChannelId = null;
        
        window.onload = function() {
            const savedNick = localStorage.getItem('chatNick');
            if(savedNick) {
                document.getElementById('nickInput').value = savedNick;
            }
            loadChannels();
            setInterval(updateCounts, 2000);
        };

        document.getElementById('loginForm').addEventListener('submit', function() {
            const nick = document.getElementById('nickInput').value;
            localStorage.setItem('chatNick', nick);
        });

        async function loadChannels() {
            const res = await fetch('/api/channels');
            const channels = await res.json();
            const list = document.getElementById('channelList');
            list.innerHTML = '';

            channels.forEach(ch => {
                const wrapper = document.createElement('div');
                wrapper.className = 'channel-wrapper';

                const card = document.createElement('div');
                card.className = 'channel-card';
                card.id = 'card_' + ch.Id; 
                // BURASI DÜZELTİLDİ: ${{...}} yerine ${...} yapıldı
                card.innerHTML = `<div class='ch-icon'>${ch.Icon}</div>
                                  <div class='ch-name'>${ch.Name}</div>
                                  <div class='ch-count' id='count_${ch.Id}'>👤 ${ch.UserCount}</div>
                                  <div class='ch-desc'>${ch.Desc}</div>`;
                
                card.onmouseup = () => { if(!isDragging) selectChannel(card, ch.Id); };
                
                // BURASI DÜZELTİLDİ: deleteChannel çağrısındaki çift parantezler kaldırıldı
                const btnHTML = `<button class='del-chan-btn' type='button' onclick=""deleteChannel(event, '${ch.Id}')"">SİL</button>`;
                
                wrapper.appendChild(card);
                wrapper.insertAdjacentHTML('beforeend', btnHTML);
                list.appendChild(wrapper);
            });

            const wrapperAdd = document.createElement('div');
            wrapperAdd.className = 'channel-wrapper';
            
            const addCard = document.createElement('div');
            addCard.className = 'channel-card create-card';
            addCard.innerHTML = `<div class='ch-icon'>+</div><div class='ch-name'>Oda Kur</div>`;
            addCard.onmouseup = () => { if(!isDragging) openModal(); };
            
            wrapperAdd.appendChild(addCard);
            list.appendChild(wrapperAdd);
            
            enableDragScroll();
        }

        async function deleteChannel(e, id) {
            e.preventDefault(); 
            e.stopPropagation();

            if(!confirm('Bu odayı silmek istediğine emin misin?')) return;
            
            const fd = new FormData();
            fd.append('channelId', id);
            await fetch('/api/delete_channel', { method: 'POST', body: fd });
            
            loadChannels(); 
            if(selectedChannelId === id) {
                selectedChannelId = null;
                document.getElementById('channelIdInput').value = '';
                checkReady();
            }
        }

        async function updateCounts() {
            try {
                const res = await fetch('/api/channels');
                const channels = await res.json();
                const existingWrappers = document.querySelectorAll('.channel-wrapper');
                if((existingWrappers.length - 1) !== channels.length) { loadChannels(); return; }

                channels.forEach(ch => {
                    const countEl = document.getElementById('count_' + ch.Id);
                    if(countEl) {
                        const newText = '👤 ' + ch.UserCount;
                        if(countEl.innerText !== newText) {
                            countEl.innerText = newText;
                            countEl.style.color = '#fff';
                            setTimeout(() => countEl.style.color = '#4da6ff', 300);
                        }
                    }
                });
            } catch(e) {}
        }

        function selectChannel(card, id) {
            document.querySelectorAll('.channel-card').forEach(c => c.classList.remove('selected'));
            card.classList.add('selected');
            selectedChannelId = id;
            document.getElementById('channelIdInput').value = id;
            checkReady();
        }

        document.getElementById('nickInput').addEventListener('input', checkReady);

        function checkReady() {
            const nick = document.getElementById('nickInput').value.trim();
            document.getElementById('joinBtn').disabled = !(nick && selectedChannelId);
        }

        let isDragging = false;
        function enableDragScroll() {
            const slider = document.getElementById('channelList');
            let startX, scrollLeft, isDown = false;
            slider.addEventListener('mousedown', (e) => { isDown = true; slider.style.cursor = 'grabbing'; startX = e.pageX - slider.offsetLeft; scrollLeft = slider.scrollLeft; isDragging = false; });
            slider.addEventListener('mouseleave', () => { isDown = false; slider.style.cursor = 'grab'; });
            slider.addEventListener('mouseup', () => { isDown = false; slider.style.cursor = 'grab'; });
            slider.addEventListener('mousemove', (e) => { if(!isDown) return; e.preventDefault(); const x = e.pageX - slider.offsetLeft; const walk = (x - startX) * 2; slider.scrollLeft = scrollLeft - walk; if(Math.abs(walk) > 5) isDragging = true; });
        }

        const modal = document.getElementById('createModal');
        function openModal() { modal.style.display = 'flex'; }
        function closeModal() { modal.style.display = 'none'; }
        async function createChannel() {
            const name = document.getElementById('newChName').value;
            const icon = document.getElementById('newChIcon').value || '💬';
            const desc = document.getElementById('newChDesc').value;
            if(!name) return alert('İsim şart!');
            const fd = new FormData();
            fd.append('name', name); fd.append('icon', icon); fd.append('desc', desc);
            await fetch('/api/create_channel', { method: 'POST', body: fd });
            closeModal();
            loadChannels();
        }
    </script>
</body>
</html>";
        }

        public static string GetChatPage(string nick, string channelId)
        {
            string channelName = DbHelper.GetChannelName(channelId);
            string messagesHtml = DbHelper.GetMessagesHtml(channelId, 50);

            // GÜVENLİK FIX: JS String Encoding
            string safeNickJs = HttpUtility.JavaScriptStringEncode(nick);
            string safeChannelIdJs = HttpUtility.JavaScriptStringEncode(channelId);

            return $@"<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{channelName}</title>
    <style>
        body {{ font-family: 'Segoe UI', sans-serif; background: #121212; margin: 0; display: flex; justify-content: center; height: 100vh; color: #eee; }}
        .chat-container {{ width: 100%; max-width: 800px; background: #1e1e1e; display: flex; flex-direction: column; height: 100%; box-shadow: 0 4px 15px rgba(0,0,0,0.5); }}
        .header {{ background: #0078d4; color: white; padding: 15px; text-align: center; display:flex; justify-content:space-between; align-items:center; }}
        .header h1 {{ margin:0; font-size:1.2em; font-weight:bold; }}
        .logout-btn {{ color: white; text-decoration: none; border: 1px solid rgba(255,255,255,0.3); padding: 5px 12px; border-radius: 5px; font-size: 0.9em; background: rgba(0,0,0,0.1); cursor: pointer; }}
        .logout-btn:hover {{ background: rgba(255,50,50,0.4); border-color: red; }}
        .messages {{ flex: 1; padding: 20px; overflow-y: auto; background: #121212; display: flex; flex-direction: column-reverse; align-items: flex-start; scroll-behavior: smooth; }} 
        .message {{ background: #2d2d2d; color: #ddd; padding: 8px 12px; padding-right: 30px !important; margin-bottom: 10px; border-radius: 8px; position: relative; max-width: 80%; width: fit-content; word-wrap: break-word; min-width: 60px; border: 1px solid #333; }}
        .message.media-msg {{ padding: 4px !important; padding-right: 30px !important; display: flex; flex-direction: column; }}
        .message .delete-btn {{ position: absolute; top: 2px; right: 2px; background: none; border: none; color: #555; cursor: pointer; width: 20px; height: 20px; font-weight: bold; font-size: 16px; }}
        .message .delete-btn:hover {{ color: #ff4444; }}
        .message.media-msg video, .message.media-msg img {{ max-width: 300px !important; width: 100%; height: auto; border-radius: 6px; display: block; }}
        .audio-container {{ display: flex; align-items: center; gap: 10px; padding: 5px; width: 100%; min-width: 280px; }}
        .media-header {{ font-size: 0.75em; margin-bottom: 4px; padding-left: 4px; color: #777; }}
        .nick {{ font-weight: bold; color: #4da6ff; }}
        .timestamp {{ font-size: 0.75em; color: #666; margin-right: 5px; }}
        .footer {{ padding: 15px; background: #1e1e1e; border-top: 1px solid #333; position: relative; }}
        .input-group {{ display: flex; gap: 10px; align-items: center; }}
        #messageInput {{ flex: 1; padding: 12px; border: 1px solid #333; border-radius: 20px; outline: none; background: #2d2d2d; color: white; }}
        .submit-btn {{ padding: 10px 20px; background: #0078d4; color: white; border: none; border-radius: 20px; cursor: pointer; }}
        .mic-btn {{ background: #2d2d2d; border: 1px solid #333; border-radius: 50%; width: 42px; height: 42px; cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 20px; color: white; }}
        .mic-btn.recording {{ background: #ff4444; animation: pulse 1.5s infinite; }}
        .file-upload-container {{ margin-top: 10px; border: 2px dashed #444; padding: 10px; text-align: center; border-radius: 10px; position: relative; background: #252525; display: none; }} 
        #fileInput {{ position: absolute; top: 0; left: 0; width: 100%; height: 100%; opacity: 0; cursor: pointer; }}
        .preview-box {{ display: none; margin-top: 10px; align-items: center; flex-direction: column; position: relative; }}
        .show-upload-btn {{ background: #2d2d2d; border: 1px solid #333; width: 42px; height: 42px; border-radius: 50%; cursor: pointer; font-size: 20px; display:flex; align-items:center; justify-content:center; color:white; }}
        .file-attachment a {{ color: #4da6ff; text-decoration: none; }}
        #recordingOverlay {{ display:none; position: absolute; bottom: 80px; left: 50%; transform: translateX(-50%); background: rgba(255,0,0,0.8); color: white; padding: 10px 20px; border-radius: 30px; font-weight: bold; animation: blink 1s infinite; pointer-events: none; white-space: nowrap; }}
        @keyframes blink {{ 50% {{ opacity: 0.5; }} }}
        @keyframes pulse {{ 0% {{ box-shadow: 0 0 0 0 rgba(255, 68, 68, 0.7); }} 70% {{ box-shadow: 0 0 0 10px rgba(255, 68, 68, 0); }} 100% {{ box-shadow: 0 0 0 0 rgba(255, 68, 68, 0); }} }}
    </style>
</head>
<body>
    <div class='chat-container'>
        <div class='header'>
            <h1>{channelName}</h1>
            <button onclick='exitRoom()' class='logout-btn'>Odadan Çık</button>
        </div>
        <div class='messages' id='messages'>{messagesHtml}</div>
        <div class='footer'>
            <div id='recordingOverlay'>🔴 KAYIT YAPILIYOR...</div>
            <form id='chatForm' method='POST' enctype='multipart/form-data'>
                <input type='hidden' name='nick' value='{HttpUtility.HtmlEncode(nick)}'>
                <input type='hidden' name='channelId' value='{channelId}'>
                <input type='hidden' name='ajax' value='true'>
                <div class='input-group'>
                    <button type='button' id='toggleUploadBtn' class='show-upload-btn'>+</button>
                    <input type='text' name='message' id='messageInput' placeholder='Mesaj yaz...' autocomplete='off' autofocus>
                    <button type='button' id='micBtn' class='mic-btn'>🎤</button>
                    <button type='submit' class='submit-btn' id='sendBtn'>Gönder</button>
                </div>
                <div class='file-upload-container' id='dropArea'>
                   <span id='dropText'>Dosya seç</span>
                   <input type='file' name='image' id='fileInput'>
                   <div class='preview-box' id='previewBox'><button type='button' id='cancelFileBtn' style='position:absolute;top:-10px;right:-10px;background:red;color:white;border:none;border-radius:50%;width:20px;height:20px;'>x</button><img id='imagePreview' style='max-height:100px;display:none;'><div id='fileNamePreview'></div></div>
                </div>
            </form>
        </div>
    </div>
    <script>
        const channelId = '{safeChannelIdJs}';
        const nick = '{safeNickJs}';
        const messagesDiv = document.getElementById('messages');
        const messageInput = document.getElementById('messageInput');
        
        window.onload = function() {{
            fetch('/api/enter_room', {{ method: 'POST', body: new URLSearchParams({{nick, channelId}}) }});
        }};
        function exitRoom() {{
            const fd = new FormData(); fd.append('nick', nick);
            fetch('/api/leave_room', {{ method: 'POST', body: fd }})
            .then(() => window.location.href = '/');
        }}
        setInterval(() => {{
            fetch('/api/messages?channelId=' + channelId + '&nick=' + encodeURIComponent(nick))
                .then(r => r.json())
                .then(data => {{
                    const existingIds = new Set();
                    document.querySelectorAll('.message').forEach(el => existingIds.add(el.getAttribute('data-id')));
                    data.reverse().forEach(msg => {{
                        if(!existingIds.has(msg.Id)) {{
                            const temp = document.createElement('div');
                            let css = msg.Content.includes('<img') || msg.Content.includes('<video') || msg.Content.includes('<audio') ? 'message media-msg' : 'message';
                            temp.innerHTML = `<div class='${{css}}' data-id='${{msg.Id}}'>${{msg.Content}}<button class='delete-btn' data-id='${{msg.Id}}' onclick='deleteMsg(this)'>×</button></div>`;
                            messagesDiv.insertAdjacentElement('afterbegin', temp.firstChild);
                            messagesDiv.scrollTop = messagesDiv.scrollHeight; 
                        }}
                    }});
                }});
        }}, 1500);
        function deleteMsg(btn) {{ if(!confirm('Sil?')) return; const fd = new FormData(); fd.append('messageId', btn.getAttribute('data-id')); fetch('/delete', {{ method: 'POST', body: fd }}).then(r=>r.json()).then(d=>{{ if(d.success) btn.parentElement.remove(); }}); }}
        document.getElementById('chatForm').addEventListener('submit', function(e) {{ e.preventDefault(); if(isRecording && mediaRecorder && mediaRecorder.state === 'recording') {{ shouldSendAfterStop = true; mediaRecorder.stop(); return; }} submitForm(this); }});
        function submitForm(form) {{ const fd = new FormData(form); const msgVal = document.getElementById('messageInput').value; if(!msgVal && !document.getElementById('fileInput').files.length) return; document.getElementById('messageInput').value = ''; resetFile(); document.getElementById('dropArea').style.display = 'none'; fetch('/chat', {{ method:'POST', body:fd }}).then(r=>r.json()).then(d=>{{}}); }}
        document.getElementById('toggleUploadBtn').onclick = () => {{ document.getElementById('dropArea').style.display = document.getElementById('dropArea').style.display=='block'?'none':'block'; }};
        const fileInput = document.getElementById('fileInput'); const previewBox = document.getElementById('previewBox'); const imagePreview = document.getElementById('imagePreview'); const fileNamePreview = document.getElementById('fileNamePreview'); const cancelFileBtn = document.getElementById('cancelFileBtn'); const recordingOverlay = document.getElementById('recordingOverlay');
        function resetFile() {{ fileInput.value = ''; previewBox.style.display = 'none'; }}
        cancelFileBtn.addEventListener('click', () => {{ resetFile(); }});
        fileInput.addEventListener('change', function() {{ if (!this.files[0]) return; previewBox.style.display = 'flex'; fileNamePreview.textContent = this.files[0].name; if (this.files[0].type.startsWith('image/')) {{ const reader = new FileReader(); reader.onload = e => {{ imagePreview.src = e.target.result; imagePreview.style.display = 'block'; }}; reader.readAsDataURL(this.files[0]); }} else {{ imagePreview.style.display = 'none'; }} }});
        const micBtn = document.getElementById('micBtn'); let isRecording = false, mediaRecorder, audioChunks = []; let shouldSendAfterStop = false;
        micBtn.addEventListener('click', async () => {{ if (!isRecording) {{ try {{ const stream = await navigator.mediaDevices.getUserMedia({{ audio: true }}); mediaRecorder = new MediaRecorder(stream); audioChunks = []; mediaRecorder.ondataavailable = e => audioChunks.push(e.data); mediaRecorder.onstop = () => {{ const file = new File([new Blob(audioChunks, {{ type: 'audio/webm' }})], 'ses.webm', {{ type: 'audio/webm' }}); const container = new DataTransfer(); container.items.add(file); fileInput.files = container.files; isRecording = false; micBtn.classList.remove('recording'); recordingOverlay.style.display = 'none'; if(shouldSendAfterStop) {{ shouldSendAfterStop = false; submitForm(document.getElementById('chatForm')); }} else {{ previewBox.style.display = 'flex'; fileNamePreview.textContent = '🎤 Ses Kaydı Hazır (Gönder\'e bas)'; document.getElementById('dropArea').style.display = 'block'; }} }}; resetFile(); mediaRecorder.start(); isRecording = true; shouldSendAfterStop = false; micBtn.classList.add('recording'); recordingOverlay.style.display = 'block'; }} catch (e) {{ alert('Mikrofon hatası: ' + e.message); }} }} else {{ mediaRecorder.stop(); }} }});
    </script>
</body>
</html>";
        }
    }
}