document.addEventListener("DOMContentLoaded", () => {
  // HTML elementlerini tanımla
  const messagesDiv = document.getElementById("messages");
  const chatForm = document.getElementById("chatForm");
  const messageInput = document.getElementById("messageInput");
  const fileInput = document.getElementById("fileInput");
  const dropzone = document.getElementById("dropzone");
  const previewContainer = document.querySelector(".preview-container");
  const uploadPreview = document.getElementById("uploadPreview");
  const ajaxFlag = document.getElementById("ajaxFlag");

  // Mesaj sayısı için değişken
  let lastMessageCount = initialMessageCount; // initialMessageCount HTML'den geliyor
  let refreshInterval;

  function init() {
    setupEventListeners();
    startAutoRefresh();
    messageInput.focus();
  }

  function setupEventListeners() {
    // Form gönderme işlemi
    chatForm.addEventListener("submit", handleSubmit);

    // Enter ile mesaj gönderme
    messageInput.addEventListener("keydown", (e) => {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        handleSubmit(e);
      }
    });

    // Dosya seçildiğinde önizleme
    fileInput.addEventListener("change", handleFileSelect);

    // Dosya sürükle-bırak olayları
    ["dragover", "dragleave", "drop"].forEach((event) => {
      dropzone.addEventListener(event, (e) => {
        e.preventDefault();
        if (event === "dragover") dropzone.classList.add("drag-over");
        else if (event === "dragleave") dropzone.classList.remove("drag-over");
        else if (event === "drop") {
          dropzone.classList.remove("drag-over");
          if (e.dataTransfer.files.length) {
            fileInput.files = e.dataTransfer.files;
            handleFileSelect();
          }
        }
      });
    });

    // Silme butonları için event delegasyonu
    messagesDiv.addEventListener("click", (e) => {
      // Eğer tıklanan eleman delete-btn sınıfına sahipse
      if (e.target.classList.contains("delete-btn")) {
        const messageId = e.target.getAttribute("data-id");
        if (
          messageId &&
          confirm("Bu mesajı silmek istediğinize emin misiniz?")
        ) {
          deleteMessage(messageId);
        }
      }
    });
  }

  function startAutoRefresh() {
    refreshInterval = setInterval(fetchNewMessages, 2000);
  }

  async function handleSubmit(e) {
    e.preventDefault();

    // Ajax olarak gönder
    ajaxFlag.value = "true";

    const formData = new FormData(chatForm);

    try {
      const response = await fetch("/chat", {
        method: "POST",
        body: formData,
      });

      if (response.ok) {
        const result = await response.json();

        // Mesaj kutusunu temizle
        messageInput.value = "";
        fileInput.value = "";
        previewContainer.style.display = "none";

        // Yeni mesajları getir
        await fetchNewMessages();
      }
    } catch (error) {
      console.error("Mesaj gönderirken hata:", error);
    }
  }

  function handleFileSelect() {
    if (fileInput.files && fileInput.files[0]) {
      const reader = new FileReader();
      reader.onload = (e) => {
        uploadPreview.src = e.target.result;
        previewContainer.style.display = "block";
      };
      reader.readAsDataURL(fileInput.files[0]);
    }
  }

  async function fetchNewMessages() {
    try {
      const response = await fetch(`/messages?lastCount=${lastMessageCount}`);
      const data = await response.json();

      if (data.messages?.length > 0) {
        const fragment = document.createDocumentFragment();

        data.messages.forEach((msg) => {
          // Yeni mesaj div'ini ekle
          const div = document.createElement("div");
          div.className = "message";
          div.setAttribute("data-id", msg.id);
          div.innerHTML = msg.content;

          // Sil butonunu ekle
          const deleteBtn = document.createElement("button");
          deleteBtn.className = "delete-btn";
          deleteBtn.setAttribute("data-id", msg.id);
          deleteBtn.textContent = "×";

          div.appendChild(deleteBtn);
          fragment.appendChild(div);
        });

        // Mesajları en yeni üstte olacak şekilde ekle
        messagesDiv.insertBefore(fragment, messagesDiv.firstChild);
        lastMessageCount = data.count;
      }
    } catch (error) {
      console.error("Mesajlar alınırken hata:", error);
    }
  }

  async function deleteMessage(messageId) {
    try {
      const formData = new FormData();
      formData.append("messageId", messageId);

      const response = await fetch("/delete", {
        method: "POST",
        body: formData,
      });

      const result = await response.json();

      if (result.success) {
        // Mesajı DOM'dan kaldır
        const messageElement = document.querySelector(
          `.message[data-id="${messageId}"]`
        );
        if (messageElement) {
          messageElement.remove();
        }
        lastMessageCount = result.count;
      } else {
        alert("Silme işlemi başarısız: " + (result.error || "Bilinmeyen hata"));
      }
    } catch (error) {
      console.error("Silme hatası:", error);
      alert("Bir hata oluştu: " + error.message);
    }
  }

  // Sayfa kapanırken interval'i temizle
  window.addEventListener("beforeunload", () => {
    clearInterval(refreshInterval);
  });

  // Başlangıç fonksiyonunu çağır
  init();
});
