using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace HD2Walkman {
    // A bounded, read-only PCM analysis path; it never captures desktop/game audio.
    internal sealed class SpectrumFrame {
        public readonly double Time; public readonly double[] Bands;
        public SpectrumFrame(double time,double[] bands) { Time=time; Bands=bands; }
    }
    internal static class SpectrumMath {
        public const int Size=1024, BandCount=12;
        public static double[] Analyze(double[] samples,int rate) {
            if (samples.Length != Size || rate < 8000) throw new ArgumentException("Invalid spectrum input");
            double[] real=new double[Size],imag=new double[Size];
            for (int i=0;i<Size;i++) real[i]=(Double.IsNaN(samples[i]) || Double.IsInfinity(samples[i]) ? 0 : samples[i])*(.5-.5*Math.Cos(2*Math.PI*i/(Size-1)));
            for (int i=1,j=0;i<Size;i++) {
                int bit=Size>>1; for (;(j&bit)!=0;bit>>=1) j^=bit; j^=bit;
                if (i<j) { double v=real[i];real[i]=real[j];real[j]=v; }
            }
            for (int length=2;length<=Size;length<<=1) {
                double angle=-2*Math.PI/length,wr0=Math.Cos(angle),wi0=Math.Sin(angle);
                for (int start=0;start<Size;start+=length) {
                    double wr=1,wi=0;
                    for (int j=0;j<length/2;j++) {
                        int a=start+j,b=a+length/2;
                        double tr=real[b]*wr-imag[b]*wi,ti=real[b]*wi+imag[b]*wr;
                        real[b]=real[a]-tr;imag[b]=imag[a]-ti;real[a]+=tr;imag[a]+=ti;
                        double next=wr*wr0-wi*wi0;wi=wr*wi0+wi*wr0;wr=next;
                    }
                }
            }
            double[] bands=new double[BandCount]; double upper=Math.Min(16000,rate*.49);
            for (int band=0;band<BandCount;band++) {
                double low=40*Math.Pow(upper/40.0,band/(double)BandCount),high=40*Math.Pow(upper/40.0,(band+1)/(double)BandCount);
                int begin=Math.Max(1,(int)Math.Ceiling(low*Size/rate)),end=Math.Min(Size/2,(int)Math.Ceiling(high*Size/rate));
                double peak=0;
                for (int i=begin;i<end;i++) peak=Math.Max(peak,Math.Sqrt(real[i]*real[i]+imag[i]*imag[i])*4/Size);
                bands[band]=Math.Max(0,Math.Min(1,(20*Math.Log10(Math.Max(1e-9,peak))+55)/55));
            }
            return bands;
        }
    }
    internal sealed class SongSpectrum : IDisposable {
        readonly object gate=new object(); readonly Thread worker; readonly AutoResetEvent wake=new AutoResetEvent(false);
        readonly List<SpectrumFrame> frames=new List<SpectrumFrame>(); readonly Action<string> log;
        string requestedFile; double requestedPosition; bool enabled,disposed; int ready,requestGeneration;
        public SongSpectrum(Action<string> logger) { log=logger;worker=new Thread(Work) { IsBackground=true,Name="Walkman song spectrum",Priority=ThreadPriority.BelowNormal };worker.Start(); }
        public void Request(string file,double position,bool active) {
            lock(gate) { if(disposed)return;bool changed=enabled!=active || requestedFile!=file;requestedFile=file;requestedPosition=position;enabled=active && file!=null;if(changed) { frames.Clear();ready=0;requestGeneration++;wake.Set(); } }
        }
        public uint[] Read(double position,double volume,out uint state) {
            lock(gate) {
                uint[] result=new uint[SpectrumMath.BandCount];state=(uint)ready;
                if(!enabled || frames.Count==0) { state=ready==2 ? 2u : 0u;return result; }
                SpectrumFrame left=frames[0],right=left;
                foreach(var f in frames) { if(f.Time<=position)left=f;if(f.Time>=position) {right=f;break;}right=f; }
                if(Math.Abs(left.Time-position)>.25 && Math.Abs(right.Time-position)>.25) { state=0;return result; }
                double blend=right.Time>left.Time ? Math.Max(0,Math.Min(1,(position-left.Time)/(right.Time-left.Time))) : 0;
                for(int i=0;i<result.Length;i++) result[i]=(uint)Math.Round((left.Bands[i]*(1-blend)+right.Bands[i]*blend)*Math.Max(0,Math.Min(1,volume))*1000);
                state=1;return result;
            }
        }
        void Work() {
            NativePcmReader reader=null;string loaded=null,failed=null;double decoded=-1,lastRequest=-1;bool end=false;int failedGeneration=-1;
            try {
                while(true) {
                    string file;double position;bool active;int generation;
                    lock(gate) { if(disposed)break;file=requestedFile;position=requestedPosition;active=enabled;generation=requestGeneration; }
                    if(!active || file==failed && generation==failedGeneration) {
                        if(reader!=null) {reader.Dispose();reader=null;loaded=null;}
                        wake.WaitOne(100);continue;
                    }
                    try {
                        if(reader==null || loaded!=file) {
                            if(reader!=null)reader.Dispose();reader=new NativePcmReader(file);loaded=file;decoded=-1;lastRequest=-1;end=false;
                        }
                        if(decoded<0 || position<lastRequest-.15 || position>decoded+.5) {
                            reader.Seek(Math.Max(0,position-.08));decoded=position-.08;end=false;
                            lock(gate) {frames.Clear();ready=0;}
                        }
                        lastRequest=position;
                        if(end || decoded>position+.8) {wake.WaitOne(50);continue;}
                        List<SpectrumFrame> batch=reader.Read();
                        if(batch==null) {end=true;wake.WaitOne(50);continue;}
                        if(batch.Count>0)decoded=batch[batch.Count-1].Time;else wake.WaitOne(20);
                        lock(gate) {
                            if(enabled && requestedFile==loaded) {
                                frames.AddRange(batch);while(frames.Count>256 || frames.Count>1 && frames[1].Time<requestedPosition-.25)frames.RemoveAt(0);
                                if(frames.Count>0)ready=1;
                            }
                        }
                    } catch(Exception ex) {
                        failed=file;failedGeneration=generation;lock(gate) {if(requestGeneration==generation && requestedFile==file) {frames.Clear();ready=2;} }
                        if(reader!=null) {reader.Dispose();reader=null;loaded=null;}
                        if(log!=null)log("Song visualizer unavailable for "+file+": "+ex.Message);
                    }
                }
            } finally {if(reader!=null)reader.Dispose();wake.Dispose();}
        }
        public void Dispose() {lock(gate) {if(disposed)return;disposed=true;wake.Set();}worker.Join(300);}
    }
    // IMFSourceReader / IMFSample / IMFMediaBuffer slots from the Windows SDK.
    internal sealed class NativePcmReader : IDisposable {
        const uint Audio=0xfffffffd, All=0xfffffffe;
        [DllImport("mfplat.dll",ExactSpelling=true)] static extern int MFStartup(uint version,uint flags);
        [DllImport("mfplat.dll",ExactSpelling=true)] static extern int MFShutdown();
        [DllImport("mfplat.dll",ExactSpelling=true)] static extern int MFCreateMediaType(out IntPtr type);
        [DllImport("mfreadwrite.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int MFCreateSourceReaderFromURL(string url,IntPtr attributes,out IntPtr reader);
        [DllImport("ole32.dll",ExactSpelling=true)] static extern int CoInitializeEx(IntPtr reserved,uint mode);
        [DllImport("ole32.dll",ExactSpelling=true)] static extern void CoUninitialize();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Select(IntPtr self,uint stream,int selected);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetGuid(IntPtr self,ref Guid key,ref Guid value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetNumber(IntPtr self,ref Guid key,out uint value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetType(IntPtr self,uint stream,IntPtr reserved,IntPtr type);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetMediaType(IntPtr self,uint stream,out IntPtr type);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ReadSample(IntPtr self,uint stream,uint control,out uint actual,out uint flags,out long timestamp,out IntPtr sample);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Contiguous(IntPtr self,out IntPtr buffer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int LockBuffer(IntPtr self,out IntPtr data,out uint maximum,out uint current);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int UnlockBuffer(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Position(IntPtr self,ref Guid format,ref Variant position);
        [StructLayout(LayoutKind.Explicit,Size=24)] struct Variant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public long Value; }
        static readonly Guid Major=new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"),Subtype=new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"),AudioType=new Guid("73647561-0000-0010-8000-00aa00389b71"),PCM=new Guid("00000001-0000-0010-8000-00aa00389b71"),Float=new Guid("00000003-0000-0010-8000-00aa00389b71");
        static readonly Guid RateKey=new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba"),ChannelsKey=new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a"),BitsKey=new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
        IntPtr reader;bool com,started,isFloat;int rate,channels,bits;double[] window=new double[SpectrumMath.Size],rightWindow=new double[SpectrumMath.Size];int filled,decimation,averaged;double leftSum,rightSum,windowTime;
        static T Slot<T>(IntPtr self,int index)where T:class {return(T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self),index*IntPtr.Size),typeof(T));}
        static void Check(int result) {if(result<0)Marshal.ThrowExceptionForHR(result);}
        public NativePcmReader(string file) {
            IntPtr type=IntPtr.Zero,actual=IntPtr.Zero;
            try {
                Check(CoInitializeEx(IntPtr.Zero,0));com=true;Check(MFStartup(0x20070,0));started=true;
                Check(MFCreateSourceReaderFromURL(System.IO.Path.GetFullPath(file),IntPtr.Zero,out reader));
                Check(Slot<Select>(reader,4)(reader,All,0));Check(Slot<Select>(reader,4)(reader,Audio,1));
                Check(MFCreateMediaType(out type));Guid key=Major,value=AudioType;Check(Slot<SetGuid>(type,24)(type,ref key,ref value));key=Subtype;value=Float;Check(Slot<SetGuid>(type,24)(type,ref key,ref value));
                isFloat=Slot<SetType>(reader,7)(reader,Audio,IntPtr.Zero,type)>=0;
                if(!isFloat) {value=PCM;Check(Slot<SetGuid>(type,24)(type,ref key,ref value));Check(Slot<SetType>(reader,7)(reader,Audio,IntPtr.Zero,type));}
                Check(Slot<GetMediaType>(reader,6)(reader,Audio,out actual));uint n;
                key=RateKey;Check(Slot<GetNumber>(actual,7)(actual,ref key,out n));rate=(int)n;
                key=ChannelsKey;Check(Slot<GetNumber>(actual,7)(actual,ref key,out n));channels=(int)n;
                key=BitsKey;Check(Slot<GetNumber>(actual,7)(actual,ref key,out n));bits=(int)n;
                decimation=Math.Max(1,(int)Math.Ceiling(rate/24000.0));
                if(rate<8000 || rate>384000 || channels<1 || channels>16 || isFloat && bits!=32 || !isFloat && bits!=16 && bits!=24 && bits!=32)throw new NotSupportedException("PCM analysis format unavailable");
            }catch {Dispose();throw;}finally {if(type!=IntPtr.Zero)Marshal.Release(type);if(actual!=IntPtr.Zero)Marshal.Release(actual);}
        }
        public void Seek(double seconds) {Guid format=Guid.Empty;Variant position=new Variant {Type=20,Value=(long)(seconds*10000000)};Check(Slot<Position>(reader,8)(reader,ref format,ref position));filled=0;averaged=0;leftSum=rightSum=0;}
        public List<SpectrumFrame> Read() {
            uint actual,flags;long stamp;IntPtr sample=IntPtr.Zero,buffer=IntPtr.Zero;bool locked=false;
            var result=new List<SpectrumFrame>();
            try {
                Check(Slot<ReadSample>(reader,9)(reader,Audio,0,out actual,out flags,out stamp,out sample));
                if((flags&1)!=0 || (flags&0x20)!=0)throw new InvalidOperationException("Audio analysis stream changed");
                if(sample==IntPtr.Zero)return (flags&2)!=0 ? null : result;
                Check(Slot<Contiguous>(sample,41)(sample,out buffer));IntPtr data;uint maximum,current;Check(Slot<LockBuffer>(buffer,3)(buffer,out data,out maximum,out current));locked=true;
                if(current>16777216)throw new InvalidOperationException("Audio analysis buffer too large");
                byte[] bytes=new byte[current];Marshal.Copy(data,bytes,0,(int)current);int step=bits/8*channels;
                for(int at=0;at+step<=bytes.Length;at+=step) {
                    if(filled==0 && averaged==0)windowTime=stamp/10000000.0+(at/(double)step)/rate;
                    leftSum+=Decode(bytes,at);rightSum+=Decode(bytes,at+(channels>1 ? bits/8 : 0));averaged++;
                    if(averaged<decimation)continue;
                    window[filled]=leftSum/averaged;rightWindow[filled]=rightSum/averaged;filled++;averaged=0;leftSum=rightSum=0;
                    if(filled==window.Length) {
                        int analysisRate=rate/decimation;
                        double[] bands=SpectrumMath.Analyze(window,analysisRate),right=SpectrumMath.Analyze(rightWindow,analysisRate);
                        for(int i=0;i<bands.Length;i++)bands[i]=Math.Max(bands[i],right[i]);
                        result.Add(new SpectrumFrame(windowTime+window.Length*decimation/(2.0*rate),bands));filled=0;
                    }
                }
                return result;
            }finally {if(locked)Slot<UnlockBuffer>(buffer,4)(buffer);if(buffer!=IntPtr.Zero)Marshal.Release(buffer);if(sample!=IntPtr.Zero)Marshal.Release(sample);}
        }
        double Decode(byte[] bytes,int at) {
            if(isFloat)return BitConverter.ToSingle(bytes,at);
            if(bits==16)return (short)(bytes[at]|bytes[at+1]<<8)/32768.0;
            if(bits==24)return ((bytes[at]|bytes[at+1]<<8|bytes[at+2]<<16)<<8)/2147483648.0;
            return BitConverter.ToInt32(bytes,at)/2147483648.0;
        }
        public void Dispose() {if(reader!=IntPtr.Zero) {Marshal.Release(reader);reader=IntPtr.Zero;}if(started) {MFShutdown();started=false;}if(com) {CoUninitialize();com=false;}}
    }
}
