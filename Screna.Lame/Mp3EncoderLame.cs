using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Screna.Audio;

namespace Screna.Lame
{
    // TODO: Make NuSpec
    /// <summary>
    /// Mpeg Layer 3 (MP3) audio encoder using the LAME codec in external DLL.
    /// </summary>
    /// <remarks>
    /// Only 16-bit input audio is currently supported.
    /// The class is designed for using only a single instance at a time.
    /// Find information about and downloads of the LAME project at http://lame.sourceforge.net/.
    /// <para>
    /// Default Name for the loaded lame dll is lameenc32.dll for 32-bit process and lameenc64.dll for 64-bit process.
    /// Use <see cref="Load(string)"/> to load the library from a custom path.
    /// </para>
    /// </remarks>
    public class Mp3EncoderLame : IAudioEncoder
    {
        /// <summary>
        /// Supported output bit rates (in kilobits per second).
        /// </summary>
        /// <remarks>
        /// Currently supported are 64, 96, 128, 160, 192 and 320 kbps.
        /// </remarks>
        public static readonly int[] SupportedBitRates = { 64, 96, 128, 160, 192, 320 };

        #region Loading LAME DLL
        static Type _lameFacadeType;

        /// <summary>
        /// Sets the location of LAME DLL for using by this class.
        /// </summary>
        /// <remarks>
        /// This method may be called before creating any instances of this class.
        /// The LAME DLL should have the appropriate bitness (32/64), depending on the current process.
        /// If it is not already loaded into the process, the method loads it automatically.
        /// </remarks>
        public static void Load(string LameDllPath)
        {
            var libraryName = Path.GetFileName(LameDllPath);

            if (!IsLibraryLoaded(libraryName))
            {
                var loadResult = LoadLibrary(LameDllPath);

                if (loadResult == IntPtr.Zero)
                    throw new DllNotFoundException($"Library '{LameDllPath}' could not be loaded.");
            }

            var facadeAssembly = GenerateLameFacadeAssembly(libraryName);
            _lameFacadeType = facadeAssembly.GetType(typeof(Mp3EncoderLame).Namespace + ".LameFacadeImpl");
        }

        static Assembly GenerateLameFacadeAssembly(string LameDllName)
        {
            var csCompiler = new Microsoft.CSharp.CSharpCodeProvider();

            var compilerOptions = new System.CodeDom.Compiler.CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
                IncludeDebugInformation = false,
                CompilerOptions = "/optimize",
                ReferencedAssemblies = { "mscorlib.dll" },
                OutputAssembly = "Screna.Lame.Facade"
            };

            var sourceCode = GetLameFacadeAssemblySource(LameDllName);
            var compilerResult = csCompiler.CompileAssemblyFromSource(compilerOptions, sourceCode);

            if (compilerResult.Errors.HasErrors)
                throw new Exception("Could not generate LAME facade assembly.");

            return compilerResult.CompiledAssembly;
        }

        static string GetLameFacadeAssemblySource(string LameDllName)
        {
            string sourceCode;

            using (var sourceStream = typeof(Mp3EncoderLame).Assembly.GetManifestResourceStream("Screna.Lame.LameFacadeImpl.cs"))
            using (var sourceReader = new StreamReader(sourceStream))
                sourceCode = sourceReader.ReadToEnd();
            
            return sourceCode.Replace("\"lame_enc.dll\"", $"\"{LameDllName}\"");
        }

        static bool IsLibraryLoaded(string LibraryName)
        {
            return Process.GetCurrentProcess().Modules.Cast<ProcessModule>().
                Any(m => string.Compare(m.ModuleName, LibraryName, StringComparison.InvariantCultureIgnoreCase) == 0);
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string FileName);
        #endregion

        const int SampleByteSize = 2;

        readonly dynamic _lameFacade;

        /// <summary>
        /// Creates a new instance of <see cref="Mp3EncoderLame"/>.
        /// </summary>
        /// <param name="ChannelCount">Channel count.</param>
        /// <param name="SampleRate">Sample rate (in samples per second).</param>
        /// <param name="OutputBitRateKbps">Output bit rate (in kilobits per second).</param>
        /// <remarks>
        /// Encoder expects audio data in 16-bit samples.
        /// Stereo data should be interleaved: left sample first, right sample second.
        /// </remarks>
        public Mp3EncoderLame(int ChannelCount = 1, int SampleRate = 44100, int OutputBitRateKbps = 160)
        {
            if (_lameFacadeType == null)
                Load(Path.Combine(Environment.CurrentDirectory, $"lameenc{(Environment.Is64BitProcess ? 64 : 32)}.dll"));

            _lameFacade = Activator.CreateInstance(_lameFacadeType);
            _lameFacade.ChannelCount = ChannelCount;
            _lameFacade.InputSampleRate = SampleRate;
            _lameFacade.OutputBitRate = OutputBitRateKbps;

            _lameFacade.PrepareEncoding();

            WaveFormat = new Mp3WaveFormat(SampleRate, ChannelCount, _lameFacade.FrameSize, EncoderDelay: _lameFacade.EncoderDelay);
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose() => _lameFacade?.Dispose();

        /// <summary>
        /// Encodes block of audio data.
        /// </summary>
        public int Encode(byte[] Source, int SourceOffset, int SourceCount, byte[] Destination, int DestinationOffset)
        {
            return _lameFacade.Encode(Source, SourceOffset, SourceCount / SampleByteSize, Destination, DestinationOffset);
        }
        
        /// <summary>
        /// Ensures that the buffer is big enough to hold the result of encoding <paramref name="SourceCount"/> bytes.
        /// </summary>
        public void EnsureBufferIsSufficient(ref byte[] Buffer, int SourceCount)
        {
            var maxLength = GetMaxEncodedLength(SourceCount);
            if (Buffer?.Length >= maxLength)
                return;

            var newLength = Buffer?.Length * 2 ?? 1024;

            while (newLength < maxLength)
                newLength *= 2;

            Array.Resize(ref Buffer, newLength);
        }

        /// <summary>
        /// Flushes internal encoder's buffers.
        /// </summary>
        public int Flush(byte[] Destination, int DestinationOffset) => _lameFacade.FinishEncoding(Destination, DestinationOffset);

        /// <summary>
        /// Gets if RIFF header is needed when writing to a file.
        /// </summary>
        public bool RequiresRiffHeader => false;

        /// <summary>
        /// Gets maximum length of encoded data. Estimate taken from the description of 'lame_encode_buffer' method in 'lame.h'
        /// </summary>
        public int GetMaxEncodedLength(int SourceCount) => (int)Math.Ceiling(1.25 * SourceCount / SampleByteSize + 7200);

        /// <summary>
        /// Wave Format including Mp3 Specific Data.
        /// </summary>
        public WaveFormat WaveFormat { get; }
    }
}
