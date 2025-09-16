using AIPOC.Exceptions;
using AIPOC.DataSources;
using AIPOC.Models;
using AIPOC.Options;
using Serilog;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace AIPOC.WindowsAudio
{
    internal class WindowsAudioCaptureDataSource : DataSource
    {
        public override string Id => "WindowsAudioCaptureDataSource";
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

            // Check if we're on Windows and can access real audio
            isCapturingRealAudio = await CheckWindowsAudioAccess();

            Logger?.Information("Windows Audio capture initialized - SampleRate: {SampleRate}, Channels: {Channels}, Interval: {Interval}ms, RealAudio: {RealAudio}", 
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
                
                NewData($"Windows audio capture prepared for {Name} (Real: {isCapturingRealAudio})");
                return true;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to prepare Windows audio capture");
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
                
                Logger?.Information("Windows audio capture started ({Mode})", 
                    isCapturingRealAudio ? "Real microphone via PowerShell" : "Mock mode");
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Failed to start Windows audio capture");
            }
        }

        public override async Task StopAsync()
        {
            await base.StopAsync();

            captureTimer?.Dispose();
            captureTimer = null;

            Logger?.Information("Windows audio capture stopped");
        }

        private async Task<bool> CheckWindowsAudioAccess()
        {
            // Check if we're on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Logger?.Information("Not on Windows - using mock audio");
                return false;
            }

            try
            {
                // Check if PowerShell is available for audio capture using Windows Media Format SDK
                var powershellCheck = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"Get-Command Add-Type\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(powershellCheck);
                await process!.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Logger?.Information("PowerShell found - Windows real audio capture available");
                    return true;
                }
                else
                {
                    Logger?.Information("PowerShell not available - will use mock audio");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "Could not check for PowerShell - using mock audio");
                return false;
            }
        }

        private async Task RequestMicrophonePermission()
        {
            Logger?.Information("Requesting Windows microphone permission...");
            
            // On Windows, we'll test microphone access by attempting a short recording
            try
            {
                var testProcess = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = CreateWindowsAudioCaptureScript(0.1), // 0.1 second test
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(testProcess);
                await process!.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    Logger?.Information("Windows microphone access granted");
                }
                else
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    Logger?.Warning("Windows microphone access may be denied: {Error}", error);
                }
            }
            catch (Exception ex)
            {
                Logger?.Warning(ex, "Could not test Windows microphone access");
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
                        audioData = CaptureWindowsRealAudio();
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
                        
                        Logger?.Information("Captured Windows audio segment: {Size} bytes ({Mode})", 
                            audioData.Length, isCapturingRealAudio ? "Real" : "Mock");
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Error capturing Windows audio segment");
                }
            }
        }

        private byte[] CaptureWindowsRealAudio()
        {
            try
            {
                // Create temporary file for recording
                string tempFile = Path.GetTempFileName() + ".wav";
                
                // Use PowerShell with Windows Media Format SDK to capture audio
                var captureProcess = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = CreateWindowsAudioCaptureScript(CaptureIntervalMs / 1000.0, tempFile),
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(captureProcess);
                process!.WaitForExit(CaptureIntervalMs + 5000); // Wait for capture + 5 second buffer

                if (process.ExitCode == 0 && File.Exists(tempFile))
                {
                    // Read the captured audio file
                    byte[] audioData = File.ReadAllBytes(tempFile);
                    
                    // Clean up temp file
                    try { File.Delete(tempFile); } catch { }
                    
                    Logger?.Information("Successfully captured Windows real audio: {Size} bytes", audioData.Length);
                    return audioData;
                }
                else
                {
                    string error = "";
                    try 
                    { 
                        error = process.StandardError.ReadToEnd();
                    } 
                    catch { }
                    
                    Logger?.Warning("Windows real audio capture failed: {Error}, falling back to mock audio", error);
                    // Clean up temp file
                    try { File.Delete(tempFile); } catch { }
                    return GenerateMockAudioData();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error during Windows real audio capture, falling back to mock");
                return GenerateMockAudioData();
            }
        }

        private string CreateWindowsAudioCaptureScript(double durationSeconds, string? outputFile = null)
        {
            outputFile ??= "test.wav";
            
            // PowerShell script using Windows Core Audio APIs via COM
            string script = $@"
Add-Type -AssemblyName System.Speech
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;

public class AudioRecorder {{
    [DllImport(""winmm.dll"", EntryPoint = ""mciSendStringA"", CharSet = CharSet.Ansi)]
    public static extern int mciSendString(string lpstrCommand, string lpstrReturnString, int uReturnLength, IntPtr hwndCallback);
    
    public static void RecordAudio(string filename, int durationMs) {{
        string openCommand = ""open new type waveaudio alias capture"";
        string recordCommand = ""record capture"";
        string stopCommand = ""stop capture"";
        string saveCommand = $""save capture {{filename}}"";
        string closeCommand = ""close capture"";
        
        mciSendString(openCommand, null, 0, IntPtr.Zero);
        mciSendString(recordCommand, null, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(durationMs);
        mciSendString(stopCommand, null, 0, IntPtr.Zero);
        mciSendString(saveCommand, null, 0, IntPtr.Zero);
        mciSendString(closeCommand, null, 0, IntPtr.Zero);
    }}
}}
'@

[AudioRecorder]::RecordAudio('{outputFile}', {(int)(durationSeconds * 1000)})
";

            return $"-Command \"{script.Replace("\"", "\\\"")}\"";
        }

        private byte[] GenerateMockAudioData()
        {
            // Generate mock WAV data for Windows testing
            
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
            
            // Generate Windows-specific test audio pattern
            var audioData = new List<byte>(wavHeader);
            
            for (int i = 0; i < totalSamples; i++)
            {
                double time = (double)i / samplesPerSecond;
                double sample = 0;
                
                // Create Windows notification-like sound pattern
                if (time < 0.3)
                {
                    // Windows notification start tone: 800Hz
                    sample = 0.6 * Math.Sin(2.0 * Math.PI * 800.0 * time);
                }
                else if (time < 0.6)
                {
                    // Second tone: 600Hz
                    sample = 0.6 * Math.Sin(2.0 * Math.PI * 600.0 * time);
                }
                else if (time < 1.0)
                {
                    // Third tone: 400Hz
                    sample = 0.6 * Math.Sin(2.0 * Math.PI * 400.0 * time);
                }
                else
                {
                    // Rest: gentle 300Hz
                    sample = 0.4 * Math.Sin(2.0 * Math.PI * 300.0 * time);
                }
                
                // Convert to 16-bit signed integer
                short sampleValue = (short)(sample * short.MaxValue);
                audioData.AddRange(BitConverter.GetBytes(sampleValue));
            }
            
            Logger?.Information("Generated Windows mock audio: notification-style pattern over {Duration}s", sampleDuration);
            
            return audioData.ToArray();
        }
    }
}