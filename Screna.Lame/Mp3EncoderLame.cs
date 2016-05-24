using System;
using System.IO;
using Screna.Audio;

namespace Screna.Lame
{
    /// <summary>
    /// Mpeg Layer 3 (MP3) audio encoder using the lameenc32.dll (x86) or lameenc64.dll (x64).
    /// </summary>
    /// <remarks>
    /// Only 16-bit input audio is currently supported.
    /// The class is designed for using only a single instance at a time.
    /// Find information about and downloads of the LAME project at http://lame.sourceforge.net/.
    /// </remarks>
    public class Mp3EncoderLame : IAudioEncoder
    {
        /// <summary>
        /// Supported output bit rates (in kilobits per second).
        /// </summary>
        /// <remarks>
        /// Currently supported are 64, 96, 128, 160, 192 and 320 kbps.
        /// </remarks>
        public static int[] SupportedBitRates { get; } = { 64, 96, 128, 160, 192, 320 };

        #region Fields
        const int SampleByteSize = 2;

        readonly dynamic _lameFacade;

        static readonly Type LameFacadeType;
        #endregion

        #region Constructors
        static Mp3EncoderLame()
        {
            var csCompiler = new Microsoft.CSharp.CSharpCodeProvider();

            var compilerOptions = new System.CodeDom.Compiler.CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
                IncludeDebugInformation = false,
                CompilerOptions = "/optimize",
                ReferencedAssemblies = { "mscorlib.dll" }
            };

            var sourceCode = new LameFacade().TransformText();
            var compilerResult = csCompiler.CompileAssemblyFromSource(compilerOptions, sourceCode);

            if (compilerResult.Errors.HasErrors)
                throw new Exception("Could not generate LAME facade assembly.");

            var facadeAssembly = compilerResult.CompiledAssembly;
            LameFacadeType = facadeAssembly.GetType(typeof(Mp3EncoderLame).Namespace + ".LameFacadeImpl");
        }

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
            _lameFacade = Activator.CreateInstance(LameFacadeType);
            _lameFacade.ChannelCount = ChannelCount;
            _lameFacade.InputSampleRate = SampleRate;
            _lameFacade.OutputBitRate = OutputBitRateKbps;

            _lameFacade.PrepareEncoding();

            WaveFormat = new Mp3WaveFormat(SampleRate, ChannelCount, _lameFacade.FrameSize, EncoderDelay: _lameFacade.EncoderDelay);
        }
        #endregion

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
