using System;
using System.Runtime.InteropServices;

namespace Screna.Avi
{
    /// <summary>
    /// Selected constants, structures and functions from Video for Windows APIs.
    /// </summary>
    /// <remarks>
    /// Useful for implementing stream encoding using VCM codecs.
    /// See Windows API documentation on the meaning and usage of all this stuff.
    /// </remarks>
    static class VfwApi
    {
        public const int ICERR_OK = 0;

        public const short ICMODE_COMPRESS = 1;

        public const int ICCOMPRESS_KEYFRAME = 0x00000001;

        public const int AVIIF_KEYFRAME = 0x00000010;

        public const int ICM_COMPRESS_GET_SIZE = 0x4005;
        public const int ICM_COMPRESS_QUERY = 0x4006;
        public const int ICM_COMPRESS_BEGIN = 0x4007;
        public const int ICM_COMPRESS_END = 0x4009;
        public const int ICM_COMPRESS_FRAMES_INFO = 0x4046;

        [Flags]
        enum CompressorFlags
        {
            SupportsQuality = 0x0001,
            RequestsCompressFrames = 0x0008,
            SupportsFastTemporalCompression = 0x0020
        }

        /// <summary>
        /// Corresponds to the <c>BITMAPINFOHEADER</c> structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BitmapInfoHeader
        {
            public uint SizeOfStruct;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint ImageSize;
            public int PixelsPerMeterX;
            public int PixelsPerMeterY;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }

        /// <summary>
        /// Corresponds to the <c>ICINFO</c> structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CompressorInfo
        {
            uint sizeOfStruct;
            uint fccType;
            uint fccHandler;
            CompressorFlags flags;
            uint version;
            uint versionIcm;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string Name;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Driver;

            public bool SupportsQuality => flags.HasFlag(CompressorFlags.SupportsQuality);
            
            public bool SupportsFastTemporalCompression => flags.HasFlag(CompressorFlags.SupportsFastTemporalCompression);

            public bool RequestsCompressFrames => flags.HasFlag(CompressorFlags.RequestsCompressFrames);
        }

        /// <summary>
        /// Corresponds to the <c>ICCOMPRESSFRAMES</c> structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CompressFramesInfo
        {
            uint flags;
            public IntPtr OutBitmapInfoPtr;
            int outputSize;
            public IntPtr InBitmapInfoPtr;
            int inputSize;
            public int StartFrame;
            public int FrameCount;
            /// <summary>Quality from 0 to 10000.</summary>
            public int Quality;
            int dataRate;
            /// <summary>Interval between key frames.</summary>
            /// <remarks>Equal to 1 if each frame is a key frame.</remarks>
            public int KeyRate;
            public uint FrameRateNumerator;
            public uint FrameRateDenominator;
            uint overheadPerFrame;
            uint reserved2;
            IntPtr getDataFuncPtr;
            IntPtr setDataFuncPtr;
        }

        const string DllName = "msvfw32.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr ICOpen(uint FccType, uint FccHandler, int Mode);

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi)]
        public static extern int ICClose(IntPtr Handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi)]
        public static extern int ICSendMessage(IntPtr Handle, int Message, IntPtr Param1, IntPtr Param2);

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi)]
        public static extern int ICSendMessage(IntPtr Handle, int Message, ref BitmapInfoHeader InHeader, ref BitmapInfoHeader OutHeader);

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi)]
        public static extern int ICSendMessage(IntPtr Handle, int Message, ref CompressFramesInfo Info, int SizeOfInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi)]
        public static extern int ICGetInfo(IntPtr Handle, out CompressorInfo Info, int InfoSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ICCompress(IntPtr Handle,
                                            int InFlags,
                                            ref BitmapInfoHeader OutHeader,
                                            IntPtr EncodedData,
                                            ref BitmapInfoHeader InHeader,
                                            IntPtr FrameData,
                                            out int ChunkId,
                                            out int OutFlags,
                                            int FrameNumber,
                                            int RequestedFrameSize,
                                            int RequestedQuality,
                                            IntPtr PrevHeaderPtr,
                                            IntPtr PrevFrameData);
    }
}
