// Compile with WALKMAN_HOST_TESTS, Companion.cs, ShuffleQueue.cs and SongSpectrum.cs;
// exclude NativeAudio.cs. This substitutes only the audio boundary and creates
// no windows, processes or native audio engines.
#if WALKMAN_HOST_TESTS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
namespace HD2Walkman {
    sealed class NativeAudio : IDisposable {
        public string Error { get { return null; } }
        public string File; public int Opens; public bool Playing;
        public double Position { get; private set; }
        public double Duration { get { return 247; } }
        public uint State { get { return Playing ? 1u : 2u; } }
        public void Open(string path, bool autoplay = true) { File = path; Opens++; Playing = autoplay; Position = 0; }
        public void Stop() { Playing = false; }
        public void Toggle() { Playing = !Playing; }
        public void Seek(double position) { Position = position; }
        public bool Poll() { return false; }
        public void SetVolume(double volume) { }
        public void Dispose() { }
    }
    static class HostLogicTests {
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Set(GameContext host, string name, object value) { typeof(GameContext).GetField(name,Flags).SetValue(host,value); }
        static T Get<T>(GameContext host, string name) { return (T)typeof(GameContext).GetField(name,Flags).GetValue(host); }
        static void Call(GameContext host, string name, params object[] args) { typeof(GameContext).GetMethod(name,Flags).Invoke(host,args); }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
        static string Text(System.IO.MemoryMappedFiles.MemoryMappedViewAccessor view, int offset, int length) { var bytes = new byte[length]; view.ReadArray(offset,bytes,0,length); return System.Text.Encoding.ASCII.GetString(bytes).Split('\0')[0]; }
        static void Main() {
            var host = (GameContext)FormatterServices.GetUninitializedObject(typeof(GameContext));
            GC.SuppressFinalize(host); // The fixture bypasses the ApplicationContext constructor.
            var audio = new NativeAudio();
            Set(host,"audio",audio); Set(host,"clock",Stopwatch.StartNew());
            Set(host,"order",new ShuffleQueue(new Random(42))); Set(host,"songs",new List<string>());
            var songs = new List<string> { Files.Anthem };
            for (int i = 1; i < 9; i++) songs.Add("/music/track" + i + ".mp3");
            Call(host,"ApplyLibrary",songs);
            Check(audio.File == Files.Anthem && !audio.Playing && audio.Opens == 1,"The real host selects and preloads the first anthem without requesting playback.");
            Call(host,"Execute",1u,0u);
            Check(audio.Playing && audio.File == Files.Anthem,"The first Play click starts the preselected anthem.");
            Call(host,"Execute",6u,1u);
            var visited = new HashSet<string> { audio.File };
            uint generation = Get<uint>(host,"libraryGeneration");
            for (int i = 0; i < 8; i++) {
                Call(host,"Next",1);
                Check(visited.Add(audio.File),"Host Next consumes a fresh shuffled song.");
                if (i == 2) {
                    string file = audio.File; int opens = audio.Opens; audio.Seek(42);
                    Call(host,"ApplyLibrary",new List<string>(songs));
                    Check(audio.File == file && audio.Opens == opens && audio.Playing && audio.Position == 42 && Get<uint>(host,"libraryGeneration") == generation,"An unchanged refresh preserves playback, position, cache generation and shuffle progress.");
                }
            }
            string selected = audio.File; int previousOpens = audio.Opens;
            var expanded = new List<string>(songs); expanded.Insert(1,"/music/new.mp3");
            Call(host,"ApplyLibrary",expanded);
            Check(audio.File == selected && audio.Opens == previousOpens && audio.Playing && Get<uint>(host,"libraryGeneration") != generation,"Changing row indices preserves the selected file and invalidates old library identities.");
            expanded.Remove(selected); Call(host,"ApplyLibrary",expanded);
            Check(audio.File == Files.Anthem && !audio.Playing,"Removing the selected song safely falls back to the first anthem paused.");
            string fileWithPunctuation="/music/Mix \u2014 Live/0001 - ASMR \uff5c Beyonc\u00e9 \u2014 \u201cLive\u201d [dQw4w9WgXcQ].mp3";
            Call(host,"ApplyLibrary",new List<string>{Files.Anthem,fileWithPunctuation});
            string bridgeFile=System.IO.Path.GetTempFileName();
            try {
                using(var file=new System.IO.FileStream(bridgeFile,System.IO.FileMode.Open,System.IO.FileAccess.ReadWrite))file.SetLength(4096);
                using(var map=System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(bridgeFile,System.IO.FileMode.Open,null,4096))using(var view=map.CreateViewAccessor()) {
                    Set(host,"view",view);view.Write(3000,Get<uint>(host,"libraryGeneration"));Call(host,"Execute",4u,1u);Call(host,"Publish");
                    Check(Text(view,288,256)=="ASMR | Beyonce - \"Live\""&&Text(view,928,256)=="ASMR | Beyonce - \"Live\""&&Text(view,544,128)=="Mix - Live"&&audio.File==fileWithPunctuation,"Player titles, library rows and folder labels have readable punctuation and no downloader prefixes; the original audio path remains unchanged.");
                    Check(view.ReadUInt32(48)==2&&view.ReadUInt32(52)==1&&view.ReadUInt32(3080)==0,"The restored host publishes one library and its matching playback index without playlist capabilities.");
                }
            } finally { System.IO.File.Delete(bridgeFile); }
            Console.WriteLine("Audio calls were substituted; Windows Media Engine and the live game were not exercised.");
        }
    }
}
#endif
