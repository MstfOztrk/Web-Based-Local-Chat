using ChatApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Servisler
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<VoiceManager>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

app.UseCors();

// index.html, chat.html gibi dosyaları dışarı açar
app.UseDefaultFiles(); 
app.UseStaticFiles(); 

// Upload klasörünü ayarla
var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

// --- API ENDPOINTLERİ (HTML DÖNMEZ, SADECE JSON) ---

app.MapGet("/api/channels", (DatabaseService db) => Results.Json(db.GetChannels()));

app.MapPost("/api/create_channel", async (HttpContext ctx, DatabaseService db) => {
    var form = await ctx.Request.ReadFormAsync();
    db.CreateChannel(form["name"], form["icon"], form["desc"]);
    return Results.Ok(new { success = true });
});

app.MapPost("/api/delete_channel", async (HttpContext ctx, DatabaseService db) => {
    var form = await ctx.Request.ReadFormAsync();
    db.DeleteChannel(form["channelId"]);
    return Results.Ok(new { success = true });
});

app.MapGet("/api/messages", (HttpContext ctx, DatabaseService db) => {
    string cid = ctx.Request.Query["channelId"];
    return Results.Json(db.GetMessages(cid));
});

app.MapPost("/api/send_message", async (HttpContext ctx, DatabaseService db) => {
    var form = await ctx.Request.ReadFormAsync();
    string nick = form["nick"];
    string cid = form["channelId"];
    string msg = form["message"];
    string ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    
    if (form.Files.Count > 0) {
        var file = form.Files[0];
        string ext = Path.GetExtension(file.FileName).ToLower();
        string newName = Guid.NewGuid() + ext;
        using (var stream = File.Create(Path.Combine(uploadPath, newName))) {
            await file.CopyToAsync(stream);
        }
        
        string webPath = $"/uploads/{newName}";
        string content = "";
        
        if (new[] { ".jpg", ".png", ".gif" }.Contains(ext))
            content = $"<img src='{webPath}' class='media-img'>";
        else if (new[] { ".mp4", ".webm" }.Contains(ext))
            content = $"<video controls src='{webPath}' class='media-vid'></video>";
        else if (new[] { ".mp3", ".wav" }.Contains(ext))
            content = $"<audio controls src='{webPath}'></audio>";
        else
            content = $"<a href='{webPath}' target='_blank'>📎 {file.FileName}</a>";

        db.SaveMessage(cid, nick, content, ip);
    }
    else if (!string.IsNullOrWhiteSpace(msg)) {
        db.SaveMessage(cid, nick, msg, ip);
    }
    return Results.Ok(new { success = true });
});

app.MapPost("/api/delete_message", async (HttpContext ctx, DatabaseService db) => {
    var form = await ctx.Request.ReadFormAsync();
    db.DeleteMessage(form["messageId"]);
    return Results.Ok(new { success = true });
});

// Ses Sistemi (WebRTC)
app.MapPost("/api/voice/join", async (HttpContext ctx, VoiceManager vm) => {
    var form = await ctx.Request.ReadFormAsync();
    return Results.Json(vm.Join(form["nick"]));
});
app.MapGet("/api/voice/poll", (HttpContext ctx, VoiceManager vm) => Results.Json(vm.Poll(ctx.Request.Query["nick"])));
app.MapPost("/api/voice/signal", async (HttpContext ctx, VoiceManager vm) => {
    var form = await ctx.Request.ReadFormAsync();
    vm.Signal(form["from"], form["to"], form["type"], form["sdp"], form["candidate"]);
    return Results.Ok(new { success = true });
});
app.MapPost("/api/voice/leave", async (HttpContext ctx, VoiceManager vm) => {
    var form = await ctx.Request.ReadFormAsync();
    vm.Leave(form["nick"]);
    return Results.Ok(new { success = true });
});

app.Run();