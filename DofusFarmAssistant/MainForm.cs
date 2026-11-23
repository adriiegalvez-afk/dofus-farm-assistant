using Microsoft.Win32;
using System.Net.Mail;
using System.Diagnostics;

namespace DofusFarmAssistant;

public sealed class MainForm : Form
{
    private readonly Settings settings = Store.LoadSettings();
    private readonly Monitor monitor;
    private RemoteServer? remote;
    private readonly Label state = new();
    private readonly Label duration = new();
    private readonly Label eventCount = new();
    private readonly Label latestEvent = new();
    private readonly Label lastShot = new();
    private readonly Label remoteStatus = new();
    private readonly TextBox process = new();
    private readonly ComboBox client = new();
    private readonly NumericUpDown poll = new(), shotEvery = new(), keepDays = new(), storage = new();
    private readonly TextBox remotePass = new(), smtpHost = new(), smtpUser = new(), smtpPass = new(), mailTo = new();
    private readonly NumericUpDown smtpPort = new();
    private readonly CheckBox email = new(), notifyClose = new(), notifyHang = new(), notifyPotential = new(), autoMonitor = new(), autoWindows = new();
    private readonly ListBox history = new(), captures = new();
    private readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 1000 };
    public MainForm()
    {
        Text = "Dofus Farm Assistant"; MinimumSize = new Size(800, 620); Size = new Size(1100, 800); StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(16, 20, 28); ForeColor = Color.FromArgb(231, 237, 247); Font = new Font("Segoe UI", 10);
        monitor = new Monitor(settings, RefreshDashboard);
        BuildUi(); LoadFields(); uiTimer.Tick += (_, _) => RefreshDashboard(); uiTimer.Start();
        if (settings.AutoStartWindows) SetStartup(true);
        if (settings.AutoStartMonitoring) monitor.Start();
        if (settings.RemoteEnabled) StartRemote();
        FormClosing += (_, _) => { SaveFields(); monitor.Dispose(); remote?.Dispose(); uiTimer.Dispose(); };
    }
    private void BuildUi()
    {
        poll.Minimum=1; poll.Maximum=60;
        shotEvery.Minimum=0; shotEvery.Maximum=1440;
        keepDays.Minimum=1; keepDays.Maximum=3650;
        storage.Minimum=50; storage.Maximum=100000;
        smtpPort.Minimum=1; smtpPort.Maximum=65535;
        var root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=2, Padding=new Padding(18), BackColor=BackColor }; root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); Controls.Add(root);
        var title = new Label { Text="DOFUS FARM ASSISTANT", Dock=DockStyle.Fill, Font=new Font("Segoe UI Semibold",21,FontStyle.Bold), ForeColor=Color.White, TextAlign=ContentAlignment.MiddleLeft }; root.Controls.Add(title,0,0);
        var tabs = new TabControl { Dock=DockStyle.Fill }; root.Controls.Add(tabs,0,1);
        var dash = Page(tabs,"Tableau de bord"); var grid = new TableLayoutPanel { Dock=DockStyle.Top, ColumnCount=2, RowCount=4, Padding=new Padding(16), Height=300 }; grid.ColumnStyles.Add(new(SizeType.Percent,50)); grid.ColumnStyles.Add(new(SizeType.Percent,50)); dash.Controls.Add(grid);
        state.Font=new Font("Segoe UI Semibold",20,FontStyle.Bold); grid.Controls.Add(Card("ÉTAT ACTUEL",state),0,0); grid.Controls.Add(Card("DURÉE DE SURVEILLANCE",duration),1,0); grid.Controls.Add(Card("DERNIER ÉVÉNEMENT",latestEvent),0,1); grid.Controls.Add(Card("ÉVÉNEMENTS ENREGISTRÉS",eventCount),1,1); grid.Controls.Add(Card("DERNIÈRE CAPTURE",lastShot),0,2); grid.Controls.Add(Card("TABLEAU DE BORD DISTANT",remoteStatus),1,2);
        var actions = new FlowLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(16), AutoSize=true }; dash.Controls.Add(actions); actions.Controls.Add(Button("Démarrer la surveillance",()=>monitor.Start())); actions.Controls.Add(Button("Arrêter la surveillance",()=>monitor.Stop())); actions.Controls.Add(Button("Prendre une capture",()=>{if(monitor.TakeScreenshot() is null) MessageBox.Show("La capture a échoué. Vérifiez les permissions de session.");})); actions.Controls.Add(Button("Actualiser l’historique",RefreshLists)); actions.Controls.Add(Button("Ouvrir le dossier des données",()=>Process.Start("explorer.exe",Store.Root)));
        var hist=Page(tabs,"Historique"); history.Dock=DockStyle.Fill; hist.Controls.Add(history);
        var shotPage=Page(tabs,"Captures"); var shotButtons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50}; shotButtons.Controls.Add(Button("Ouvrir la capture",OpenSelectedCapture)); shotButtons.Controls.Add(Button("Actualiser",RefreshLists)); shotPage.Controls.Add(captures); captures.Dock=DockStyle.Fill; shotPage.Controls.Add(shotButtons);
        var notif=Page(tabs,"Notifications"); var nf=FormGrid(notif); AddRow(nf,"Notifications e-mail",email); AddRow(nf,"Serveur SMTP",smtpHost); AddRow(nf,"Port",smtpPort); AddRow(nf,"Utilisateur SMTP",smtpUser); AddRow(nf,"Mot de passe SMTP",smtpPass); smtpPass.UseSystemPasswordChar=true; AddRow(nf,"Adresse destinataire",mailTo); AddRow(nf,"Alerter si le processus ferme",notifyClose); AddRow(nf,"Alerter si le processus est bloqué",notifyHang); AddRow(nf,"Déconnexion potentielle (aucun indicateur intégré)",notifyPotential); nf.Controls.Add(Button("Envoyer un e-mail de test",SendTestMail));
        var conf=Page(tabs,"Paramètres"); var cf=FormGrid(conf); client.DropDownStyle=ComboBoxStyle.DropDownList; client.Items.AddRange(["Dofus Unity","Dofus Retro","Personnalisé"]); AddRow(cf,"Type de client",client); AddRow(cf,"Nom du processus (sans .exe)",process); AddRow(cf,"Intervalle de surveillance (secondes)",poll); AddRow(cf,"Capture automatique (minutes, 0 = désactivée)",shotEvery); AddRow(cf,"Conserver les captures (jours)",keepDays); AddRow(cf,"Limite de stockage (Mo)",storage); AddRow(cf,"Démarrer la surveillance au lancement",autoMonitor); AddRow(cf,"Lancer avec Windows",autoWindows); cf.Controls.Add(Button("Enregistrer les paramètres",()=>{SaveFields(); MessageBox.Show("Paramètres enregistrés.");}));
        var sec=Page(tabs,"Sécurité"); var sf=FormGrid(sec); AddRow(sf,"Mot de passe du tableau de bord",remotePass); remotePass.UseSystemPasswordChar=true; AddRow(sf,"Démarrer le serveur local",remoteStatus); sf.Controls.Add(Button("Enregistrer le mot de passe",()=>{if(remotePass.Text.Length<14){MessageBox.Show("Choisissez un mot de passe d’au moins 14 caractères.");return;}settings.RemotePasswordHash=Monitor.CreatePasswordHash(remotePass.Text);remotePass.Clear();SaveFields();MessageBox.Show("Mot de passe enregistré. Il n’est pas conservé en clair.");})); sf.Controls.Add(Button("Démarrer le tableau de bord",()=>{SaveFields(); StartRemote();})); sf.Controls.Add(Button("Arrêter le tableau de bord",()=>{remote?.Stop();remoteStatus.Text="Arrêté";})); sf.Controls.Add(new Label{Text="Serveur lié à 127.0.0.1 uniquement. Pour l’iPhone, installez Tailscale sur les deux appareils, puis activez Tailscale Serve sur le PC vers http://127.0.0.1:8765. N’utilisez pas Funnel et n’ouvrez pas de port routeur.",AutoSize=true,MaximumSize=new Size(750,0),ForeColor=Color.LightSteelBlue});
        RefreshLists(); RefreshDashboard();
    }
    private static TabPage Page(TabControl tabs,string name){var p=new TabPage(name){BackColor=Color.FromArgb(16,20,28),ForeColor=Color.White,Padding=new Padding(12)};tabs.TabPages.Add(p);return p;}
    private static Control Card(string caption,Control value){var p=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(26,34,48),Margin=new Padding(8),Padding=new Padding(12)};p.Controls.Add(value);value.Dock=DockStyle.Fill;var l=new Label{Text=caption,Dock=DockStyle.Top,Height=28,ForeColor=Color.LightSteelBlue,Font=new Font("Segoe UI",9,FontStyle.Bold)};p.Controls.Add(l);l.BringToFront();return p;}
    private static FlowLayoutPanel FormGrid(Control parent){var f=new FlowLayoutPanel{Dock=DockStyle.Top,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(18)};parent.Controls.Add(f);return f;}
    private static void AddRow(FlowLayoutPanel f,string caption,Control control){var p=new Panel{Width=790,Height=48,Margin=new Padding(2)};var l=new Label{Text=caption,Width=330,Height=35,TextAlign=ContentAlignment.MiddleLeft};control.SetBounds(340,4,390,34);p.Controls.Add(l);p.Controls.Add(control);f.Controls.Add(p);}
    private static Button Button(string text,Action action){var b=new Button{Text=text,AutoSize=true,Height=40,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(113,88,226),ForeColor=Color.White,Margin=new Padding(6)};b.Click+=(_,_)=>action();return b;}
    private void LoadFields(){process.Text=settings.ProcessName;client.SelectedItem=settings.ClientType;if(client.SelectedIndex<0)client.SelectedIndex=0;poll.Value=Math.Clamp(settings.PollSeconds,1,60);shotEvery.Value=Math.Clamp(settings.ScreenshotMinutes,0,1440);keepDays.Value=Math.Clamp(settings.KeepDays,1,3650);storage.Value=Math.Clamp(settings.MaxStorageMb,50,100000);autoMonitor.Checked=settings.AutoStartMonitoring;autoWindows.Checked=settings.AutoStartWindows;smtpHost.Text=settings.SmtpHost;smtpPort.Value=Math.Clamp(settings.SmtpPort,1,65535);smtpUser.Text=settings.SmtpUser;smtpPass.Text=Monitor.Unprotect(settings.SmtpPasswordProtected);mailTo.Text=settings.MailTo;email.Checked=settings.EmailEnabled;notifyClose.Checked=settings.NotifyClose;notifyHang.Checked=settings.NotifyHang;notifyPotential.Checked=settings.NotifyPotentialDisconnect;}
    private void SaveFields(){settings.ProcessName=string.IsNullOrWhiteSpace(process.Text)?"Dofus":process.Text.Trim();settings.ClientType=client.SelectedItem?.ToString()??"Dofus Unity";settings.PollSeconds=(int)poll.Value;settings.ScreenshotMinutes=(int)shotEvery.Value;settings.KeepDays=(int)keepDays.Value;settings.MaxStorageMb=(int)storage.Value;settings.AutoStartMonitoring=autoMonitor.Checked;settings.AutoStartWindows=autoWindows.Checked;settings.SmtpHost=smtpHost.Text.Trim();settings.SmtpPort=(int)smtpPort.Value;settings.SmtpUser=smtpUser.Text.Trim();if(smtpPass.Text.Length>0)settings.SmtpPasswordProtected=Monitor.Protect(smtpPass.Text);settings.MailTo=mailTo.Text.Trim();settings.EmailEnabled=email.Checked;settings.NotifyClose=notifyClose.Checked;settings.NotifyHang=notifyHang.Checked;settings.NotifyPotentialDisconnect=notifyPotential.Checked;settings.RemoteEnabled=remote?.Running??settings.RemoteEnabled;Store.SaveSettings(settings);SetStartup(settings.AutoStartWindows);}
    private void SetStartup(bool on){try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);if(on)key?.SetValue("DofusFarmAssistant",'"'+Application.ExecutablePath+'"');else key?.DeleteValue("DofusFarmAssistant",false);}catch{}}
    private void StartRemote(){try{if(settings.RemotePasswordHash.Length==0){MessageBox.Show("Définissez d’abord un mot de passe distant dans l’onglet Sécurité.");return;}remote?.Dispose();remote=new RemoteServer(monitor,settings,()=>monitor.TakeScreenshot());remote.Start();settings.RemoteEnabled=true;Store.SaveSettings(settings);remoteStatus.Text=$"Actif · 127.0.0.1:{remote.Port}";}catch(Exception e){MessageBox.Show("Impossible de démarrer le serveur : "+e.Message);}}
    private void RefreshDashboard(){if(IsDisposed)return;var s=monitor.GetSnapshot();state.Text=s.State;state.ForeColor=s.State=="Jeu ouvert"?Color.LightGreen:s.State=="Jeu fermé"?Color.LightCoral:Color.LightGoldenrodYellow;duration.Text=s.RuntimeSeconds is null?"—":TimeSpan.FromSeconds(s.RuntimeSeconds.Value).ToString(@"d\.hh\:mm\:ss");latestEvent.Text=s.LastEvent?.ToString("g")??"Aucun";eventCount.Text=s.EventCount.ToString();lastShot.Text=Directory.GetFiles(Store.Shots,"*.png").OrderByDescending(File.GetCreationTime).Select(Path.GetFileName).FirstOrDefault()??"Aucune";remoteStatus.Text=remote?.Running==true?$"Actif · 127.0.0.1:{remote.Port}":"Arrêté";}
    private void RefreshLists(){history.Items.Clear();foreach(var e in Store.LoadEvents().TakeLast(500).Reverse())history.Items.Add($"{e.Timestamp:g}  |  {e.Type}  |  {e.Details}");captures.Items.Clear();foreach(var f in Directory.GetFiles(Store.Shots,"*.png").OrderByDescending(File.GetCreationTime))captures.Items.Add(Path.GetFileName(f));RefreshDashboard();}
    private void OpenSelectedCapture(){if(captures.SelectedItem is string n)Process.Start(new ProcessStartInfo(Path.Combine(Store.Shots,n)){UseShellExecute=true});}
    private void SendTestMail(){try{SaveFields();using var client=new SmtpClient(settings.SmtpHost,settings.SmtpPort){EnableSsl=true,Credentials=new System.Net.NetworkCredential(settings.SmtpUser,Monitor.Unprotect(settings.SmtpPasswordProtected))};client.Send(new MailMessage(settings.SmtpUser,settings.MailTo,"Test Dofus Farm Assistant","Le test de notification a réussi."));Store.AddEvent("Notification test envoyée",settings.MailTo);MessageBox.Show("E-mail de test envoyé.");}catch(Exception ex){MessageBox.Show("Échec de l’envoi : "+ex.Message);}}
}
