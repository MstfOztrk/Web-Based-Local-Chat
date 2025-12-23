const params = new URLSearchParams(window.location.search);
const NICK = params.get('nick');
const CID = params.get('channelId');

if (!NICK || !CID) { location.href = "/"; }

const msgsDiv = document.getElementById('messagesDiv');
const msgInput = document.getElementById('msgInput');
const fileInput = document.getElementById('fileInput');
const previewArea = document.getElementById('previewArea');

// --- 1. MESAJLAŞMA SİSTEMİ ---
let lastMsgId = "";

async function loadMessages() {
    try {
        const res = await fetch(`/api/messages?channelId=${CID}`);
        const data = await res.json();
        
        // Basit diff check: Sadece yeni mesaj varsa DOM'u yorma (Daha profesyonel yapılabilir ama şimdilik yeter)
        const currentFirstId = msgsDiv.lastElementChild?.dataset.id; // Flex-reverse olduğu için last, en üstteki (en eski) olur
        const incomingTopId = data.length > 0 ? data[data.length-1].id : ""; 
        
        // Eğer mesaj sayısı değiştiyse veya hiç mesaj yoksa tekrar çiz
        // Not: Bu basit bir yöntem, titreme yapabilir ama iş görür.
        // Daha pürüzsüz olması için mevcut ID setini kontrol edeceğiz.
        
        const existingIds = new Set();
        document.querySelectorAll('.message').forEach(el => existingIds.add(el.dataset.id));

        data.forEach(msg => {
            if (!existingIds.has(msg.id)) {
                const isMine = msg.nick === NICK;
                const div = document.createElement('div');
                div.className = `message ${isMine ? 'mine' : 'others'}`;
                div.dataset.id = msg.id;
                
                // İçerik tipini düzelt (HTML string geliyor)
                let content = msg.content;
                // HTML classlarını ekle (CSS için)
                content = content.replace('<img', '<img class="media-content"');
                content = content.replace('<video', '<video class="media-content"');
                content = content.replace('<audio', '<audio class="media-audio"'); 

                div.innerHTML = `
                    <div class="del-btn" onclick="deleteMsg('${msg.id}')">×</div>
                    <span class="nick">${msg.nick}</span>
                    <div class="body">${content}</div>
                    <span class="time">${msg.timestamp}</span>
                `;
                msgsDiv.prepend(div); // Flex-direction column-reverse olduğu için prepend aslında alta ekler
            }
        });
    } catch (e) {}
}

// --- 2. DOSYA YÖNETİMİ & PREVIEW ---
function handleFileSelect() {
    const file = fileInput.files[0];
    if (!file) return;

    previewArea.style.display = "flex";
    document.getElementById('previewName').innerText = file.name;

    const imgPreview = document.getElementById('previewImg');
    const iconPreview = document.getElementById('previewFileIcon');

    if (file.type.startsWith('image/')) {
        const reader = new FileReader();
        reader.onload = e => {
            imgPreview.src = e.target.result;
            imgPreview.style.display = "block";
            iconPreview.style.display = "none";
        };
        reader.readAsDataURL(file);
    } else {
        imgPreview.style.display = "none";
        iconPreview.style.display = "block";
    }
}

function clearFile() {
    fileInput.value = "";
    previewArea.style.display = "none";
}

async function sendMessage() {
    const text = msgInput.value.trim();
    const file = fileInput.files[0];

    if (!text && !file) return;

    const fd = new FormData();
    fd.append('nick', NICK);
    fd.append('channelId', CID);
    fd.append('message', text);
    if (file) fd.append('file', file);

    // UI Temizlik
    msgInput.value = '';
    clearFile();

    await fetch('/api/send_message', { method: 'POST', body: fd });
    loadMessages();
}

async function deleteMsg(id) {
    if(!confirm('Sil?')) return;
    const fd = new FormData(); fd.append('messageId', id);
    await fetch('/api/delete_message', { method: 'POST', body: fd });
    document.querySelector(`.message[data-id="${id}"]`).remove();
}

msgInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') sendMessage(); });
setInterval(loadMessages, 1000);


// --- 3. SES KAYDI (VOICE NOTE) ---
let mediaRecorder;
let audioChunks = [];
const micBtn = document.getElementById('micBtn');

micBtn.addEventListener('mousedown', startRecording);
micBtn.addEventListener('mouseup', stopRecording);
micBtn.addEventListener('touchstart', startRecording); // Mobil için
micBtn.addEventListener('touchend', stopRecording);

async function startRecording(e) {
    e.preventDefault();
    try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        mediaRecorder = new MediaRecorder(stream);
        audioChunks = [];
        
        mediaRecorder.ondataavailable = event => {
            audioChunks.push(event.data);
        };

        mediaRecorder.onstop = sendAudio;
        mediaRecorder.start();
        micBtn.classList.add('recording');
    } catch (err) {
        alert("Mikrofon izni yok!");
    }
}

function stopRecording(e) {
    e.preventDefault();
    if (mediaRecorder && mediaRecorder.state !== 'inactive') {
        mediaRecorder.stop();
        micBtn.classList.remove('recording');
        // Stream'i durdur
        mediaRecorder.stream.getTracks().forEach(track => track.stop());
    }
}

async function sendAudio() {
    if (audioChunks.length === 0) return;
    const audioBlob = new Blob(audioChunks, { type: 'audio/webm' });
    const audioFile = new File([audioBlob], "voice_note.webm", { type: 'audio/webm' });

    const fd = new FormData();
    fd.append('nick', NICK);
    fd.append('channelId', CID);
    fd.append('file', audioFile);

    await fetch('/api/send_message', { method: 'POST', body: fd });
    loadMessages();
}


// --- 4. WEBRTC (CANLI SESLİ ARAMA) ---
let localStream = null;
let pcMap = {};
let pollInterval = null;

async function toggleCall() {
    const btn = document.getElementById('voiceCallBtn');
    const pill = document.getElementById('statusPill');
    
    if (localStream) {
        // Ayrıl
        leaveCall();
        btn.innerHTML = '📞 <span id="callText">Katıl</span>';
        btn.style.background = "#0078d4";
        pill.innerText = "Sessiz";
        pill.classList.remove('active');
    } else {
        // Katıl
        try {
            localStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            const fd = new FormData(); fd.append('nick', NICK);
            const res = await fetch('/api/voice/join', { method: 'POST', body: fd });
            const others = await res.json();
            
            others.forEach(user => createPeer(user, true));
            pollInterval = setInterval(pollSignal, 1000);
            
            btn.innerHTML = '🔴 <span id="callText">Ayrıl</span>';
            btn.style.background = "#cf0000";
            pill.innerText = "Yayında";
            pill.classList.add('active');
        } catch(e) { alert("Mikrofon hatası: " + e); }
    }
}

function leaveCall() {
    if (pollInterval) clearInterval(pollInterval);
    if (localStream) localStream.getTracks().forEach(t => t.stop());
    localStream = null;
    Object.values(pcMap).forEach(pc => pc.close());
    pcMap = {};
    const fd = new FormData(); fd.append('nick', NICK);
    fetch('/api/voice/leave', { method: 'POST', body: fd });
}

async function pollSignal() {
    const res = await fetch(`/api/voice/poll?nick=${encodeURIComponent(NICK)}`);
    const signals = await res.json();
    for (const s of signals) {
        let pc = pcMap[s.from];
        if (!pc) pc = createPeer(s.from, false);
        if (s.type === 'offer') {
            await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(s.sdp)));
            const ans = await pc.createAnswer();
            await pc.setLocalDescription(ans);
            sendSignal(s.from, 'answer', JSON.stringify(ans), null);
        } else if (s.type === 'answer') {
            await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(s.sdp)));
        } else if (s.type === 'candidate') {
            await pc.addIceCandidate(new RTCIceCandidate(JSON.parse(s.candidate)));
        }
    }
}

function createPeer(target, initiator) {
    const pc = new RTCPeerConnection({ iceServers: [{ urls: 'stun:stun.l.google.com:19302' }] });
    pcMap[target] = pc;
    localStream.getTracks().forEach(track => pc.addTrack(track, localStream));
    pc.ontrack = (e) => {
        const audio = new Audio();
        audio.srcObject = e.streams[0];
        audio.play();
    };
    pc.onicecandidate = (e) => { if (e.candidate) sendSignal(target, 'candidate', null, JSON.stringify(e.candidate)); };
    if (initiator) {
        pc.createOffer().then(o => { pc.setLocalDescription(o); sendSignal(target, 'offer', JSON.stringify(o), null); });
    }
    return pc;
}

function sendSignal(to, type, sdp, candidate) {
    const fd = new FormData();
    fd.append('from', NICK); fd.append('to', to); fd.append('type', type);
    if(sdp) fd.append('sdp', sdp); if(candidate) fd.append('candidate', candidate);
    fetch('/api/voice/signal', { method: 'POST', body: fd });
}