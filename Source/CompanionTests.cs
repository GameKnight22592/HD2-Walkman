using System;
using System.IO;
using System.Linq;
namespace HD2Walkman {
    static class CompanionTests {
        static void Check(bool value,string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
        static void Main(string[] args) {
            Check(Files.Supported("SONG.MP3") && Files.Supported("mix.wav") && Files.Supported("album.FLAC") && Files.Supported("song.m4a") && !Files.Supported("song.mp3.exe"),"Case-insensitive audio extension selection rejects executable lookalikes.");
            Check(Files.Ascii("Beyonc\u00e9 / \u97f3\u4e50") == "Beyonce / ??", "Display normalization preserves Latin names, with a defined fallback for missing glyphs.");
            Check(Files.Ascii("ASMR \uff5c Title \u2014 mix \u2013 live \u2212 edit") == "ASMR | Title - mix - live - edit", "Full-width filename separators, long dashes and minus signs render as readable ASCII glyphs.");
            Check(Files.Ascii("\u201cDon\u2019t stop\u201d\u2026\u00a0\uff1a \uff1f \u29f8 \uff3c") == "\"Don't stop\"... : ? / \\", "Curly quotes, ellipses, spaces and Windows-safe filename punctuation remain readable.");
            Check(Files.Ascii("Song\u200b\ufe0f \ud83c\udfb5 \u2500\u2502") == "Song ? -|", "Invisible formatting is removed and unsupported supplementary characters use one fallback glyph.");
            Check(Files.Ascii(Files.Display("0003 - ASMR \uff5c Beyonc\u00e9 \u2014 \u201cLive\u201d [dQw4w9WgXcQ].mp3")) == "ASMR | Beyonce - \"Live\"", "Playlist title cleanup and punctuation folding work together without changing the file path.");
            Check(Files.Display("0001 - I Really Want to Stay at Your House [dQw4w9WgXcQ].mp3") == "I Really Want to Stay at Your House", "AudioDrop playlist numbers and video IDs are hidden in displayed titles.");
            Check(Files.Display("10000 - Beyonc\u00e9 - Song [aB0_-12cDEF].mp3") == "Beyonc\u00e9 - Song", "Large playlist indices and Unicode titles are handled without changing the title.");
            Check(Files.Display("0002 - Song [Live] [dQw4w9WgXcQ] (2).mp3") == "Song [Live] (2)", "Duplicate-copy suffixes and genuine bracketed title text remain visible.");
            Check(Files.Display("1984 - Artist.mp3") == "1984 - Artist" && Files.Display("0001 - Song.mp3") == "0001 - Song" && Files.Display("Song [Live].mp3") == "Song [Live]" && Files.Display("Song [dQw4w9WgXcQ].mp3") == "Song [dQw4w9WgXcQ]", "Ordinary numeric titles and bracketed names are preserved unless both playlist markers match.");
            Check(Files.Display("0001 - Song [short].mp3") == "0001 - Song [short]" && Files.Display("0001 - Song [dQw4w9WgXcQ] extra.mp3") == "0001 - Song [dQw4w9WgXcQ] extra", "Incomplete or unrelated filename markers are left untouched.");
            Check(Files.Anthem.EndsWith("Super Earth National Anthem.mp3"),"The supplied MP3 is the only built-in anthem path.");
            Check(Files.Collection(Files.Anthem) == "Mandated Track" && Files.Collection(Path.Combine(args[0],"Super Earth National Anthem.mp3")) == Path.GetFileName(Path.GetFullPath(args[0])),"Only the bundled anthem uses Mandated Track; imported songs retain their folder label.");
            double[] silence = new double[SpectrumMath.Size];
            Check(SpectrumMath.Analyze(silence,24000).All(x => x == 0),"A silent song produces no invented spectrum movement.");
            int lowPeak=-1,highPeak=-1;
            foreach(double frequency in new[] { 120.0, 4000.0 }) {
                double[] tone = new double[SpectrumMath.Size];
                for(int i=0;i<tone.Length;i++)tone[i]=.7*Math.Sin(2*Math.PI*frequency*i/24000);
                var bands=SpectrumMath.Analyze(tone,24000);int peak=Array.IndexOf(bands,bands.Max());
                Check(bands.All(x => x>=0 && x<=1) && bands.Max()>.8,"Spectrum bounds and tone response at " + frequency + " Hz.");
                if(frequency<1000)lowPeak=peak;else highPeak=peak;
            }
            Check(highPeak>lowPeak,"Higher-pitched tones move the higher frequency bands.");
            double[] loud = new double[SpectrumMath.Size],quiet = new double[SpectrumMath.Size];
            for(int i=0;i<loud.Length;i++) {loud[i]=.8*Math.Sin(2*Math.PI*1000*i/24000);quiet[i]=loud[i]*.05;}
            Check(SpectrumMath.Analyze(loud,24000).Max()>SpectrumMath.Analyze(quiet,24000).Max()+.3,"Quieter passages produce smaller band levels.");
            var list = Files.ScanFolder(args[0]);
            Check(list.Count == 3 && list.All(Path.IsPathRooted) && list.All(Files.Supported),"Library scan includes supported files in nested Unicode paths and excludes unrelated files.");
            Check(list.SequenceEqual(list.OrderBy(x => x,StringComparer.OrdinalIgnoreCase)),"Library ordering is deterministic with an ordinal comparison.");
            string anthem = Path.Combine(args[0],"anthem.wav"); File.WriteAllBytes(anthem,new byte[0]);
            Check(Files.WithAnthem(new System.Collections.Generic.List<string>(),anthem).Single() == Path.GetFullPath(anthem),"Built-in anthem is available when the selected music folder is empty.");
            var merged = Files.WithAnthem(new System.Collections.Generic.List<string> {list[0],anthem,anthem},anthem);
            Check(merged.Count == 2 && merged[0] == Path.GetFullPath(anthem),"Built-in anthem stays first and is not indexed twice.");
            File.Delete(anthem);
            string config = Path.Combine(args[0],"music-folder.txt");
            Check(Files.ResolveFolder(args[0]) == Path.Combine(args[0],"Music"),"A fresh install uses the default music directory without a folder setting.");
            File.WriteAllText(config,"\"" + Path.GetFullPath(args[0]) + "\"");
            Check(Files.ResolveFolder(args[0]) == Path.GetFullPath(args[0]),"Quoted existing folder settings resolve correctly.");
            File.WriteAllText(config,"/bad\0path");
            Check(Files.ResolveFolder(args[0]) == Path.Combine(args[0],"Music"),"Malformed folder settings fall back instead of faulting the scan.");
            File.Delete(config);
            Check(Files.ScanFolder(Path.Combine(args[0],"missing")).Count == 0,"An unavailable library directory does not prevent built-in songs from loading.");
            for (int count = 1; count <= 25; count++) {
                var queue = new ShuffleQueue(new Random(count)); queue.Reset(count,0);
                int current = 0;
                var cycle = new System.Collections.Generic.HashSet<int>(); cycle.Add(current);
                for (int i = 1; i < count; i++) { int next = queue.Next(current); if (!cycle.Add(next)) throw new Exception("Early repeat in initial cycle."); current = next; }
                for (int round = 0; round < 5; round++) {
                    cycle.Clear(); int previous = current;
                    for (int i = 0; i < count; i++) { current = queue.Next(current); if (!cycle.Add(current)) throw new Exception("Early repeat in a full cycle."); if (i == 0 && count > 1 && current == previous) throw new Exception("Immediate repeat at cycle boundary."); }
                }
            }
            Check(true,"Shuffle cycles visit every track exactly once and avoid immediate boundary repeats across 1-25-track libraries and five cycles each.");
            var history = new ShuffleQueue(new Random(42)); history.Reset(9,0);
            int first = history.Next(0), second = history.Next(first);
            Check(history.Previous(second) == first && history.Next(first) == second,"Previous follows shuffle playback history; Next returns to the same forward track.");
            var chosen = new ShuffleQueue(new Random(42)); chosen.Reset(9,0); chosen.Select(4);
            var seen = new System.Collections.Generic.HashSet<int> { 0,4 };
            for (int i = 0; i < 7; i++) Check(seen.Add(chosen.Next(4)),"Manual selection is consumed from the remaining shuffle cycle.");
            chosen.Reset(0,-1); Check(chosen.Next(-1) == -1,"An empty shuffle queue is safe.");
            Console.WriteLine("No Windows audio or UI functions were invoked by these tests.");
        }
    }
}
