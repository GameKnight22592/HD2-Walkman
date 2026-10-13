using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace HD2Walkman {
    // Minimal, typed calls to the Windows SDK IMFMediaEngine ABI. No decoder DLLs or browser.
    [ComVisible(true), Guid("FEE7C112-E776-42B5-9BBF-0048524E2BD5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMediaNotify { [PreserveSig] int EventNotify(uint kind, UIntPtr value, uint detail); }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class MediaNotify : IMediaNotify {
        readonly ConcurrentQueue<uint> events;
        public MediaNotify(ConcurrentQueue<uint> queue) { events = queue; }
        public int EventNotify(uint kind, UIntPtr value, uint detail) { if (kind == 5 || kind == 14 || kind == 19) events.Enqueue(kind); return 0; }
    }
    public sealed class NativeAudio : IDisposable {
        [DllImport("mfplat.dll", ExactSpelling=true)] static extern int MFStartup(uint version, uint flags);
        [DllImport("mfplat.dll", ExactSpelling=true)] static extern int MFShutdown();
        [DllImport("mfplat.dll", ExactSpelling=true)] static extern int MFCreateAttributes(out IntPtr attrs, uint count);
        [DllImport("ole32.dll", ExactSpelling=true)] static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr instance);
        [DllImport("ole32.dll", ExactSpelling=true)] static extern int CoInitializeEx(IntPtr reserved, uint mode);
        [DllImport("ole32.dll", ExactSpelling=true)] static extern void CoUninitialize();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateEngine(IntPtr self, uint flags, IntPtr attrs, out IntPtr engine);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetUnknown(IntPtr self, ref Guid key, IntPtr unknown);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int HResultCall(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int BoolSetter(IntPtr self, int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DoubleSetter(IntPtr self, double value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate double DoubleGetter(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int BoolGetter(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SourceSetter(IntPtr self, IntPtr bstr);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetError(IntPtr self, out IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate ushort GetErrorCode(IntPtr self);
        IntPtr factory, engine;
        bool started, comStarted, wantsPlay, ready, disposed;
        double volume = .5;
        MediaNotify notify;
        ConcurrentQueue<uint> events;
        public string Error { get; private set; }
        static T Slot<T>(IntPtr obj, int index) where T : class { return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), index * IntPtr.Size), typeof(T)); }
        static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
        public NativeAudio() {
            try {
                Check(CoInitializeEx(IntPtr.Zero, 2)); comStarted = true;
                Check(MFStartup(0x20070, 0)); started = true;
                Guid cls = new Guid("B44392DA-499B-446B-A4CB-005FEAD0E6D5"), iid = new Guid("4D645ACE-26AA-4688-9BE1-DF3516990B93");
                Check(CoCreateInstance(ref cls, IntPtr.Zero, 1, ref iid, out factory));
                NewEngine();
            } catch { Dispose(); throw; }
        }
        void NewEngine() {
            CloseEngine();
            events = new ConcurrentQueue<uint>(); notify = new MediaNotify(events);
            IntPtr attrs = IntPtr.Zero, callback = IntPtr.Zero;
            try {
                Check(MFCreateAttributes(out attrs, 1)); callback = Marshal.GetIUnknownForObject(notify);
                Guid key = new Guid("C60381B8-83A4-41F8-A3D0-DE05076849A9");
                Check(Slot<SetUnknown>(attrs, 27)(attrs, ref key, callback));
                Check(Slot<CreateEngine>(factory, 3)(factory, 1, attrs, out engine)); // AUDIOONLY
                Check(Slot<BoolSetter>(engine, 29)(engine, 0));
                SetVolume(volume);
            } finally { if (callback != IntPtr.Zero) Marshal.Release(callback); if (attrs != IntPtr.Zero) Marshal.Release(attrs); }
        }
        public void Open(string file, bool autoplay = true) {
            if (!System.IO.File.Exists(file)) throw new System.IO.FileNotFoundException("Song is missing", file);
            NewEngine(); Error = null; ready = false; wantsPlay = autoplay;
            IntPtr url = Marshal.StringToBSTR(new Uri(System.IO.Path.GetFullPath(file)).AbsoluteUri);
            try { Check(Slot<SourceSetter>(engine, 6)(engine, url)); Check(Slot<HResultCall>(engine, 12)(engine)); }
            finally { Marshal.FreeBSTR(url); }
        }
        public bool Poll() {
            if (engine == IntPtr.Zero) return false;
            uint kind; bool ended = false;
            while (events.TryDequeue(out kind)) {
                if (kind == 14) { ready = true; if (wantsPlay) Check(Slot<HResultCall>(engine, 32)(engine)); }
                else if (kind == 19 && Slot<BoolGetter>(engine, 27)(engine) != 0) ended = true;
                else if (kind == 5) {
                    IntPtr error = IntPtr.Zero;
                    try { Check(Slot<GetError>(engine, 3)(engine, out error)); if (error != IntPtr.Zero) Error = "Windows could not decode this file (media error " + Slot<GetErrorCode>(error, 3)(error) + ")."; }
                    finally { if (error != IntPtr.Zero) Marshal.Release(error); }
                    if (Error == null) Error = "Windows audio playback failed.";
                    wantsPlay = false;
                }
            }
            return ended;
        }
        public void Toggle() { if (engine == IntPtr.Zero) return; wantsPlay = !wantsPlay; if (ready) Check(Slot<HResultCall>(engine, wantsPlay ? 32 : 33)(engine)); }
        public void Stop() { wantsPlay = false; if (engine != IntPtr.Zero && ready) Check(Slot<HResultCall>(engine, 33)(engine)); }
        public void Seek(double seconds) { if (ready) Check(Slot<DoubleSetter>(engine, 17)(engine, Math.Max(0, Math.Min(Duration, seconds)))); }
        public void SetVolume(double value) { volume = Math.Max(0, Math.Min(1, value)); if (engine != IntPtr.Zero) Check(Slot<DoubleSetter>(engine, 37)(engine, volume)); }
        public double Position { get { return engine == IntPtr.Zero || !ready ? 0 : Finite(Slot<DoubleGetter>(engine, 16)(engine)); } }
        public double Duration { get { return engine == IntPtr.Zero || !ready ? 0 : Finite(Slot<DoubleGetter>(engine, 19)(engine)); } }
        static double Finite(double value) { return Double.IsNaN(value) || Double.IsInfinity(value) ? 0 : Math.Max(0, value); }
        public uint State { get { if (Error != null) return 4; if (!ready) return wantsPlay ? 3u : 0u; return wantsPlay ? 1u : 2u; } }
        void CloseEngine() { if (engine != IntPtr.Zero) { try { Slot<HResultCall>(engine, 42)(engine); } finally { Marshal.Release(engine); engine = IntPtr.Zero; } } notify = null; ready = false; wantsPlay = false; }
        public void Dispose() { if (disposed) return; disposed = true; try { CloseEngine(); } finally { if (factory != IntPtr.Zero) { Marshal.Release(factory); factory = IntPtr.Zero; } if (started) { MFShutdown(); started = false; } if (comStarted) { CoUninitialize(); comStarted = false; } } }
    }
}
