using Screna.Audio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Screna.Avi
{
    /// <summary>
    /// Used to write an AVI file.
    /// </summary>
    /// <remarks>
    /// After writing begin to any of the streams, no property changes or stream addition are allowed.
    /// </remarks>
    class AviInternalWriter : IDisposable, IAviStreamWriteHandler
    {
        const int MAX_SUPER_INDEX_ENTRIES = 256;
        const int MAX_INDEX_ENTRIES = 15000;
        const int INDEX1_ENTRY_SIZE = 4 * sizeof(uint);
        const int RIFF_AVI_SIZE_TRESHOLD = 512 * 1024 * 1024;
        const int RIFF_AVIX_SIZE_TRESHOLD = int.MaxValue - 1024 * 1024;

        static readonly FourCC ListType_Riff = new FourCC("RIFF");
        static class RIFFListFourCCs
        {
            /// <summary>Top-level AVI list.</summary>
            public static readonly FourCC Avi = new FourCC("AVI");

            /// <summary>Top-level extended AVI list.</summary>
            public static readonly FourCC AviExtended = new FourCC("AVIX");

            /// <summary>Header list.</summary>
            public static readonly FourCC Header = new FourCC("hdrl");

            /// <summary>List containing stream information.</summary>
            public static readonly FourCC Stream = new FourCC("strl");

            /// <summary>List containing OpenDML headers.</summary>
            public static readonly FourCC OpenDml = new FourCC("odml");

            /// <summary>List with content chunks.</summary>
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

        /// <summary>
        /// Creates a new instance of <see cref="AviInternalWriter"/>.
        /// </summary>
        /// <param name="FileName">Path to an AVI file being written.</param>
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

        /// <summary>AVI streams that have been added so far.</summary>
        ReadOnlyCollection<IAviStreamInternal> Streams => _streams.AsReadOnly();

        /// <summary>Adds new video stream.</summary>
        /// <param name="width">Frame's width.</param>
        /// <param name="height">Frame's height.</param>
        /// <param name="bitsPerPixel">Bits per pixel.</param>
        /// <returns>Newly added video stream.</returns>
        /// <remarks>
        /// Stream is initialized to be ready for uncompressed video (bottom-up BGR) with specified parameters.
        /// However, properties (such as <see cref="IAviVideoStream.Codec"/>) can be changed later if the stream is
        /// to be fed with pre-compressed data.
        /// </remarks>
        public IAviVideoStream AddVideoStream(int width = 1, int height = 1, BitsPerPixel bitsPerPixel = BitsPerPixel.Bpp32)
        {
            return AddStream<IAviVideoStreamInternal>(index =>
                {
                    var stream = new AviVideoStream(index, this, width, height, bitsPerPixel);
                    var asyncStream = new AsyncVideoStreamWrapper(stream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new encoding video stream.</summary>
        /// <param name="encoder">Encoder to be used.</param>
        /// <param name="ownsEncoder">Whether encoder should be disposed with the writer.</param>
        /// <param name="width">Frame's width.</param>
        /// <param name="height">Frame's height.</param>
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
        public IAviVideoStream AddEncodingVideoStream(IVideoEncoder encoder, bool ownsEncoder = true, int width = 1, int height = 1)
        {
            return AddStream<IAviVideoStreamInternal>(index =>
                {
                    var stream = new AviVideoStream(index, this, width, height, BitsPerPixel.Bpp32);
                    var encodingStream = new EncodingVideoStreamWrapper(stream, encoder, ownsEncoder);
                    var asyncStream = new AsyncVideoStreamWrapper(encodingStream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new audio stream.</summary>
        /// <param name="channelCount">Number of channels.</param>
        /// <param name="samplesPerSecond">Sample rate.</param>
        /// <param name="bitsPerSample">Bits per sample (per single channel).</param>
        /// <returns>Newly added audio stream.</returns>
        /// <remarks>
        /// Stream is initialized to be ready for uncompressed audio (PCM) with specified parameters.
        /// However, properties (such as <see cref="IAviAudioStream.Format"/>) can be changed later if the stream is
        /// to be fed with pre-compressed data.
        /// </remarks>
        public IAviAudioStream AddAudioStream(WaveFormat wf)
        {
            return AddStream<IAviAudioStreamInternal>(index =>
                {
                    var stream = new AviAudioStream(index, this, wf);
                    var asyncStream = new AsyncAudioStreamWrapper(stream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new encoding audio stream.</summary>
        /// <param name="encoder">Encoder to be used.</param>
        /// <param name="ownsEncoder">Whether encoder should be disposed with the writer.</param>
        /// <returns>Newly added audio stream.</returns>
        /// <remarks>
        /// <para>
        /// Stream is initialized to be to be encoded with the specified encoder.
        /// Method <see cref="IAviAudioStream.WriteBlock"/> expects data in the same format as encoder (see encoder's docs). 
        /// The data is passed to the encoder and the encoded result is written to the stream.
        /// </para>
        /// <para>
        /// The encoder defines the following properties of the stream:
        /// <see cref="IAviAudioStream.ChannelCount"/>, <see cref="IAviAudioStream.SamplesPerSecond"/>,
        /// <see cref="IAviAudioStream.BitsPerSample"/>, <see cref="IAviAudioStream.BytesPerSecond"/>,
        /// <see cref="IAviAudioStream.Granularity"/>, <see cref="IAviAudioStream.Format"/>,
        /// <see cref="IAviAudioStream.FormatSpecificData"/>.
        /// These properties cannot be modified.
        /// </para>
        /// </remarks>
        public IAviAudioStream AddEncodingAudioStream(IAudioEncoder encoder, bool ownsEncoder = true)
        {
            return AddStream<IAviAudioStreamInternal>(index =>
                {
                    var stream = new AviAudioStream(index, this, new WaveFormat(44100, 16, 1));
                    var encodingStream = new EncodingAudioStreamWrapper(stream, encoder, ownsEncoder);
                    return new AsyncAudioStreamWrapper(encodingStream);
                });
        }

        TStream AddStream<TStream>(Func<int, TStream> streamFactory)
            where TStream : IAviStreamInternal
        {
            lock (_syncWrite)
            {
                CheckNotClosed();
                CheckNotStartedWriting();

                var stream = streamFactory.Invoke(Streams.Count);

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
                if (!_isClosed)
                {
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

            _riffSizeTreshold = RIFF_AVI_SIZE_TRESHOLD;

            _currentRiff = _fileWriter.OpenList(RIFFListFourCCs.Avi, ListType_Riff);
            WriteHeader();
            _currentMovie = _fileWriter.OpenList(RIFFListFourCCs.Movie);
        }

        void CreateNewRiffIfNeeded(int approximateSizeOfNextChunk)
        {
            var estimatedSize = _fileWriter.BaseStream.Position + approximateSizeOfNextChunk - _currentRiff.ItemStart;
            if (_isFirstRiff && _emitIndex1) estimatedSize += RiffItem.ITEM_HEADER_SIZE + _index1Count * INDEX1_ENTRY_SIZE;
            if (estimatedSize > _riffSizeTreshold)
            {
                CloseCurrentRiff();

                _currentRiff = _fileWriter.OpenList(RIFFListFourCCs.AviExtended, ListType_Riff);
                _currentMovie = _fileWriter.OpenList(RIFFListFourCCs.Movie);
            }
        }

        void CloseCurrentRiff()
        {
            _fileWriter.CloseItem(_currentMovie);

            // Several special actions for the first RIFF (AVI)
            if (_isFirstRiff)
            {
                _riffAviFrameCount = _streams.OfType<IAviVideoStream>().Max(s => _streamsInfo[s.Index].FrameCount);
                if (_emitIndex1) WriteIndex1();
                _riffSizeTreshold = RIFF_AVIX_SIZE_TRESHOLD;
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

        void WriteStreamFrame(AviStreamBase stream, bool isKeyFrame, byte[] frameData, int startIndex, int count)
        {
            lock (_syncWrite)
            {
                CheckNotClosed();

                if (!_startedWriting)
                    PrepareForWriting();

                var si = _streamsInfo[stream.Index];
                if (si.SuperIndex.Count == MAX_SUPER_INDEX_ENTRIES)
                    throw new InvalidOperationException("Cannot write more frames to this stream.");

                if (ShouldFlushStreamIndex(si.StandardIndex))
                    FlushStreamIndex(stream);

                var shouldCreateIndex1Entry = _emitIndex1 && _isFirstRiff;

                CreateNewRiffIfNeeded(count + (shouldCreateIndex1Entry ? INDEX1_ENTRY_SIZE : 0));

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

                if (shouldCreateIndex1Entry)
                {
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
        }

        void IAviStreamWriteHandler.WriteStreamHeader(AviVideoStream videoStream)
        {
            // See AVISTREAMHEADER structure
            _fileWriter.Write((uint)videoStream.StreamType);
            _fileWriter.Write((uint)videoStream.Codec);
            _fileWriter.Write(0U); // StreamHeaderFlags
            _fileWriter.Write((ushort)0); // priority
            _fileWriter.Write((ushort)0); // language
            _fileWriter.Write(0U); // initial frames
            _fileWriter.Write(_frameRateDenominator); // scale (frame rate denominator)
            _fileWriter.Write(_frameRateNumerator); // rate (frame rate numerator)
            _fileWriter.Write(0U); // start
            _fileWriter.Write((uint)_streamsInfo[videoStream.Index].FrameCount); // length
            _fileWriter.Write((uint)_streamsInfo[videoStream.Index].MaxChunkDataSize); // suggested buffer size
            _fileWriter.Write(0U); // quality
            _fileWriter.Write(0U); // sample size
            _fileWriter.Write((short)0); // rectangle left
            _fileWriter.Write((short)0); // rectangle top
            var right = (short)videoStream.Width;
            var bottom = (short)videoStream.Height;
            _fileWriter.Write(right); // rectangle right
            _fileWriter.Write(bottom); // rectangle bottom
        }

        void IAviStreamWriteHandler.WriteStreamHeader(AviAudioStream audioStream)
        {
            var wf = audioStream.WaveFormat;

            // See AVISTREAMHEADER structure
            _fileWriter.Write((uint)audioStream.StreamType);
            _fileWriter.Write(0U); // no codec
            _fileWriter.Write(0U); // StreamHeaderFlags
            _fileWriter.Write((ushort)0); // priority
            _fileWriter.Write((ushort)0); // language
            _fileWriter.Write(0U); // initial frames
            _fileWriter.Write((uint)wf.BlockAlign); // scale (sample rate denominator)
            _fileWriter.Write((uint)wf.AverageBytesPerSecond); // rate (sample rate numerator)
            _fileWriter.Write(0U); // start
            _fileWriter.Write((uint)_streamsInfo[audioStream.Index].TotalDataSize); // length
            _fileWriter.Write((uint)(wf.AverageBytesPerSecond / 2)); // suggested buffer size (half-second)
            _fileWriter.Write(-1); // quality
            _fileWriter.Write(wf.BlockAlign); // sample size
            _fileWriter.SkipBytes(sizeof(short) * 4);
        }

        void IAviStreamWriteHandler.WriteStreamFormat(AviVideoStream videoStream)
        {
            // See BITMAPINFOHEADER structure
            _fileWriter.Write(40U); // size of structure
            _fileWriter.Write(videoStream.Width);
            _fileWriter.Write(videoStream.Height);
            _fileWriter.Write((short)1); // planes
            _fileWriter.Write((ushort)videoStream.BitsPerPixel); // bits per pixel
            _fileWriter.Write((uint)videoStream.Codec); // compression (codec FOURCC)
            var sizeInBytes = videoStream.Width * videoStream.Height * ((int)videoStream.BitsPerPixel / 8);
            _fileWriter.Write((uint)sizeInBytes); // image size in bytes
            _fileWriter.Write(0); // X pixels per meter
            _fileWriter.Write(0); // Y pixels per meter

            // Writing grayscale palette for 8-bit uncompressed stream
            // Otherwise, no palette
            if (videoStream.BitsPerPixel == BitsPerPixel.Bpp8 && videoStream.Codec == AviCodec.Uncompressed.FourCC)
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

        void IAviStreamWriteHandler.WriteStreamFormat(AviAudioStream audioStream)
        {
            audioStream.WaveFormat.Serialize(_fileWriter);
        }
        #endregion

        #region Header
        void WriteHeader()
        {
            _header = _fileWriter.OpenList(RIFFListFourCCs.Header);
            WriteFileHeader();
            foreach (var stream in _streams) WriteStreamList(stream);
            WriteOdmlHeader();
            WriteJunkInsteadOfMissingSuperIndexEntries();
            _fileWriter.CloseItem(_header);
        }

        void WriteJunkInsteadOfMissingSuperIndexEntries()
        {
            var missingEntriesCount = _streamsInfo.Sum(si => MAX_SUPER_INDEX_ENTRIES - si.SuperIndex.Count);
            if (missingEntriesCount > 0)
            {
                var junkDataSize = missingEntriesCount * sizeof(uint) * 4 - RiffItem.ITEM_HEADER_SIZE;
                var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.Junk, junkDataSize);
                _fileWriter.SkipBytes(junkDataSize);
                _fileWriter.CloseItem(chunk);
            }
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
            var list = _fileWriter.OpenList(RIFFListFourCCs.OpenDml);
            var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.OpenDmlHeader);
            _fileWriter.Write(_streams.OfType<IAviVideoStream>().Max(s => _streamsInfo[s.Index].FrameCount)); // total frames in file
            _fileWriter.SkipBytes(61 * sizeof(uint)); // reserved
            _fileWriter.CloseItem(chunk);
            _fileWriter.CloseItem(list);
        }

        void WriteStreamList(IAviStreamInternal stream)
        {
            var list = _fileWriter.OpenList(RIFFListFourCCs.Stream);
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
            if (!string.IsNullOrEmpty(stream.Name))
            {
                var bytes = Encoding.ASCII.GetBytes(stream.Name);
                var chunk = _fileWriter.OpenChunk(RIFFChunksFourCCs.StreamName);
                _fileWriter.Write(bytes);
                _fileWriter.Write((byte)0);
                _fileWriter.CloseItem(chunk);
            }
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
                    if (offset < minOffset)
                    {
                        minOffset = offset;
                        minIndex = i;
                    }
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
            if (index.Count >= MAX_INDEX_ENTRIES)
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
