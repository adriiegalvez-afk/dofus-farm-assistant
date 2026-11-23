using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DofusFarmAssistant;

public sealed class Monitor : IDisposable
{
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly Settings settings;
    private readonly Action changed;
    private DateTime? startedAt;
    private DateTime? lastEvent;
    private string state = "État inconnu";
    private bool monitoring;
    private bool wasOpen;
    private DateTime lastShot = DateTime.MinValue;
    private string? lastScreenshot;
    private readonly HashSet<int> hungPids = [];
    public bool IsMonitoring => monitoring;
    public string State => state;
    public Monitor(Settings s, Action onChange) { settings = s; changed = onChange; timer.Tick += (_, _) => Tick(); }
    public void Start() { if (monitoring) return; monitoring = true; startedAt = DateTime.Now; Store.AddEvent("Surveillance démarrée", "Surveillance du processus " + settings.ProcessName); timer.Interval = Math.Clamp(settings.PollSeconds, 1, 60) * 1000; timer.Start(); Tick(); }
    public void Stop() { if (!monitoring) return; timer.Stop(); monitoring = false; state = "Surveillance arrêtée"; Store.AddEvent("Surveillance arrêtée", "Arrêt demandé par l’utilisateur"); Alert("Surveillance arrêtée"); changed(); }
    private void Tick()
    {
        if (!monitoring) return;
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(settings.ProcessName));
        var open = processes.Length > 0;
        var hung = false;
        foreach (var p in processes) { try { if (p.Responding == false) { hung = true; if (hungPids.Add(p.Id)) { Log("Processus bloqué", $"Le processus {p.ProcessName} ne répond plus."); if (settings.NotifyHang) Alert("Processus bloqué"); } } else hungPids.Remove(p.Id); } catch { } }
        if (hung) state = "Processus bloqué";
        else if (open) state = "Jeu ouvert";
        else state = "Jeu fermé";
        if (open && !wasOpen) Log("Lancement détecté", "Processus " + settings.ProcessName + " détecté.");
        if (!open && wasOpen) { Log("Fermeture détectée", "Le processus s’est fermé. Cela ne confirme pas une déconnexion du compte."); if (settings.NotifyClose) Alert("Dofus fermé"); }
        wasOpen = open;
        if (open && settings.ScreenshotMinutes > 0 && (DateTime.Now - lastShot).TotalMinutes >= settings.ScreenshotMinutes) TakeScreenshot();
        Cleanup(); changed();
    }
    private void Log(string type, string details) { lastEvent = DateTime.Now; Store.AddEvent(type, details); }
    private void Alert(string subject)
    {
        if (!settings.EmailEnabled || string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.MailTo)) return;
        try { using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = true, Credentials = new NetworkCredential(settings.SmtpUser, Unprotect(settings.SmtpPasswordProtected)) }; client.Send(new MailMessage(settings.SmtpUser, settings.MailTo, "Dofus Farm Assistant – " + subject, DateTime.Now.ToString("G") + " : " + subject)); Store.AddEvent("Notification envoyée", subject); } catch (Exception ex) { Store.AddEvent("Échec notification", ex.Message); }
    }
    public string? TakeScreenshot()
    {
        try { var name = "capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"; var path = Path.Combine(Store.Shots, name); using var bmp = new Bitmap(Screen.PrimaryScreen?.Bounds.Width ?? 1280, Screen.PrimaryScreen?.Bounds.Height ?? 720); using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(Screen.PrimaryScreen?.Bounds.Location ?? Point.Empty, Point.Empty, bmp.Size); bmp.Save(path, ImageFormat.Png); lastScreenshot = name; lastShot = DateTime.Now; Log("Capture d’écran", name); Cleanup(); changed(); return path; } catch (Exception ex) { Store.AddEvent("Échec capture", ex.Message); return null; }
    }
    private void Cleanup()
    {
        try { foreach (var f in new DirectoryInfo(Store.Shots).GetFiles("*.png").OrderByDescending(x => x.CreationTime)) if ((DateTime.Now - f.CreationTime).TotalDays > settings.KeepDays) f.Delete(); var files = new DirectoryInfo(Store.Shots).GetFiles("*.png").OrderByDescending(x => x.CreationTime).ToList(); long total = 0; foreach (var f in files) { total += f.Length; if (total > (long)settings.MaxStorageMb * 1024 * 1024) f.Delete(); } } catch { }
    }
    public Snapshot GetSnapshot() => new(state, startedAt, startedAt is null ? null : (long)(DateTime.Now - startedAt.Value).TotalSeconds, lastEvent ?? Store.LoadEvents().LastOrDefault()?.Timestamp, Store.LoadEvents().Count, lastScreenshot, monitoring);
    public void Dispose() => timer.Dispose();
    public static string Hash(string s) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    public static string CreatePasswordHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 310000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
    }
    public static bool VerifyPassword(string password, string stored)
    {
        try { var parts=stored.Split('.'); var expected=Convert.FromBase64String(parts[1]); var actual=Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(parts[0]),310000,HashAlgorithmName.SHA256,32); return CryptographicOperations.FixedTimeEquals(actual,expected); } catch { return false; }
    }
    public static string Protect(string s) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));
    public static string Unprotect(string s) { try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser)); } catch { return ""; } }
}
