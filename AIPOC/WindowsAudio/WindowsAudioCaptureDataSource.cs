using AIPOC.Exceptions;
using AIPOC.DataSources;
using AIPOC.Models;
using AIPOC.Options;
using Serilog;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using NAudio.Wave;
using NAudio.Lame;

namespace AIPOC.WindowsAudio
{
    internal class WindowsAudioCaptureDataSource : DataSource
    {
        public override string Id => "WindowsAudioCaptureDataSource";
        public override string DataFormat => "audio_mp3";
        // public override string DataFormat => "audio_wav";

        private Timer? captureTimer;
        private readonly object lockObject = new object();
        private bool isCapturingRealAudio = false;
        private WaveInEvent? waveIn;
        private MemoryStream? currentRecording;
        private WaveFileWriter? waveFileWriter;
        
        // Continuous recording fields
        private readonly Queue<byte[]> audioBuffer = new Queue<byte[]>();
        private readonly List<byte> currentSegmentData = new List<byte>();
        private int bytesPerSecond;
        private int targetBytesPerSegment;
        private LameMP3FileWriter? continuousWriter;
        private MemoryStream? continuousStream;

        public int SampleRate { get; set; } = 44100;
        public int Channels { get; set; } = 1;
        public int CaptureIntervalMs { get; set; } = 2000; // 2 seconds
        public int BitsPerSample { get; set; } = 16;

        public override async Task InitializeAsync(DataSourceOptions options, ILogger logger)
        {
            await base.InitializeAsync(options, logger);

            if (options?.Settings != null)
            {
                // Optional settings - use defaults if not provided
                if (options.Settings.TryGetValue("SampleRate", out string? sampleRateStr) && 
                    int.TryParse(sampleRateStr, out int sampleRate))
                {
                    SampleRate = sampleRate;
                }

                if (options.Settings.TryGetValue("Channels", out string? channelsStr) && 
                    int.TryParse(channelsStr, out int channels))
                {
                    Channels = channels;
                }

                if (options.Settings.TryGetValue("CaptureIntervalMs", out string? intervalStr) && 
                    int.TryParse(intervalStr, out int interval))
                {
                    CaptureIntervalMs = interval;
                }

                if (options.Settings.TryGetValue("BitsPerSample", out string? bitsStr) && 
                    int.TryParse(bitsStr, out int bits))
                {
                    BitsPerSample = bits;
                }
            }

            // Check if we're on Windows and can access real audio
            isCapturingRealAudio = await CheckNAudioAccess();

            // Calculate bytes per second for buffering
            bytesPerSecond = SampleRate * Channels * (BitsPerSample / 8);
            targetBytesPerSegment = bytesPerSecond * (CaptureIntervalMs / 1000);

            Logger?.Information("NAudio Windows capture initialized - SampleRate: {SampleRate}, Channels: {Channels}, Interval: {Interval}ms, BitsPerSample: {BitsPerSample}, RealAudio: {RealAudio}, BytesPerSegment: {BytesPerSegment}", 
                SampleRate, Channels, CaptureIntervalMs, BitsPerSample, isCapturingRealAudio, targetBytesPerSegment);
        }

        public override async Task<bool> PrepareAsync(StartEventData eventData)
        {
            if (!await base.PrepareAsync(eventData)) return false;

            try
            {
                if (isCapturingRealAudio)
                {
                    await RequestMicrophonePermission();
                }
                
                NewData($"NAudio Windows audio capture prepared for {Name} (Real: {isCapturingRealAudio})");
                return true;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to prepare NAudio Windows audio capture");
                return false;
            }
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();

            try
            {
                if (isCapturingRealAudio)
                {
                    // Start continuous audio recording
                    await StartContinuousRecording();
                }
                
                // Start timer to process segments every interval
                captureTimer = new Timer(CaptureAudioSegment, null, CaptureIntervalMs, CaptureIntervalMs);
                
                Logger?.Information("NAudio Windows capture started ({Mode})", 
                    isCapturingRealAudio ? "Real microphone via NAudio" : "Mock mode");
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to start NAudio Windows audio capture");
            }
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();

            captureTimer?.Dispose();
            captureTimer = null;

            // Stop any active recording
            StopCurrentRecording();

            Logger?.Information("NAudio Windows audio capture stopped");
        }

        private Task<bool> CheckNAudioAccess()
        {
            // Check if we're on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Logger?.Information("Not on Windows - using mock audio");
                return Task.FromResult(false);
            }

            try
            {
                // Check if NAudio can access recording devices
                int deviceCount = WaveInEvent.DeviceCount;
                if (deviceCount > 0)
                {
                    Logger?.Information("NAudio found {DeviceCount} recording device(s) - real audio capture available", deviceCount);
                    
                    // Log available devices
                    for (int i = 0; i < deviceCount; i++)
                    {
                        var capabilities = WaveInEvent.GetCapabilities(i);
                        Logger?.Information("Audio Device {Index}: {Name} - {Channels} channels", i, capabilities.ProductName, capabilities.Channels);
                    }
                    
                    return Task.FromResult(true);
                }
                else
                {
                    Logger?.Information("NAudio found no recording devices - will use mock audio");
                    return Task.FromResult(false);
                }
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "Could not check NAudio recording devices - using mock audio");
                return Task.FromResult(false);
            }
        }

        private Task RequestMicrophonePermission()
        {
            Logger?.Information("NAudio microphone access check...");
            
            try
            {
                // Test creating a WaveInEvent to verify microphone access
                using var testWaveIn = new WaveInEvent();
                testWaveIn.WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels);
                
                Logger?.Information("NAudio microphone access confirmed");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "NAudio microphone access may be restricted");
                return Task.CompletedTask;
            }
        }

        private void StopCurrentRecording()
        {
            try
            {
                waveIn?.StopRecording();
                waveIn?.Dispose();
                waveIn = null;

                waveFileWriter?.Dispose();
                waveFileWriter = null;

                currentRecording?.Dispose();
                currentRecording = null;
                
                // Stop continuous recording
                continuousWriter?.Dispose();
                continuousWriter = null;
                
                continuousStream?.Dispose();
                continuousStream = null;
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "Error stopping current recording");
            }
        }

        private async Task StartContinuousRecording()
        {
            try
            {
                // Initialize continuous recording stream
                continuousStream = new MemoryStream();
                
                // Set up NAudio WaveInEvent for continuous recording
                waveIn = new WaveInEvent()
                {
                    WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels)
                };

                // Set up continuous MP3 writer
                continuousWriter = new LameMP3FileWriter(continuousStream, waveIn.WaveFormat, 128);

                waveIn.DataAvailable += OnAudioDataAvailable;
                
                waveIn.RecordingStopped += (sender, e) =>
                {
                    Logger?.Information("Continuous recording stopped");
                };

                // Start continuous recording
                waveIn.StartRecording();
                
                Logger?.Information("Started continuous audio recording");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to start continuous recording");
                throw;
            }
        }

        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                lock (lockObject)
                {
                    // Write to continuous MP3 stream
                    continuousWriter?.Write(e.Buffer, 0, e.BytesRecorded);
                    
                    // Also buffer the raw data for segment processing
                    byte[] buffer = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, buffer, e.BytesRecorded);
                    audioBuffer.Enqueue(buffer);
                    
                    // Keep buffer size manageable (max 10 seconds of data)
                    int maxBufferSize = bytesPerSecond * 10; // 10 seconds
                    int currentBufferSize = audioBuffer.Sum(b => b.Length);
                    
                    while (currentBufferSize > maxBufferSize && audioBuffer.Count > 0)
                    {
                        var oldBuffer = audioBuffer.Dequeue();
                        currentBufferSize -= oldBuffer.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "Error processing audio data");
            }
        }

        private void CaptureAudioSegment(object? state)
        {
            if (!IsRunning) return;

            lock (lockObject)
            {
                try
                {
                    byte[] audioData;
                    
                    if (isCapturingRealAudio)
                    {
                        audioData = ProcessBufferedAudioData();
                    }
                    else
                    {
                        audioData = GenerateMockAudioData();
                    }

                    if (audioData.Length > 0)
                    {
                        // Create audio event data
                        var audioEventData = new NewDataEventData
                        {
                            Data = audioData,
                            Format = DataFormat,
                            Name = Name,
                            Timestamp = DateTime.Now,
                            Type = Id
                        };

                        NewData(audioData, audioEventData);
                        
                        Logger?.Information("Captured NAudio segment: {Size} bytes ({Mode})", 
                            audioData.Length, isCapturingRealAudio ? "Real" : "Mock");
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Error capturing NAudio segment");
                }
            }
        }

        private byte[] ProcessBufferedAudioData()
        {
            try
            {
                // Collect enough buffered data for one segment
                currentSegmentData.Clear();
                int totalBytesNeeded = targetBytesPerSegment;
                int bytesCollected = 0;

                while (audioBuffer.Count > 0 && bytesCollected < totalBytesNeeded)
                {
                    var buffer = audioBuffer.Dequeue();
                    currentSegmentData.AddRange(buffer);
                    bytesCollected += buffer.Length;
                }

                if (bytesCollected == 0)
                {
                    Logger?.Warning("No audio data available in buffer");
                    return Array.Empty<byte>();
                }

                // Trim to exact segment size if we have too much data
                if (currentSegmentData.Count > totalBytesNeeded)
                {
                    var excess = currentSegmentData.GetRange(totalBytesNeeded, currentSegmentData.Count - totalBytesNeeded);
                    currentSegmentData.RemoveRange(totalBytesNeeded, currentSegmentData.Count - totalBytesNeeded);
                    
                    // Put excess back in buffer for next segment (enqueue at front)
                    var tempQueue = new Queue<byte[]>();
                    tempQueue.Enqueue(excess.ToArray());
                    while (audioBuffer.Count > 0)
                    {
                        tempQueue.Enqueue(audioBuffer.Dequeue());
                    }
                    while (tempQueue.Count > 0)
                    {
                        audioBuffer.Enqueue(tempQueue.Dequeue());
                    }
                }

                // Convert PCM data to MP3
                byte[] mp3Data = ConvertPcmToMp3(currentSegmentData.ToArray());
                
                Logger?.Information("Processed buffered audio: {PcmBytes} PCM bytes -> {Mp3Bytes} MP3 bytes", 
                    currentSegmentData.Count, mp3Data.Length);
                
                return mp3Data;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error processing buffered audio data");
                return Array.Empty<byte>();
            }
        }

        private byte[] ConvertPcmToMp3(byte[] pcmData)
        {
            try
            {
                using var pcmStream = new MemoryStream(pcmData);
                using var mp3Stream = new MemoryStream();
                
                var waveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels);
                using var mp3Writer = new LameMP3FileWriter(mp3Stream, waveFormat, 128);
                
                mp3Writer.Write(pcmData, 0, pcmData.Length);
                mp3Writer.Flush();
                
                return mp3Stream.ToArray();
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error converting PCM to MP3");
                return Array.Empty<byte>();
            }
        }

        // private byte[] CaptureNAudioRealAudio()
        // {
        //     try
        //     {
        //         // Use NAudio to capture audio directly
        //         var audioData = new List<byte>();
        //         var recordingComplete = new ManualResetEventSlim(false);
                
        //         // Create memory stream to capture audio
        //         using var memoryStream = new MemoryStream();
                
        //         // Set up NAudio WaveInEvent
        //         using var waveIn = new WaveInEvent()
        //         {
        //             WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels)
        //         };

        //         // Set up wave file writer to memory stream
        //         using var writer = new WaveFileWriter(memoryStream, waveIn.WaveFormat);
                
        //         waveIn.DataAvailable += (sender, e) =>
        //         {
        //             writer.Write(e.Buffer, 0, e.BytesRecorded);
        //         };
                
        //         waveIn.RecordingStopped += (sender, e) =>
        //         {
        //             recordingComplete.Set();
        //         };

        //         // Start recording
        //         waveIn.StartRecording();
                
        //         // Record for the specified interval
        //         Thread.Sleep(CaptureIntervalMs);
                
        //         // Stop recording
        //         waveIn.StopRecording();
                
        //         // Wait for recording to complete
        //         recordingComplete.Wait(1000);
                
        //         // Get the audio data
        //         writer.Flush();
        //         audioData.AddRange(memoryStream.ToArray());
                
        //         Logger?.Information("Successfully captured NAudio real audio: {Size} bytes", audioData.Count);
        //         return audioData.ToArray();
        //     }
        //     catch (Exception ex)
        //     {
        //         Logger?.Error(ex, "Error during NAudio real audio capture, falling back to mock");
        //         return GenerateMockAudioData();
        //     }
        // }

        private byte[] GenerateMockAudioData()
        {
            // Generate mock WAV data for NAudio testing
            
            int sampleDuration = CaptureIntervalMs / 1000; // Duration in seconds
            int samplesPerSecond = SampleRate;
            int totalSamples = samplesPerSecond * sampleDuration;
            int bytesPerSample = BitsPerSample / 8; // Convert bits to bytes
            int dataSize = totalSamples * bytesPerSample * Channels;
            
            // WAV header (44 bytes)
            var wavHeader = new List<byte>();
            
            // RIFF header
            wavHeader.AddRange(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            wavHeader.AddRange(BitConverter.GetBytes(36 + dataSize)); // File size - 8
            wavHeader.AddRange(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            
            // Format chunk
            wavHeader.AddRange(System.Text.Encoding.ASCII.GetBytes("fmt "));
            wavHeader.AddRange(BitConverter.GetBytes(16)); // PCM chunk size
            wavHeader.AddRange(BitConverter.GetBytes((short)1)); // PCM format
            wavHeader.AddRange(BitConverter.GetBytes((short)Channels)); // Channels
            wavHeader.AddRange(BitConverter.GetBytes(SampleRate)); // Sample rate
            wavHeader.AddRange(BitConverter.GetBytes(SampleRate * Channels * bytesPerSample)); // Byte rate
            wavHeader.AddRange(BitConverter.GetBytes((short)(Channels * bytesPerSample))); // Block align
            wavHeader.AddRange(BitConverter.GetBytes((short)BitsPerSample)); // Bits per sample
            
            // Data chunk
            wavHeader.AddRange(System.Text.Encoding.ASCII.GetBytes("data"));
            wavHeader.AddRange(BitConverter.GetBytes(dataSize));
            
            // Generate NAudio-optimized test audio pattern
            var audioData = new List<byte>(wavHeader);
            
            for (int i = 0; i < totalSamples; i++)
            {
                double time = (double)i / samplesPerSecond;
                double sample = 0;
                
                // Create NAudio notification-like sound pattern
                if (time < 0.3)
                {
                    // NAudio test tone: 1000Hz
                    sample = 0.7 * Math.Sin(2.0 * Math.PI * 1000.0 * time);
                }
                else if (time < 0.6)
                {
                    // Second tone: 800Hz
                    sample = 0.7 * Math.Sin(2.0 * Math.PI * 800.0 * time);
                }
                else if (time < 1.0)
                {
                    // Third tone: 600Hz
                    sample = 0.7 * Math.Sin(2.0 * Math.PI * 600.0 * time);
                }
                else
                {
                    // Rest: gentle 440Hz (A note)
                    sample = 0.5 * Math.Sin(2.0 * Math.PI * 440.0 * time);
                }
                
                // Convert to proper bit depth
                if (BitsPerSample == 16)
                {
                    short sampleValue = (short)(sample * short.MaxValue);
                    audioData.AddRange(BitConverter.GetBytes(sampleValue));
                }
                else if (BitsPerSample == 8)
                {
                    byte sampleValue = (byte)((sample + 1.0) * 127.5);
                    audioData.Add(sampleValue);
                }
                else // 32-bit
                {
                    int sampleValue = (int)(sample * int.MaxValue);
                    audioData.AddRange(BitConverter.GetBytes(sampleValue));
                }
            }
            
            Logger?.Information("Generated NAudio mock audio: {SampleRate}Hz, {Channels}ch, {BitsPerSample}-bit over {Duration}s", 
                SampleRate, Channels, BitsPerSample, sampleDuration);
            
            return audioData.ToArray();
        }
    }
}