using Screna.Audio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Screna.Avi
{
    class AviInternalWriter : IDisposable, IAviStreamWriteHandler
    {
        const int MaxSuperIndexEntries = 256,
                  MaxIndexEntries = 15000,
                  Index1EntrySize = 4 * sizeof(uint),
                  RiffAviSizeTreshold = 512 * 1024 * 1024,
                  RiffAvixSizeTreshold = int.MaxValue - 1024 * 1024;

        static readonly FourCC ListTypeRiff = new FourCC("RIFF");

        static class RiffListFourCCs
        {
            public static readonly FourCC Avi = new FourCC("AVI");

            public static readonly FourCC AviExtended = new FourCC("AVIX");

            public static readonly FourCC Header = new FourCC("hdrl");

            public static readonly FourCC Stream = new FourCC("strl");

            public static readonly FourCC OpenDml = new FourCC("odml");

            public static readonly FourCC Movie = new FourCC("movi");
        }

        readonly BinaryWriter _fileWriter;
        bool _isClosed;
        bool _startedWriting;
        readonly object _syncWrite = new object();

        bool _isFirstRiff = true;
        RiffItem _currentRiff, _currentMovie, _header;
        int _riffSizeTreshold, _riffAviFrameCount = -1, _index1Count;
        readonly List<IAviStreamInternal> _streams = new List<IAviStreamInternal>();
        StreamInfo[] _streamsInfo;

        public AviInternalWriter(string FileName)
        {
            var fileStream = new FileStream(FileName, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);
            _fileWriter = new BinaryWriter(fileStream);
        }

        /// <summary>Frame rate.</summary>
        /// <remarks>
        /// The value of the property is rounded to 3 fractional digits.
        /// Default value is <c>1</c>.
        /// </remarks>
        public decimal FramesPerSecond
        {
            get { return _framesPerSecond; }
            set
            {
                lock (_syncWrite)
                {
                    CheckNotStartedWriting();
                    _framesPerSecond = decimal.Round(value, 3);
                }
            }
        }

        decimal _framesPerSecond = 1;
        uint _frameRateNumerator, _frameRateDenominator;

        /// <summary>
        /// Whether to emit index used in AVI v1 format.
        /// </summary>
        /// <remarks>
        /// By default, only index conformant to OpenDML AVI extensions (AVI v2) is emitted. 
        /// Presence of v1 index may improve the compatibility of generated AVI files with certain software, 
        /// especially when there are multiple streams.
        /// </remarks>
        public bool EmitIndex1
        {
            get { return _emitIndex1; }
            set
            {
                lock (_syncWrite)
                {
                    CheckNotStartedWriting();
                    _emitIndex1 = value;
                }
            }
        }

        bool _emitIndex1;

        ReadOnlyCollection<IAviStreamInternal> Streams => _streams.AsReadOnly();

        /// <summary>Adds new video stream.</summary>
        /// <param name="Width">Frame's width.</param>
        /// <param name="Height">Frame's height.</param>
        /// <param name="BitsPerPixel">Bits per pixel.</param>
        /// <returns>Newly added video stream.</returns>
        /// <remarks>
        /// Stream is initialized to be ready for uncompressed video (bottom-up BGR) with specified parameters.
        /// However, properties (such as <see cref="IAviVideoStream.Codec"/>) can be changed later if the stream is to be fed with pre-compressed data.
        /// </remarks>
        public IAviVideoStream AddVideoStream(int Width = 1, int Height = 1, BitsPerPixel BitsPerPixel = BitsPerPixel.Bpp32)
        {
            return AddStream<IAviVideoStreamInternal>(Index =>
                {
                    var stream = new AviVideoStream(Index, this, Width, Height, BitsPerPixel);
                    var asyncStream = new AsyncVideoStreamWrapper(stream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new encoding video stream.</summary>
        /// <param name="Encoder">Encoder to be used.</param>
        /// <param name="OwnsEncoder">Whether encoder should be disposed with the writer.</param>
        /// <param name="Width">Frame's width.</param>
        /// <param name="Height">Frame's height.</param>
        /// <returns>Newly added video stream.</returns>
        /// <remarks>
        /// <para>
        /// Stream is initialized to be to be encoded with the specified encoder.
        /// Method <see cref="IAviVideoStream.WriteFrame"/> expects data in the same format as encoders,
        /// that is top-down BGR32 bitmap. It is passed to the encoder and the encoded result is written
        /// to the stream.
        /// Parameters <c>isKeyFrame</c> and <c>length</c> are ignored by encoding streams,
        /// as encoders determine on their own which frames are keys, and the size of input bitmaps is fixed.
        /// </para>
        /// <para>
        /// Properties <see cref="IAviVideoStream.Codec"/> and <see cref="IAviVideoStream.BitsPerPixel"/> 
        /// are defined by the encoder, and cannot be modified.
        /// </para>
        /// </remarks>
        public IAviVideoStream AddEncodingVideoStream(IVideoEncoder Encoder, bool OwnsEncoder = true, int Width = 1, int Height = 1)
        {
            return AddStream<IAviVideoStreamInternal>(Index =>
                {
                    var stream = new AviVideoStream(Index, this, Width, Height, BitsPerPixel.Bpp32);
                    var encodingStream = new EncodingVideoStreamWrapper(stream, Encoder, OwnsEncoder);
                    var asyncStream = new AsyncVideoStreamWrapper(encodingStream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new audio stream.</summary>
        /// <param name="Wf">WaveFormat of audio data.</param>
        /// <returns>Newly added audio stream.</returns>
        /// <remarks>
        /// Stream is initialized to be ready for uncompressed audio (PCM) with specified parameters.
        /// However, some properties can be changed later if the stream is to be fed with pre-compressed data.
        /// </remarks>
        public IAviAudioStream AddAudioStream(WaveFormat Wf)
        {
            return AddStream<IAviAudioStreamInternal>(Index =>
                {
                    var stream = new AviAudioStream(Index, this, Wf);
                    var asyncStream = new AsyncAudioStreamWrapper(stream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new encoding audio stream.</summary>
        /// <param name="Encoder">Encoder to be used.</param>
        /// <param name="OwnsEncoder">Whether encoder should be disposed with the writer.</param>
        /// <returns>Newly added audio stream.</returns>
        /// <remarks>
        /// <para>
        /// Stream is initialized to be to be encoded with the specified encoder.
        /// Method <see cref="IAviAudioStream.WriteBlock"/> expects data in the same format as encoder (see encoder's docs). 
        /// The data is passed to the encoder and the encoded result is written to the stream.
        /// </para>
        /// </remarks>
        public IAviAudioStream AddEncodingAudioStream(IAudioEncoder Encoder, bool OwnsEncoder = true)
        {
            return AddStream<IAviAudioStreamInternal>(Index =>
                {
                    var stream = new AviAudioStream(Index, this, new WaveFormat(44100, 16, 1));
                    var encodingStream = new EncodingAudioStreamWrapper(stream, Encoder, OwnsEncoder);
                    return new AsyncAudioStreamWrapper(encodingStream);
                });
        }

        TStream AddStream<TStream>(Func<int, TStream> StreamFactory)
            where TStream : IAviStreamInternal
        {
            lock (_syncWrite)
            {
                CheckNotClosed();
                CheckNotStartedWriting();

                var stream = StreamFactory.Invoke(Streams.Count);

                _streams.Add(stream);

                return stream;
            }
        }

        /// <summary>
        /// Closes the writer and AVI file itself.
        /// </summary>
        public void Close()
        {
            try
            {
                if (_isClosed)
                    return;

                bool finishWriting;
                lock (_syncWrite) finishWriting = _startedWriting;

                // Call FinishWriting without holding the lock
                // because additional writes may be performed inside
                if (finishWriting)
                    foreach (var stream in _streams)
                        stream.FinishWriting();

                lock (_syncWrite)
                {
                    if (_startedWriting)
                    {
                        foreach (var stream in _streams)
                            FlushStreamIndex(stream);

                        CloseCurrentRiff();

                        // Rewrite header with actual data like frames count, super index, etc.
                        _fileWriter.BaseStream.Position = _header.ItemStart;
                        WriteHeader();
                    }

                    _fileWriter.Close();
                    _isClosed = true;
                }

                foreach (var disposableStream in _streams.OfType<IDisposable>())
                    disposableStream.Dispose();
            }
            catch (ObjectDisposedException) { }
        }

        void IDisposable.Dispose() => Close();

        void CheckNotStartedWriting()
        {
            if (_startedWriting)
                throw new InvalidOperationException("No stream information can be changed after starting to write frames.");
        }

        void CheckNotClosed() { if (_isClosed) throw new ObjectDisposedException(typeof(AviInternalWriter).Name); }

        void PrepareForWriting()
        {
            _startedWriting = true;
            foreach (var stream in _streams) stream.PrepareForWriting();

            Extensions.SplitFrameRate(FramesPerSecond, out _frameRateNumerator, out _frameRateDenominator);

            _streamsInfo = _streams.Select(s => new StreamInfo(RIFFChunksFourCCs.IndexData(s.Index))).ToArray();

            _riffSizeTreshold = RiffAviSizeTreshold;

            _currentRiff = _fileWriter.OpenList(RiffListFourCCs.Avi, ListTypeRiff);
            WriteHeader();
            _currentMovie = _fileWriter.OpenList(RiffListFourCCs.Movie);
        }

        void CreateNewRiffIfNeeded(int ApproximateSizeOfNextChunk)
        {
            var estimatedSize = _fileWriter.BaseStream.Position + ApproximateSizeOfNextChunk - _currentRiff.ItemStart;
            if (_isFirstRiff && _emitIndex1) estimatedSize += RiffItem.ITEM_HEADER_SIZE + _index1Count * Index1EntrySize;

            if (estimatedSize <= _riffSizeTreshold)
                return;

            CloseCurrentRiff();

            _currentRiff = _fileWriter.OpenList(RiffListFourCCs.AviExtended, ListTypeRiff);
            _currentMovie = _fileWriter.OpenList(RiffListFourCCs.Movie);
        }

        void CloseCurrentRiff()
        {
            _fileWriter.CloseItem(_currentMovie);

            // Several special actions for the first RIFF (AVI)
            if (_isFirstRiff)
            {
                _riffAviFrameCount = _streams.OfType<IAviVideoStream>().Max(s => _streamsInfo[s.Index].FrameCount);
                if (_emitIndex1) WriteIndex1();
                _riffSizeTreshold = RiffAvixSizeTreshold;
            }

            _fileWriter.CloseItem(_currentRiff);
            _isFirstRiff = false;
        }

        #region IAviStreamDataHandler implementation
        void IAviStreamWriteHandler.WriteVideoFrame(AviVideoStream stream, bool isKeyFrame, byte[] frameData, int startIndex, int count)
        {
            WriteStreamFrame(stream, isKeyFrame, frameData, startIndex, count);
        }

        void IAviStreamWriteHandler.WriteAudioBlock(AviAudioStream stream, byte[] blockData, int startIndex, int count)
        {
            WriteStreamFrame(stream, true, blockData, startIndex, count);
        }

        void WriteStreamFrame(IAviStreamInternal stream, bool isKeyFrame, byte[] frameData, int startIndex, int count)
        {
            lock (_syncWrite)
            {
                CheckNotClosed();

                if (!_startedWriting)
                    PrepareForWriting();

                var si = _streamsInfo[stream.Index];
                if (si.SuperIndex.Count == MaxSuperIndexEntries)
                    throw new InvalidOperationException("Cannot write more frames to this stream.");

                if (ShouldFlushStreamIndex(si.StandardIndex))
                    FlushStreamIndex(stream);

                var shouldCreateIndex1Entry = _emitIndex1 && _isFirstRiff;

                CreateNewRiffIfNeeded(count + (shouldCreateIndex1Entry ? Index1EntrySize : 0));

                var chunk = _fileWriter.OpenChunk(stream.ChunkId, count);
                _fileWriter.Write(frameData, startIndex, count);
                _fileWriter.CloseItem(chunk);

                si.OnFrameWritten(chunk.DataSize);
                var dataSize = (uint)chunk.DataSize;
                
                // Set highest bit for non-key frames according to the OpenDML spec
                if (!isKeyFrame)
                    dataSize |= 0x80000000U;

                var newEntry = new StandardIndexEntry
                {
                    DataOffset = chunk.DataStart,
                    DataSize = dataSize
                };

                si.StandardIndex.Add(newEntry);

                if (!shouldCreateIndex1Entry)
                    return;

                var index1Entry = new Index1Entry
                {
                    IsKeyFrame = isKeyFrame,
                    DataOffset = (uint)(chunk.ItemStart - _currentMovie.DataStart),
                    DataSize = dataSize
                };

                si.Index1.Add(index1Entry);
                _index1Count++;
            }
        }

        void IAviStreamWriteHandler.WriteStreamHeader(AviVideoStream VideoStream)
        {
            // See AVISTREAMHEADER structure
            _fileWriter.Write((uint)VideoStream.StreamType);
            _fileWriter.Write((uint)VideoStream.Codec);
            _fileWriter.Write(0U); // StreamHeaderFlags
            _fileWriter.Write((ushort)0); // priority
            _fileWriter.Write((ushort)0); // language
            _fileWriter.Write(0U); // initial frames
            _fileWriter.Write(_frameRateDenominator); // scale (frame rate denominator)
            _fileWriter.Write(_frameRateNumerator); // rate (frame rate numerator)
            _fileWriter.Write(0U); // start
            _fileWriter.Write((uint)_streamsInfo[VideoStream.Index].FrameCount); // length
            _fileWriter.Write((uint)_streamsInfo[VideoStream.Index].MaxChunkDataSize); // suggested buffer size
            _fileWriter.Write(0U); // quality
            _fileWriter.Write(0U); // sample size
            _fileWriter.Write((short)0); // rectangle left
            _fileWriter.Write((short)0); // rectangle top
            var right = (short)VideoStream.Width;
            var bottom = (short)VideoStream.Height;
            _fileWriter.Write(right); // rectangle right
            _fileWriter.Write(bottom); // rectangle bottom
        }

        void IAviStreamWriteHandler.WriteStreamHeader(AviAudioStream AudioStream)
        {
            var wf = AudioStream.WaveFormat;

            // See AVISTREAMHEADER structure
            _fileWriter.Write((uint)AudioStream.StreamType);
            _fileWriter.Write(0U); // no codec
            _fileWriter.Write(0U); // StreamHeaderFlags
            _fileWriter.Write((ushort)0); // priority
            _fileWriter.Write((ushort)0); // language
            _fileWriter.Write(0U); // initial frames
            _fileWriter.Write((uint)wf.BlockAlign); // scale (sample rate denominator)
            _fileWriter.Write((uint)wf.AverageBytesPerSecond); // rate (sample rate numerator)
            _fileWriter.Write(0U); // start
            _fileWriter.Write((uint)_streamsInfo[AudioStream.Index].TotalDataSize); // length
            _fileWriter.Write((uint)(wf.AverageBytesPerSecond / 2)); // suggested buffer size (half-second)
            _fileWriter.Write(-1); // quality
            _fileWriter.Write(wf.BlockAlign); // sample size
            _fileWriter.SkipBytes(sizeof(short) * 4);
        }

        void IAviStreamWriteHandler.WriteStreamFormat(AviVideoStream VideoStream)
        {
            // See BITMAPINFOHEADER structure
            _fileWriter.Write(40U); // size of structure
            _fileWriter.Write(VideoStream.Width);
            _fileWriter.Write(VideoStream.Height);
            _fileWriter.Write((short)1); // planes
            _fileWriter.Write((ushort)VideoStream.BitsPerPixel); // bits per pixel
            _fileWriter.Write((uint)VideoStream.Codec); // compression (codec FOURCC)
            var sizeInBytes = VideoStream.Width * VideoStream.Height * ((int)VideoStream.BitsPerPixel / 8);
            _fileWriter.Write((uint)sizeInBytes); // image size in bytes
            _fileWriter.Write(0); // X pixels per meter
            _fileWriter.Write(0); // Y pixels per meter

            // Writing grayscale palette for 8-bit uncompressed stream
            // Otherwise, no palette
            if (VideoStream.BitsPerPixel == BitsPerPixel.Bpp8 && VideoStream.Codec == AviCodec.Uncompressed.FourCC)
            {
                _fileWriter.Write(256U); // palette colors used
                _fileWriter.Write(0U); // palette colors important
                for (var i = 0; i < 256; i++)
                {
                    _fileWriter.Write((byte)i);
                    _fileWriter.Write((byte)i);
                    _fileWriter.Write((byte)i);
                    _fileWriter.Write((byte)0);
                }
            }
            else
            {
                _fileWriter.Write(0U); // palette colors used
                _fileWriter.Write(0U); // palette colors important
            }
        }

        void IAviStreamWriteHandler.WriteStreamFormat(AviAudioStream AudioStream)
        {
            AudioStream.WaveFormat.Serialize(_fileWriter);
        }
        #endregion

        #region Header
        void WriteHeader()
        {
            _header = _fileWriter.OpenList(RiffListFourCCs.Header);
            WriteFileHeader();
            foreach (var stream in _streams) WriteStreamList(stream);
            WriteOdmlHeader();
            WriteJunkInsteadOfMissingSuperIndexEntries();
            _fileWriter.CloseItem(_header);
        }

        void WriteJunkInsteadOfMissingSuperIndexEntries()
        {
            var missingEntriesCount = _streamsInfo.Sum(si => MaxSuperIndexEntries - si.SuperIndex.Count);

            if (missingEntriesCount <= 0)
                return;

            var junkDataSize = missingEntriesCount * sizeof(uint) * 4 - RiffItem.ITEM_HEADER_SIZE;
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.Junk, junkDataSize);
            _fileWriter.SkipBytes(junkDataSize);
            _fileWriter.CloseItem(chunk);
        }

        void WriteFileHeader()
        {
            // See AVIMAINHEADER structure
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.AviHeader);
            _fileWriter.Write((uint)decimal.Round(1000000m / FramesPerSecond)); // microseconds per frame
            // TODO: More correct computation of byterate
            _fileWriter.Write((uint)decimal.Truncate(FramesPerSecond * _streamsInfo.Sum(s => s.MaxChunkDataSize))); // max bytes per second
            _fileWriter.Write(0U); // padding granularity
            var flags = MainHeaderFlags.IsInterleaved | MainHeaderFlags.TrustChunkType;
            if (_emitIndex1) flags |= MainHeaderFlags.HasIndex;
            _fileWriter.Write((uint)flags); // MainHeaderFlags
            _fileWriter.Write(_riffAviFrameCount); // total frames (in the first RIFF list containing this header)
            _fileWriter.Write(0U); // initial frames
            _fileWriter.Write((uint)Streams.Count); // stream count
            _fileWriter.Write(0U); // suggested buffer size
            var firstVideoStream = _streams.OfType<IAviVideoStream>().First();
            _fileWriter.Write(firstVideoStream.Width); // video width
            _fileWriter.Write(firstVideoStream.Height); // video height
            _fileWriter.SkipBytes(4 * sizeof(uint)); // reserved
            _fileWriter.CloseItem(chunk);
        }

        void WriteOdmlHeader()
        {
            var list = _fileWriter.OpenList(RiffListFourCCs.OpenDml);
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.OpenDmlHeader);
            _fileWriter.Write(_streams.OfType<IAviVideoStream>().Max(s => _streamsInfo[s.Index].FrameCount)); // total frames in file
            _fileWriter.SkipBytes(61 * sizeof(uint)); // reserved
            _fileWriter.CloseItem(chunk);
            _fileWriter.CloseItem(list);
        }

        void WriteStreamList(IAviStreamInternal stream)
        {
            var list = _fileWriter.OpenList(RiffListFourCCs.Stream);
            WriteStreamHeader(stream);
            WriteStreamFormat(stream);
            WriteStreamName(stream);
            WriteStreamSuperIndex(stream);
            _fileWriter.CloseItem(list);
        }

        void WriteStreamHeader(IAviStreamInternal stream)
        {
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.StreamHeader);
            stream.WriteHeader();
            _fileWriter.CloseItem(chunk);
        }

        void WriteStreamFormat(IAviStreamInternal stream)
        {
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.StreamFormat);
            stream.WriteFormat();
            _fileWriter.CloseItem(chunk);
        }

        void WriteStreamName(IAviStream stream)
        {
            if (string.IsNullOrEmpty(stream.Name))
                return;

            var bytes = Encoding.ASCII.GetBytes(stream.Name);
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.StreamName);
            _fileWriter.Write(bytes);
            _fileWriter.Write((byte)0);
            _fileWriter.CloseItem(chunk);
        }

        void WriteStreamSuperIndex(IAviStream stream)
        {
            var superIndex = _streamsInfo[stream.Index].SuperIndex;

            // See AVISUPERINDEX structure
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.StreamIndex);
            _fileWriter.Write((ushort)4); // DWORDs per entry
            _fileWriter.Write((byte)0); // index sub-type
            _fileWriter.Write((byte)IndexType.Indexes); // index type
            _fileWriter.Write((uint)superIndex.Count); // entries count
            _fileWriter.Write((uint)((IAviStreamInternal)stream).ChunkId); // chunk ID of the stream
            _fileWriter.SkipBytes(3 * sizeof(uint)); // reserved

            // entries
            foreach (var entry in superIndex)
            {
                _fileWriter.Write((ulong)entry.ChunkOffset); // offset of sub-index chunk
                _fileWriter.Write((uint)entry.ChunkSize); // size of sub-index chunk
                _fileWriter.Write((uint)entry.Duration); // duration of sub-index data (number of frames it refers to)
            }

            _fileWriter.CloseItem(chunk);
        }
        #endregion

        #region Index
        void WriteIndex1()
        {
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.Index1);

            var indices = _streamsInfo.Select((si, i) => new { si.Index1, ChunkId = (uint)_streams.ElementAt(i).ChunkId }).
                Where(a => a.Index1.Count > 0)
                .ToList();

            while (_index1Count > 0)
            {
                var minOffset = indices[0].Index1[0].DataOffset;
                var minIndex = 0;

                for (var i = 1; i < indices.Count; i++)
                {
                    var offset = indices[i].Index1[0].DataOffset;

                    if (offset >= minOffset)
                        continue;

                    minOffset = offset;
                    minIndex = i;
                }

                var index = indices[minIndex];
                _fileWriter.Write(index.ChunkId);
                _fileWriter.Write(index.Index1[0].IsKeyFrame ? 0x00000010U : 0);
                _fileWriter.Write(index.Index1[0].DataOffset);
                _fileWriter.Write(index.Index1[0].DataSize);

                index.Index1.RemoveAt(0);
                if (index.Index1.Count == 0)
                    indices.RemoveAt(minIndex);

                _index1Count--;
            }

            _fileWriter.CloseItem(chunk);
        }

        bool ShouldFlushStreamIndex(IList<StandardIndexEntry> index)
        {
            // Check maximum number of entries
            if (index.Count >= MaxIndexEntries)
                return true;

            // Check relative offset
            return index.Count > 0 && _fileWriter.BaseStream.Position - index[0].DataOffset > uint.MaxValue;
        }

        void FlushStreamIndex(IAviStreamInternal stream)
        {
            var si = _streamsInfo[stream.Index];
            var index = si.StandardIndex;
            var entriesCount = index.Count;
            if (entriesCount == 0)
                return;

            var baseOffset = index[0].DataOffset;
            var indexSize = 24 + entriesCount * 8;

            CreateNewRiffIfNeeded(indexSize);

            // See AVISTDINDEX structure
            var chunk = _fileWriter.OpenChunk(si.StandardIndexChunkId, indexSize);
            _fileWriter.Write((ushort)2); // DWORDs per entry
            _fileWriter.Write((byte)0); // index sub-type
            _fileWriter.Write((byte)IndexType.Chunks); // index type
            _fileWriter.Write((uint)entriesCount); // entries count
            _fileWriter.Write((uint)stream.ChunkId); // chunk ID of the stream
            _fileWriter.Write((ulong)baseOffset); // base offset for entries
            _fileWriter.SkipBytes(sizeof(uint)); // reserved

            foreach (var entry in index)
            {
                _fileWriter.Write((uint)(entry.DataOffset - baseOffset)); // chunk data offset
                _fileWriter.Write(entry.DataSize); // chunk data size
            }

            _fileWriter.CloseItem(chunk);

            var superIndex = _streamsInfo[stream.Index].SuperIndex;
            var newEntry = new SuperIndexEntry
            {
                ChunkOffset = chunk.ItemStart,
                ChunkSize = chunk.ItemSize,
                Duration = entriesCount
            };
            superIndex.Add(newEntry);

            index.Clear();
        }
        #endregion
    }
}
