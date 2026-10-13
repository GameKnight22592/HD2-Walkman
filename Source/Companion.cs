using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyVersion("0.3.7.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.3.7.0")]

namespace HD2Walkman {
    static class Files {
        public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HD2Walkman");
        public static string Folder {
            get { return ResolveFolder(Root); }
        }
        internal static string ResolveFolder(string root) {
                try {
                    string config = Path.Combine(root,"music-folder.txt");
                    string value = File.Exists(config) ? File.ReadAllText(config).Trim().Trim('"') : "";
                    if (value.Length != 0) { value = Environment.ExpandEnvironmentVariables(value); if (Path.IsPathRooted(value) && Directory.Exists(value)) return Path.GetFullPath(value); }
                } catch (Exception ex) { if (!Recoverable(ex)) throw; Log("Music folder setting ignored: " + ex.Message); }
                return Path.Combine(root,"Music");
        }
        static bool Recoverable(Exception ex) { return ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException || ex is ArgumentException || ex is NotSupportedException; }
        public static bool Supported(string path) { return new [] { ".mp3", ".wav", ".flac", ".m4a" }.Contains(Path.GetExtension(path).ToLowerInvariant()); }
        public static void Log(string text) { try { Directory.CreateDirectory(Root); File.AppendAllText(Path.Combine(Root,"Walkman.log"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine); } catch { } }
        public static void Refresh() { File.WriteAllText(Path.Combine(Root,"refresh.txt"), DateTime.UtcNow.Ticks.ToString()); }
        public static readonly string Anthem = Path.Combine(Root,"BuiltIn","Super Earth National Anthem.mp3");
        public static List<string> Scan() { var songs = WithAnthem(ScanFolder(Folder),Anthem); if (songs.Count > 10000) songs.RemoveRange(10000,songs.Count-10000); return songs; }
        internal static List<string> WithAnthem(List<string> songs, string anthem) {
            if (File.Exists(anthem)) {
                songs.RemoveAll(p => String.Equals(Path.GetFullPath(p),Path.GetFullPath(anthem),StringComparison.OrdinalIgnoreCase));
                songs.Insert(0,Path.GetFullPath(anthem));
            }
            return songs;
        }
        internal static List<string> ScanFolder(string folder) {
            var result = new List<string>(); var stack = new Stack<string>(); if (!String.IsNullOrWhiteSpace(folder)) stack.Push(folder);
            while (stack.Count > 0) {
                string dir = stack.Pop();
                try {
                    foreach (string file in Directory.GetFiles(dir)) {
                        try { if (Supported(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) result.Add(Path.GetFullPath(file)); }
                        catch (Exception ex) { if (!Recoverable(ex)) throw; Log("Skipped song: " + file + ": " + ex.Message); }
                        if (result.Count >= 9999) { stack.Clear(); break; }
                    }
                    if (result.Count < 9999) foreach (string child in Directory.GetDirectories(dir)) {
                        try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) stack.Push(child); }
                        catch (Exception ex) { if (!Recoverable(ex)) throw; Log("Skipped folder: " + child + ": " + ex.Message); }
                    }
                } catch (Exception ex) { if (!Recoverable(ex)) throw; Log("Could not scan " + dir + ": " + ex.Message); }
            }
            result.Sort(StringComparer.OrdinalIgnoreCase); return result;
        }
        // Match both AudioDrop playlist markers, leaving ordinary song names untouched.
        static readonly Regex PlaylistTitle = new Regex(@"\A[0-9]{4,} - (?<title>.+) \[[A-Za-z0-9_-]{11}\](?<copy> \([0-9]+\))?\z", RegexOptions.CultureInvariant);
        public static string Display(string path) {
            string name = Path.GetFileNameWithoutExtension(path);
            Match match = PlaylistTitle.Match(name);
            return match.Success ? match.Groups["title"].Value + match.Groups["copy"].Value : name;
        }
        public static string Collection(string path) { return String.Equals(Path.GetFullPath(path),Path.GetFullPath(Anthem),StringComparison.OrdinalIgnoreCase) ? "Mandated Track" : Path.GetFileName(Path.GetDirectoryName(path)); }
        public static string ErrorText(Exception ex) { var aggregate = ex as AggregateException; if (aggregate != null) ex = aggregate.Flatten().InnerExceptions[0]; return ex.GetType().Name + ": " + ex.Message; }
        public static string Ascii(string s) {
            // The game atlas is ASCII. Fold filename-safe full-width punctuation and
            // typography to readable glyphs before the unsupported-character fallback.
            string value = (s ?? "").Normalize(NormalizationForm.FormKD);
            var b = new StringBuilder();
            for (int i = 0; i < value.Length; i++) {
                char c = value[i]; var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(value,i);
                if (category == System.Globalization.UnicodeCategory.NonSpacingMark || category == System.Globalization.UnicodeCategory.EnclosingMark || category == System.Globalization.UnicodeCategory.Format) continue;
                if (category == System.Globalization.UnicodeCategory.DashPunctuation || c == '\u2212' || c == '\u23af' || c == '\u2500' || c == '\u2501') b.Append('-');
                else if (category == System.Globalization.UnicodeCategory.SpaceSeparator || c == '\t' || c == '\r' || c == '\n') b.Append(' ');
                else switch (c) {
                    case '\u2018': case '\u2019': case '\u201a': case '\u201b': case '\u2032': b.Append('\''); break;
                    case '\u201c': case '\u201d': case '\u201e': case '\u201f': case '\u2033': case '\u00ab': case '\u00bb': b.Append('"'); break;
                    case '\u2502': case '\u2503': case '\u2758': b.Append('|'); break;
                    case '\u29f8': case '\u2044': case '\u2215': b.Append('/'); break;
                    case '\u29f9': b.Append('\\'); break;
                    case '\u2022': case '\u00b7': b.Append('*'); break;
                    default: b.Append(c >= 32 && c < 127 ? c : '?'); break;
                }
                if (Char.IsHighSurrogate(c) && i+1 < value.Length && Char.IsLowSurrogate(value[i+1])) i++;
            }
            return b.ToString();
        }
    }
    sealed class LibraryForm : Form {
        readonly ListBox songs = new ListBox(); readonly Label location = new Label(); readonly Label status = new Label();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        Task<List<string>> scan; bool rescanRequested; readonly List<string> paths = new List<string>(); NativeAudio preview; string previewPath;
        public LibraryForm() {
            Text = "Walkman - Music Library (v0.3.7)"; Size = new Size(940,590); MinimumSize = new Size(800,430);
            BackColor = Color.FromArgb(20,23,27); ForeColor = Color.WhiteSmoke; Font = new Font("Segoe UI",10);
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(10), WrapContents = true };
            AddButton(top,"Add songs...",Import); AddButton(top,"Choose music folder...",Choose); AddButton(top,"Open folder",delegate { Process.Start("explorer.exe", "\"" + Files.Folder + "\""); }); AddButton(top,"Refresh",delegate { StartScan(); Files.Refresh(); });
            AddButton(top,"Preview / pause",Preview); AddButton(top,"Stop preview",StopPreview);
            AddButton(top,"Open log",delegate { string path = Path.Combine(Files.Root,"Walkman.log"); if (!File.Exists(path)) Files.Log("Walkman library opened."); Process.Start("notepad.exe","\"" + path + "\""); });
            location.Dock = DockStyle.Top; location.Height = 40; location.Padding = new Padding(12,2,12,2); location.AutoEllipsis = true;
            songs.Dock = DockStyle.Fill; songs.BackColor = Color.FromArgb(30,34,40); songs.ForeColor = Color.WhiteSmoke; songs.BorderStyle = BorderStyle.None;
            status.Dock = DockStyle.Bottom; status.Height = 66; status.Padding = new Padding(12); status.Text = "Super Earth National Anthem is included. Add songs or choose your music folder.\nSongs play through the in-game Walkman; this window manages files.";
            Controls.Add(songs); Controls.Add(location); Controls.Add(top); Controls.Add(status);
            AllowDrop = true; DragEnter += delegate(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += delegate(object sender, DragEventArgs e) { CopySongs((string[])e.Data.GetData(DataFormats.FileDrop)); };
            timer.Interval = 250; timer.Tick += delegate { CompleteScan(); PollPreview(); }; timer.Start();
            StartScan();
        }
        void CompleteScan() {
            if (scan == null || !scan.IsCompleted) return;
            var job = scan; scan = null;
            if (job.IsFaulted) { location.Text = Files.Folder; status.Text = "Scan failed: " + Files.ErrorText(job.Exception) + "\nOpen log for details."; Files.Log(job.Exception.ToString()); }
            else {
                string selected = songs.SelectedIndex >= 0 && songs.SelectedIndex < paths.Count ? paths[songs.SelectedIndex] : null;
                paths.Clear(); paths.AddRange(job.Result); songs.Items.Clear(); foreach (string file in paths) songs.Items.Add(Files.Display(file));
                if (paths.Count > 0) songs.SelectedIndex = Math.Max(0,paths.IndexOf(selected));
                location.Text = Files.Folder + " - " + paths.Count + " songs (includes anthem)";
                status.Text = "Select a song and use Preview / pause to test it here.\nUse the in-game library controls to select music during a mission.";
            }
            if (rescanRequested) { rescanRequested = false; StartScan(); }
        }
        void Preview() {
            int i = songs.SelectedIndex; if (i < 0 || i >= paths.Count) { status.Text = "Select a song first."; return; }
            if (preview == null) preview = new NativeAudio();
            if (previewPath == paths[i] && preview.Error == null) preview.Toggle(); else { preview.Open(paths[i]); previewPath = paths[i]; }
            status.Text = "Preview: " + Files.Display(previewPath);
        }
        void StopPreview() { if (preview != null) { preview.Dispose(); preview = null; } previewPath = null; status.Text = "Preview stopped."; }
        void PollPreview() {
            if (preview == null) return;
            try { if (preview.Poll()) StopPreview(); else if (preview.Error != null) { string error = preview.Error; StopPreview(); status.Text = error + " Open log for details."; Files.Log(error); } }
            catch (Exception ex) { Files.Log(ex.ToString()); StopPreview(); status.Text = "Preview failed: " + Files.ErrorText(ex); }
        }
        void AddButton(Control parent, string text, Action action) { var b = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat }; b.Click += delegate { try { action(); } catch (Exception ex) { Files.Log(ex.ToString()); MessageBox.Show(this,ex.Message,"Walkman",MessageBoxButtons.OK,MessageBoxIcon.Error); } }; parent.Controls.Add(b); }
        void StartScan() { if (scan == null) { location.Text = "Scanning music folder..."; scan = Task.Run((Func<List<string>>)Files.Scan); } else rescanRequested = true; }
        void Choose() { using (var d = new FolderBrowserDialog { Description = "Choose an existing music folder. Walkman reads its songs without copying or deleting them.", SelectedPath = Files.Folder }) { if (d.ShowDialog(this) == DialogResult.OK) { File.WriteAllText(Path.Combine(Files.Root,"music-folder.txt"),d.SelectedPath); StartScan(); Files.Refresh(); } } }
        void Import() { using (var d = new OpenFileDialog { Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a", Multiselect = true }) { if (d.ShowDialog(this) == DialogResult.OK) CopySongs(d.FileNames); } }
        void CopySongs(string[] paths) {
            string folder = Files.Folder; Directory.CreateDirectory(folder); int added = 0;
            foreach (string path in paths) {
                if (!File.Exists(path) || !Files.Supported(path)) continue;
                string target = Path.Combine(folder,Path.GetFileName(path));
                if (String.Equals(Path.GetFullPath(path),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase)) continue;
                int suffix = 1; while (File.Exists(target)) target = Path.Combine(folder,Path.GetFileNameWithoutExtension(path) + " (" + suffix++ + ")" + Path.GetExtension(path));
                File.Copy(path,target,false); added++;
            }
            status.Text = added > 0 ? "Added " + added + " songs. Originals were kept." : "No new songs copied. Files already in this folder will appear after scanning. Use actual MP3/WAV/FLAC/M4A files."; StartScan(); Files.Refresh();
        }
        protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); if (preview != null) { preview.Dispose(); preview = null; } } base.Dispose(disposing); }
    }
    sealed class GameContext : ApplicationContext {
        const uint Magic = 0x574B4D31;
        readonly Process game; readonly MemoryMappedFile map; readonly MemoryMappedViewAccessor view;
        readonly NativeAudio audio; readonly SongSpectrum spectrum; readonly NotifyIcon tray; readonly System.Windows.Forms.Timer timer;
        readonly Mutex mutex; readonly Stopwatch clock = Stopwatch.StartNew(); readonly ShuffleQueue order = new ShuffleQueue(new Random());
        List<string> songs = new List<string>(); Task<List<string>> scan; string playingFile, error;
        uint lastCommand, revision, listFirst, libraryGeneration; int volume = 50, repeat; bool shuffle, disposed, failed, userStopped, rescanRequested;
        DateTime refreshStamp; long nextRefresh, loadStarted; LibraryForm library;
        public GameContext(int pid, Mutex instanceMutex) {
            mutex = instanceMutex;
            try {
                game = Process.GetProcessById(pid); if (!String.Equals(game.ProcessName,"helldivers2",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The supplied process is not Helldivers 2.");
                map = MemoryMappedFile.OpenExisting("Local\\HD2Walkman_" + pid, MemoryMappedFileRights.ReadWrite); view = map.CreateViewAccessor(0,4096);
                if (view.ReadUInt32(0) != Magic || view.ReadUInt32(4) != 1 || view.ReadUInt32(8) != (uint)pid) throw new InvalidOperationException("Walkman game bridge version mismatch.");
                audio = new NativeAudio(); spectrum = new SongSpectrum(Files.Log);
                var menu = new ContextMenuStrip(); menu.Items.Add("Music library",null,delegate { ShowLibrary(); }); menu.Items.Add("Play / pause",null,delegate { Safe(delegate { Execute(1,0); }); }); menu.Items.Add("Exit Walkman companion",null,delegate { ExitThread(); });
                tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "Walkman - connected to Helldivers 2", ContextMenuStrip = menu, Visible = true }; tray.DoubleClick += delegate { ShowLibrary(); };
                timer = new System.Windows.Forms.Timer { Interval = 200 }; timer.Tick += delegate { Tick(); }; timer.Start();
                StartScan(); Files.Log("Connected to game PID " + pid + "; Media Engine audio only.");
            } catch { Dispose(); throw; }
        }
        void ShowLibrary() { if (library == null || library.IsDisposed) library = new LibraryForm(); library.Show(); library.Activate(); }
        void StartScan() { if (scan == null) scan = Task.Run((Func<List<string>>)Files.Scan); else rescanRequested = true; }
        static uint Now { get { return unchecked((uint)Environment.TickCount); } }
        void Tick() {
            try {
                if (game.HasExited || view.ReadUInt32(16) != 0 || unchecked(Now-view.ReadUInt32(12)) > 15000) { ExitThread(); return; }
                uint command = view.ReadUInt32(20);
                if (command != lastCommand) {
                    Thread.MemoryBarrier(); uint op = view.ReadUInt32(24), arg = view.ReadUInt32(28);
                    if (command == view.ReadUInt32(20)) { Safe(delegate { Execute(op,arg); }); lastCommand = command; Thread.MemoryBarrier(); view.Write(32,lastCommand); }
                }
                if (scan != null && scan.IsCompleted) { var job = scan; scan = null; if (job.IsFaulted) { error = "Music scan failed; check Walkman.log"; Files.Log(job.Exception.ToString()); } else ApplyLibrary(job.Result); revision++; if (rescanRequested) { rescanRequested = false; StartScan(); } }
                if (clock.ElapsedMilliseconds >= nextRefresh) { nextRefresh = clock.ElapsedMilliseconds + 2000; string path = Path.Combine(Files.Root,"refresh.txt"); DateTime stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; if (stamp != refreshStamp) { refreshStamp = stamp; StartScan(); } }
                bool ended = audio.Poll();
                if (audio.Error != null) { if (!failed) { failed = true; error = audio.Error; Files.Log(error); } }
                else if (ended && !userStopped) {
                    int current = songs.IndexOf(playingFile);
                    if (repeat == 2) Play(current >= 0 ? current : 0);
                    else if (shuffle || repeat == 1 || current >= 0 && current + 1 < songs.Count) Next(1);
                    else { audio.Stop(); userStopped = true; }
                }
                if (playingFile != null && (audio.State == 3 || audio.State == 0 && !userStopped) && clock.ElapsedMilliseconds-loadStarted > 20000 && !failed) { audio.Stop(); failed = true; error = "Audio startup timed out. Try another song."; }
                bool analyzing = view.ReadUInt32(3004) != 0 && audio.State == 1 && volume > 0 && !userStopped && !failed;
                spectrum.Request(playingFile,audio.Position,analyzing);
                uint spectrumState; uint[] bands = spectrum.Read(audio.Position,volume/100.0,out spectrumState);
                uint spectrumSeq = view.ReadUInt32(3024); if ((spectrumSeq & 1) != 0) spectrumSeq++;
                view.Write(3024,spectrumSeq+1); Thread.MemoryBarrier(); view.Write(3028,spectrumState);
                for(int band=0;band<bands.Length;band++)view.Write(3032+band*4,bands[band]);
                Thread.MemoryBarrier(); view.Write(3024,spectrumSeq+2);
                timer.Interval = analyzing ? 33 : 200;
                Publish();
            } catch (Exception ex) { Files.Log(ex.ToString()); ExitThread(); }
        }
        void ApplyLibrary(List<string> loaded) {
            bool changed = !songs.SequenceEqual(loaded,StringComparer.OrdinalIgnoreCase);
            songs = loaded;
            if (changed) libraryGeneration++;
            if (songs.IndexOf(playingFile) < 0) {
                audio.Stop(); playingFile = null;
                if (songs.Count > 0) Safe(delegate { Play(0,false); });
            }
            if (changed) order.Reset(songs.Count,songs.IndexOf(playingFile));
        }
        void Safe(Action action) { try { action(); } catch (Exception ex) { error = "Playback failed; see Walkman.log"; failed = true; Files.Log(ex.ToString()); } }
        void Play(int index, bool autoplay = true) { if (index < 0 || index >= songs.Count) { error = songs.Count == 0 ? "Add songs with Walkman Library.exe" : "Refresh library and select a song"; return; } failed = false; error = null; userStopped = false; playingFile = songs[index]; loadStarted = clock.ElapsedMilliseconds; audio.Open(playingFile,autoplay); if (shuffle) order.Select(index); revision++; }
        void Next(int delta) {
            if (songs.Count == 0) { Play(0); return; }
            int current = songs.IndexOf(playingFile);
            int index = shuffle ? (delta > 0 ? order.Next(current) : order.Previous(current)) : current < 0 ? 0 : (current + delta + songs.Count) % songs.Count;
            Play(index);
        }
        void Execute(uint op, uint arg) {
            switch (op) {
                case 1: if (playingFile == null || failed || userStopped) Play(Math.Max(0,songs.IndexOf(playingFile))); else audio.Toggle(); break;
                case 2: Next(1); break; case 3: if (audio.Position > 3) audio.Seek(0); else Next(-1); break;
                case 4: if (view.ReadUInt32(3000) != libraryGeneration) error = "Library changed. Select the song again."; else Play(arg <= Int32.MaxValue ? (int)arg : -1); break;
                case 5: volume = (int)Math.Min(100,arg); audio.SetVolume(volume/100.0); break;
                case 6: if (shuffle != (arg != 0)) { shuffle = arg != 0; order.Reset(songs.Count,songs.IndexOf(playingFile)); } break; case 7: repeat = (int)Math.Min(2,arg); break;
                case 8: audio.Seek(arg/1000.0); break; case 9: StartScan(); break; case 10: audio.Stop(); userStopped = true; break;
            } revision++;
        }
        void PutText(int offset, int length, string text) { byte[] bytes = Encoding.ASCII.GetBytes(Files.Ascii(text)); byte[] fixedBuffer = new byte[length]; Array.Copy(bytes,fixedBuffer,Math.Min(length-1,bytes.Length)); view.WriteArray(offset,fixedBuffer,0,length); }
        static uint Milliseconds(double seconds) { return (uint)Math.Min(UInt32.MaxValue,Math.Max(0,seconds*1000)); }
        void Publish() {
            uint seq = view.ReadUInt32(44); if ((seq & 1) != 0) seq++;
            view.Write(44,seq+1); Thread.MemoryBarrier();
            uint first = Math.Min((uint)songs.Count,view.ReadUInt32(84)); if (first != listFirst) { listFirst = first; revision++; }
            uint state = failed ? 4u : userStopped || playingFile == null ? 0u : audio.State == 0 ? 2u : audio.State;
            view.Write(36,Now); view.Write(40,state); view.Write(48,(uint)songs.Count);
            int playing = songs.IndexOf(playingFile); view.Write(52,playing < 0 ? UInt32.MaxValue : (uint)playing);
            view.Write(56,Milliseconds(audio.Position)); view.Write(60,Milliseconds(audio.Duration)); view.Write(64,(uint)volume); view.Write(68,shuffle ? 1u : 0u); view.Write(72,(uint)repeat);
            view.Write(76,first); view.Write(80,(uint)Math.Min(8,songs.Count-(int)first)); view.Write(88,revision); view.Write(92,libraryGeneration);
            PutText(96,192,error ?? (songs.Count == 0 ? "Add songs with Walkman Library.exe" : scan != null ? "Refreshing library..." : ""));
            PutText(288,256,playingFile == null ? "Super Earth National Anthem" : Files.Display(playingFile));
            PutText(544,128,playingFile == null ? "Mandated Track" : Files.Collection(playingFile));
            for (int row = 0; row < 8; row++) PutText(672+row*256,256,first+row < songs.Count ? Files.Display(songs[(int)first+row]) : "");
            Thread.MemoryBarrier(); view.Write(44,seq+2);
        }
        protected override void ExitThreadCore() { Dispose(); base.ExitThreadCore(); }
        protected override void Dispose(bool disposing) {
            if (!disposed) { disposed = true; if (timer != null) timer.Dispose(); if (tray != null) { tray.Visible = false; tray.Dispose(); } if (library != null) library.Dispose(); if (spectrum != null) spectrum.Dispose(); if (audio != null) audio.Dispose(); if (view != null) view.Dispose(); if (map != null) map.Dispose(); if (game != null) game.Dispose(); if (mutex != null) mutex.Dispose(); }
            base.Dispose(disposing);
        }
    }
    static class Program {
        [STAThread] static void Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
                Directory.CreateDirectory(Files.Root); Directory.CreateDirectory(Path.Combine(Files.Root,"Music"));
                Files.Log("Walkman v0.3.7 starting; CLR " + Environment.Version + "; folder " + Files.Folder);
                if (args.Length == 2 && args[0] == "--game") {
                    int pid; if (!Int32.TryParse(args[1],out pid) || pid <= 0) throw new ArgumentException("Invalid game PID.");
                    bool created; var mutex = new Mutex(true,"Local\\HD2WalkmanHost_" + pid,out created); if (!created) { mutex.Dispose(); return; }
                    using (var context = new GameContext(pid,mutex)) Application.Run(context);
                } else if (args.Length == 1 && args[0] == "--doctor") {
                    using (var audio = new NativeAudio()) MessageBox.Show("Windows Media Engine initialized successfully.\nThis checks the audio engine, not individual song codecs or game installation.","Walkman diagnostics");
                } else { using (var library = new LibraryForm()) Application.Run(library); }
            } catch (Exception ex) { Files.Log(ex.ToString()); MessageBox.Show("Walkman could not start.\n" + ex.Message + "\n\nDetails: " + Path.Combine(Files.Root,"Walkman.log"),"Walkman",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        }
    }
}
