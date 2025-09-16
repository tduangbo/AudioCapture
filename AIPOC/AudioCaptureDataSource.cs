using AIPOC.Exceptions;
using AIPOC.DataSources;
using AIPOC.Models;
using AIPOC.Options;
using Serilog;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace AIPOC.DataSources
{
    internal class AudioCaptureDataSource : DataSource
    {
        public override string Id => "AudioCaptureDataSource";
        public override string DataFormat => "audio_wav";

        private Timer? captureTimer;
        private readonly object lockObject = new object();
        private bool isCapturingRealAudio = false;

        public int SampleRate { get; set; } = 44100;
        public int Channels { get; set; } = 1;
        public int CaptureIntervalMs { get; set; } = 2000; // 2 seconds

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
            }

            // Check if we're on macOS and can access real audio
            isCapturingRealAudio = await CheckMicrophoneAccess();

            Logger?.Information("Audio capture initialized - SampleRate: {SampleRate}, Channels: {Channels}, Interval: {Interval}ms, RealAudio: {RealAudio}", 
                SampleRate, Channels, CaptureIntervalMs, isCapturingRealAudio);
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
                
                NewData($"Audio capture prepared for {Name} (Real: {isCapturingRealAudio})");
                return true;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to prepare audio capture");
                return false;
            }
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();

            try
            {
                // Start timer to capture audio every 2 seconds
                captureTimer = new Timer(CaptureAudioSegment, null, CaptureIntervalMs, CaptureIntervalMs);
                
                Logger?.Information("Audio capture started ({Mode})", 
                    isCapturingRealAudio ? "Real microphone" : "Mock mode");
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to start audio capture");
            }
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();

            captureTimer?.Dispose();
            captureTimer = null;

            Logger?.Information("Audio capture stopped");
        }

        private async Task<bool> CheckMicrophoneAccess()
        {
            // Check if we're on macOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Logger?.Information("Not on macOS - using mock audio");
                return false;
            }

            try
            {
                // Check if SoX is available for audio capture
                var soxCheck = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "sox",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(soxCheck);
                await process!.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Logger?.Information("SoX found - real audio capture available");
                    return true;
                }
                else
                {
                    Logger?.Information("SoX not found - will use mock audio. Install with: brew install sox");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "Could not check for SoX - using mock audio");
                return false;
            }
        }

        private async Task RequestMicrophonePermission()
        {
            Logger?.Information("Requesting microphone permission...");
            
            // On macOS, the first audio access will automatically prompt for permission
            // We'll test with a quick 0.1 second recording
            try
            {
                var testProcess = new ProcessStartInfo
                {
                    FileName = "sox",
                    Arguments = "-t coreaudio default -t wav /dev/null trim 0 0.1",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(testProcess);
                await process!.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    Logger?.Information("Microphone access granted");
                }
                else
                {
                    Logger?.Warning("Microphone access may be denied. Check System Preferences > Security & Privacy > Privacy > Microphone");
                }
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "Could not test microphone access");
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
                        audioData = CaptureRealAudio();
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
                        
                        Logger?.Information("Captured audio segment: {Size} bytes ({Mode})", 
                            audioData.Length, isCapturingRealAudio ? "Real" : "Mock");
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Error capturing audio segment");
                }
            }
        }

        private byte[] CaptureRealAudio()
        {
            try
            {
                // Create temporary file for recording
                string tempFile = Path.GetTempFileName() + ".wav";
                
                // Use SoX to capture audio from default microphone
                var captureProcess = new ProcessStartInfo
                {
                    FileName = "sox",
                    Arguments = $"-t coreaudio default -r {SampleRate} -c {Channels} -b 16 \"{tempFile}\" trim 0 {CaptureIntervalMs / 1000.0}",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(captureProcess);
                process!.WaitForExit(CaptureIntervalMs + 1000); // Wait for capture + 1 second buffer

                if (process.ExitCode == 0 && File.Exists(tempFile))
                {
                    // Read the captured audio file
                    byte[] audioData = File.ReadAllBytes(tempFile);
                    
                    // Clean up temp file
                    try { File.Delete(tempFile); } catch { }
                    
                    Logger?.Information("Successfully captured real audio: {Size} bytes", audioData.Length);
                    return audioData;
                }
                else
                {
                    Logger?.Warning("Real audio capture failed, falling back to mock audio");
                    // Clean up temp file
                    try { File.Delete(tempFile); } catch { }
                    return GenerateMockAudioData();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error during real audio capture, falling back to mock");
                return GenerateMockAudioData();
            }
        }

        private byte[] GenerateMockAudioData()
        {
            // Generate mock WAV data with a LOUD, CLEAR tone for testing
            
            int sampleDuration = CaptureIntervalMs / 1000; // Duration in seconds
            int samplesPerSecond = SampleRate;
            int totalSamples = samplesPerSecond * sampleDuration;
            int bytesPerSample = 2; // 16-bit audio
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
            wavHeader.AddRange(BitConverter.GetBytes((short)(bytesPerSample * 8))); // Bits per sample
            
            // Data chunk
            wavHeader.AddRange(System.Text.Encoding.ASCII.GetBytes("data"));
            wavHeader.AddRange(BitConverter.GetBytes(dataSize));
            
            // Generate LOUD, CLEAR audio that you should definitely hear
            var audioData = new List<byte>(wavHeader);
            
            for (int i = 0; i < totalSamples; i++)
            {
                double time = (double)i / samplesPerSecond;
                double sample = 0;
                
                // Create a loud, alternating tone pattern that's easy to hear
                if (time < 0.5)
                {
                    // First half second: 440Hz (A note) - LOUD
                    sample = 0.7 * Math.Sin(2.0 * Math.PI * 440.0 * time);
                }
                else if (time < 1.0)
                {
                    // Second half: 880Hz (A note one octave higher) - LOUD
                    sample = 0.7 * Math.Sin(2.0 * Math.PI * 880.0 * time);
                }
                else
                {
                    // Rest of duration: 660Hz (middle frequency) - LOUD
                    sample = 0.7 * Math.Sin(2.0 * Math.PI * 660.0 * time);
                }
                
                // Convert to 16-bit signed integer
                short sampleValue = (short)(sample * short.MaxValue);
                audioData.AddRange(BitConverter.GetBytes(sampleValue));
            }
            
            Logger?.Information("Generated mock test audio: 440Hz->880Hz->660Hz pattern over {Duration}s", sampleDuration);
            
            return audioData.ToArray();
        }
    }
}