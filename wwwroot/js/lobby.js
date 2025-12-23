// Nickname Hatırlama QoL
const savedNick = localStorage.getItem('chatNick');
if(savedNick) document.getElementById('nickInput').value = savedNick;

async function loadChannels() {
    try {
        const res = await fetch('/api/channels');
        const channels = await res.json();
        const list = document.getElementById('channelList');
        list.innerHTML = '';

        channels.forEach(ch => {
            const div = document.createElement('div');
            div.className = 'channel-item';
            div.onclick = () => selectChannel(div, ch.id);
            div.innerHTML = `
                <div style="font-size:2em">${ch.icon}</div>
                <div style="font-weight:bold; margin-top:5px; font-size:0.9em">${ch.name}</div>
                <div style="position:absolute; top:5px; right:5px; color:#555; font-size:10px" onclick="deleteChannel(event, '${ch.id}')">❌</div>
            `;
            list.appendChild(div);
        });
    } catch(e) { console.log("Offline?"); }
}

let selectedChannelId = null;
function selectChannel(el, id) {
    document.querySelectorAll('.channel-item').forEach(c => c.classList.remove('selected'));
    el.classList.add('selected');
    selectedChannelId = id;
    document.getElementById('channelIdInput').value = id;
    checkReady();
}

function checkReady() {
    const nick = document.getElementById('nickInput').value;
    document.getElementById('joinBtn').disabled = !(nick && selectedChannelId);
}

// Form gönderilmeden önce nicki kaydet
document.getElementById('loginForm').addEventListener('submit', () => {
    localStorage.setItem('chatNick', document.getElementById('nickInput').value);
});

async function createChannel() {
    const fd = new FormData();
    fd.append('name', document.getElementById('newChName').value);
    fd.append('icon', document.getElementById('newChIcon').value);
    fd.append('desc', document.getElementById('newChDesc').value);
    await fetch('/api/create_channel', { method: 'POST', body: fd });
    loadChannels();
}

async function deleteChannel(e, id) {
    e.stopPropagation();
    if(!confirm('Odayı silmek istiyor musun?')) return;
    const fd = new FormData(); fd.append('channelId', id);
    await fetch('/api/delete_channel', { method: 'POST', body: fd });
    loadChannels();
}

document.getElementById('nickInput').addEventListener('input', checkReady);
loadChannels();
setInterval(loadChannels, 3000);