using System;
using System.Runtime.InteropServices;

namespace omtplugin
{
    // OMT-HX (omthx.dll on Windows, libomthx.so on Linux): hardware HEVC/H.264 inside OMT.
    // Plain OMT keeps using libomtnet; only the Hx mode goes through this C API.
    internal static class NativeOmthx
    {
        public const int ModeFull = 0;
        public const int ModeHx = 1;
        public const int QualityLow = 0;
        public const int QualityMedium = 1;
        public const int QualityHigh = 2;
        public const int CodecHevc = 0;
        public const int CodecH264 = 1;
        public const int PixelBgra = 0;
        public const int PixelUyvy = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct VideoFormat
        {
            public int Width;
            public int Height;
            public int FpsN;
            public int FpsD;
            public int Alpha;
        }

        private const string Dll = "omthx";

        // HX needs the native library, which ships for Windows and Linux; macOS is plain OMT for now.
        public static bool Available => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr omthx_c_error();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr omthx_c_sender_open([MarshalAs(UnmanagedType.LPUTF8Str)] string name, int mode,
            int quality, int codec);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void omthx_c_sender_close(IntPtr sender);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int omthx_c_sender_video(IntPtr sender, ref VideoFormat format, int pixelFormat,
            IntPtr data, int stride, int premultiplied, long timestamp);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int omthx_c_sender_video_nv12(IntPtr sender, ref VideoFormat format, IntPtr y, int yStride,
            IntPtr uv, int uvStride, int fullRange, long timestamp);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int omthx_c_sender_audio(IntPtr sender, IntPtr planar, int sampleRate, int channels,
            int samplesPerChannel, long timestamp);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void omthx_c_sender_keyframe(IntPtr sender);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr omthx_c_sender_address(IntPtr sender);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int omthx_c_probe([MarshalAs(UnmanagedType.LPUTF8Str)] string address, int timeoutMs,
            out VideoFormat format, out int mode, out int codec);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr omthx_c_receiver_open([MarshalAs(UnmanagedType.LPUTF8Str)] string address);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void omthx_c_receiver_close(IntPtr receiver);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int omthx_c_receiver_video(IntPtr receiver, int timeoutMs, out VideoFormat format,
            out int premultiplied, out IntPtr bgra, out long timestamp);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int omthx_c_receiver_audio(IntPtr receiver, int timeoutMs, out IntPtr planar,
            out int sampleRate, out int channels, out int samplesPerChannel, out long timestamp);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void omthx_c_receiver_tally(IntPtr receiver, int preview, int program);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void omthx_c_receiver_keyframe(IntPtr receiver);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr omthx_c_receiver_decoder(IntPtr receiver);

        public static string Error()
        {
            try
            {
                return Marshal.PtrToStringUTF8(omthx_c_error()) ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static IntPtr SenderOpen(string name, int mode, int quality, int codec) =>
            omthx_c_sender_open(name, mode, quality, codec);

        public static void SenderClose(IntPtr sender) => omthx_c_sender_close(sender);

        public static int SenderVideo(IntPtr sender, ref VideoFormat format, int pixelFormat, IntPtr data, int stride,
            bool premultiplied, long timestamp) =>
            omthx_c_sender_video(sender, ref format, pixelFormat, data, stride, premultiplied ? 1 : 0, timestamp);

        public static int SenderVideoNv12(IntPtr sender, ref VideoFormat format, IntPtr y, int yStride, IntPtr uv,
            int uvStride, bool fullRange, long timestamp) =>
            omthx_c_sender_video_nv12(sender, ref format, y, yStride, uv, uvStride, fullRange ? 1 : 0, timestamp);

        public static int SenderAudio(IntPtr sender, IntPtr planar, int sampleRate, int channels, int samples,
            long timestamp) =>
            omthx_c_sender_audio(sender, planar, sampleRate, channels, samples, timestamp);

        public static void SenderKeyframe(IntPtr sender) => omthx_c_sender_keyframe(sender);

        public static string SenderAddress(IntPtr sender) =>
            Marshal.PtrToStringUTF8(omthx_c_sender_address(sender)) ?? "";

        public static IntPtr ReceiverOpen(string address) => omthx_c_receiver_open(address);

        // What the selected source sends. A source that does not answer in time is treated as plain OMT,
        // the path that has always worked without the native library.
        public static int DetectMode(string address, int timeoutMs = 1500)
        {
            try
            {
                return omthx_c_probe(address, timeoutMs, out _, out int mode, out _) == 1 ? mode : ModeFull;
            }
            catch (Exception)
            {
                return ModeFull;
            }
        }

        public static void ReceiverClose(IntPtr receiver) => omthx_c_receiver_close(receiver);

        public static int ReceiverVideo(IntPtr receiver, int timeoutMs, out VideoFormat format,
            out bool premultiplied, out IntPtr bgra, out long timestamp)
        {
            int ok = omthx_c_receiver_video(receiver, timeoutMs, out format, out int premul, out bgra, out timestamp);
            premultiplied = premul != 0;
            return ok;
        }

        public static int ReceiverAudio(IntPtr receiver, int timeoutMs, out IntPtr planar, out int sampleRate,
            out int channels, out int samples, out long timestamp) =>
            omthx_c_receiver_audio(receiver, timeoutMs, out planar, out sampleRate, out channels, out samples,
                out timestamp);

        public static void ReceiverTally(IntPtr receiver, bool preview, bool program) =>
            omthx_c_receiver_tally(receiver, preview ? 1 : 0, program ? 1 : 0);

        public static void ReceiverKeyframe(IntPtr receiver) => omthx_c_receiver_keyframe(receiver);

        public static string ReceiverDecoder(IntPtr receiver) =>
            Marshal.PtrToStringUTF8(omthx_c_receiver_decoder(receiver)) ?? "";
    }
}