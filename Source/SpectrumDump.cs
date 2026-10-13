using System;
using System.IO;
namespace HD2Walkman {
    internal static class SpectrumDump {
        static void Main(string[] args) {
            using(var input=new BinaryReader(File.OpenRead(args[0])))using(var output=new BinaryWriter(File.Create(args[1]))) {
                double[] window=new double[SpectrumMath.Size];
                while(input.BaseStream.Position+window.Length*4<=input.BaseStream.Length) {
                    for(int i=0;i<window.Length;i++)window[i]=input.ReadSingle();
                    foreach(double v in SpectrumMath.Analyze(window,24000))output.Write(v);
                }
            }
        }
    }
}
