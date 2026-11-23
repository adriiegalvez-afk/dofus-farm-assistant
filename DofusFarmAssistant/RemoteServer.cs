using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace DofusFarmAssistant;

public sealed class RemoteServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Monitor monitor;
    private readonly Settings settings;
    private readonly Action capture;
    private CancellationTokenSource? stop;
    private readonly ConcurrentDictionary<string, DateTime> sessions = new();
    private int failedLogins;
    private DateTime blockedUntil = DateTime.MinValue;
    public int Port { get; } = 8765;
    public bool Running => listener.IsListening;
    public RemoteServer(Monitor m, Settings s, Action takeCapture) { monitor = m; settings = s; capture = takeCapture; }
    public void Start()
    {
        if (Running) return;
        listener.Prefixes.Add($"http://127.0.0.1:{Port}/"); listener.Start(); stop = new(); _ = Loop(stop.Token);
    }
    public void Stop() { stop?.Cancel(); try { listener.Stop(); } catch { } }
    private async Task Loop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext ctx; try { ctx = await listener.GetContextAsync(); } catch { break; }
            _ = Task.Run(() => Handle(ctx), token);
        }
    }
    private async Task Handle(HttpListenerContext c)
    {
        try
        {
            var path = c.Request.Url?.AbsolutePath ?? "/";
            if (path == "/") { await Text(c, "text/html; charset=utf-8", Page); return; }
            if (path == "/api/login" && c.Request.HttpMethod == "POST")
            {
                if (DateTime.UtcNow < blockedUntil) { c.Response.StatusCode=429; await Text(c,"application/json","{\"error\":\"Réessayez plus tard\"}"); return; }
                var data = await JsonSerializer.DeserializeAsync<Dictionary<string,string>>(c.Request.InputStream);
                var pass = data?.GetValueOrDefault("password") ?? "";
                if (settings.RemotePasswordHash.Length == 0 || !Monitor.VerifyPassword(pass,settings.RemotePasswordHash)) { if(++failedLogins>=5){blockedUntil=DateTime.UtcNow.AddMinutes(10);failedLogins=0;} c.Response.StatusCode = 401; await Text(c,"application/json","{\"error\":\"Accès refusé\"}"); return; }
                failedLogins=0;var token=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));sessions[token]=DateTime.UtcNow.AddHours(12);
                c.Response.AppendHeader("Set-Cookie", $"dfa={Uri.EscapeDataString(token)}; HttpOnly; Secure; SameSite=Strict; Path=/; Max-Age=43200"); await Text(c,"application/json","{\"ok\":true}"); return;
            }
            if (!Authenticated(c)) { c.Response.StatusCode = 401; await Text(c,"application/json","{\"error\":\"Authentification requise\"}"); return; }
            if (path == "/api/status") { await Text(c,"application/json",JsonSerializer.Serialize(monitor.GetSnapshot())); return; }
            if (path == "/api/events") { await Text(c,"application/json",JsonSerializer.Serialize(Store.LoadEvents().TakeLast(100).Reverse())); return; }
            if (path == "/api/captures") { var names = Directory.GetFiles(Store.Shots,"*.png").OrderByDescending(File.GetCreationTime).Select(Path.GetFileName); await Text(c,"application/json",JsonSerializer.Serialize(names)); return; }
            if (path.StartsWith("/capture/",StringComparison.Ordinal) && Path.GetFileName(path[9..]) == path[9..]) { var f = Path.Combine(Store.Shots,path[9..]); if (!File.Exists(f)) { c.Response.StatusCode=404; c.Response.Close(); return; } c.Response.ContentType="image/png"; await using var fs=File.OpenRead(f); await fs.CopyToAsync(c.Response.OutputStream); c.Response.Close(); return; }
            if (path == "/api/capture" && c.Request.HttpMethod == "POST") { capture(); await Text(c,"application/json","{\"ok\":true}"); return; }
            c.Response.StatusCode=404; c.Response.Close();
        } catch { try { c.Response.StatusCode=500; c.Response.Close(); } catch { } }
    }
    private bool Authenticated(HttpListenerContext c)
    {
        var raw = c.Request.Headers["Cookie"] ?? ""; var value=raw.Split(';').Select(x=>x.Trim()).FirstOrDefault(x=>x.StartsWith("dfa="))?.Substring(4);
        try { var token=value is null?"":Uri.UnescapeDataString(value); if(!sessions.TryGetValue(token,out var expiry))return false;if(expiry<DateTime.UtcNow){sessions.TryRemove(token,out _);return false;}return true; } catch { return false; }
    }
    private static async Task Text(HttpListenerContext c,string type,string body) { c.Response.ContentType=type; c.Response.Headers.Add("X-Content-Type-Options","nosniff"); c.Response.Headers.Add("Content-Security-Policy","default-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src 'self' data:"); var b=Encoding.UTF8.GetBytes(body); c.Response.ContentLength64=b.Length; await c.Response.OutputStream.WriteAsync(b); c.Response.Close(); }
    private const string Page = """
<!doctype html><html lang="fr"><meta name="viewport" content="width=device-width,initial-scale=1"><meta charset="utf-8"><title>Dofus Farm Assistant</title><style>body{margin:0;background:#10141c;color:#e7edf7;font:16px system-ui}main{max-width:900px;margin:auto;padding:22px}.card{background:#1a2230;padding:18px;border-radius:14px;margin:12px 0}button,input{padding:12px;border-radius:8px;border:1px solid #33445a;background:#101722;color:#fff}button{background:#7158e2;cursor:pointer}.muted{color:#9aacbf}.good{color:#5dde9b}.bad{color:#ff697c}#content{display:none}li{padding:7px}</style><main><h1>DOFUS FARM ASSISTANT</h1><p class="muted">Surveillance locale · Connexion privée</p><section id="login" class="card"><h2>Connexion</h2><input id="pw" type="password" placeholder="Mot de passe distant"><button onclick="login()">Se connecter</button><p id="err"></p></section><section id="content"><div class="card"><h2>État : <span id="state">…</span></h2><p>Fonctionnement : <span id="duration">—</span> · Événements : <span id="count">0</span></p><button onclick="capture()">Demander une capture</button></div><div class="card"><h2>Captures</h2><div id="shots"></div></div><div class="card"><h2>Historique récent</h2><ul id="events"></ul></div></section></main><script>const esc=s=>String(s).replace(/[&<>\"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;',"'":'&#39;'}[c]));async function login(){let r=await fetch('/api/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({password:document.querySelector('#pw').value})});if(r.ok){document.querySelector('#login').style.display='none';document.querySelector('#content').style.display='block';refresh()}else document.querySelector('#err').textContent='Mot de passe incorrect'}async function refresh(){try{let s=await(await fetch('/api/status')).json();let e=await(await fetch('/api/events')).json();let c=await(await fetch('/api/captures')).json();document.querySelector('#state').textContent=s.state;document.querySelector('#state').className=s.state==='Jeu ouvert'?'good':'bad';document.querySelector('#duration').textContent=s.runtimeSeconds?new Date(s.runtimeSeconds*1000).toISOString().slice(11,19):'—';document.querySelector('#count').textContent=s.eventCount;document.querySelector('#events').innerHTML=e.map(x=>'<li>'+esc(new Date(x.timestamp).toLocaleString())+' · '+esc(x.type)+' · '+esc(x.details)+'</li>').join('');document.querySelector('#shots').innerHTML=c.map(x=>'<div><p>'+esc(x)+'</p><a href="/capture/'+encodeURIComponent(x)+'"><img style="max-width:100%;border-radius:8px" src="/capture/'+encodeURIComponent(x)+'"></a></div>').join('')}catch{document.querySelector('#login').style.display='block';document.querySelector('#content').style.display='none'}}async function capture(){await fetch('/api/capture',{method:'POST'});refresh()}setInterval(()=>{if(document.querySelector('#content').style.display==='block')refresh()},5000)</script></html>
""";
    public void Dispose() { Stop(); listener.Close(); }
}
