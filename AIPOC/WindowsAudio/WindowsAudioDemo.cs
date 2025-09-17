using AIPOC.WindowsAudio;
using AIPOC.Models;
using AIPOC.Options;
using Serilog;

namespace AIPOC.WindowsAudio
{
    public class WindowsAudioDemo
    {
        public static async Task RunDemo()
        {
            // Configure Serilog logger
            var logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            // Create Windows audio capture data source
            var windowsAudioDataSource = new WindowsAudioCaptureDataSource();

            // Set up event callback to handle captured audio
            windowsAudioDataSource.OnNewData = (audioEventData) =>
            {
                logger.Information("Windows Audio captured at {Timestamp}: {Size} bytes", 
                    audioEventData.Timestamp, 
                    ((byte[])audioEventData.Data).Length);
                
                // Process the Windows audio data
                ProcessWindowsAudioData((byte[])audioEventData.Data, audioEventData);
            };

            // Configure options for Windows
            var options = new DataSourceOptions
            {
                Type = "WindowsAudioCapture",
                Name = "Windows Audio Capture Demo",
                Modes = new List<string> { "All" },
                Settings = new Dictionary<string, string>
                {
                    { "SampleRate", "44100" },
                    { "Channels", "1" },
                    { "CaptureIntervalMs", "2000" } // Capture every 2 seconds
                }
            };

            try
            {
                // Initialize the Windows audio capture
                await windowsAudioDataSource.InitializeAsync(options, logger);

                // Prepare for capturing
                var eventData = new StartEventData
                {
                    ConfirmationNumber = "WIN-DEMO-001",
                    Mode = "All"
                };

                if (await windowsAudioDataSource.PrepareAsync(eventData))
                {
                    logger.Information("Starting Windows audio capture demo...");
                    
                    // Start capturing
                    await windowsAudioDataSource.StartAsync();

                    // Let it run for 10 seconds
                    logger.Information("Windows audio capture running for 10 seconds...");
                    await Task.Delay(10000);

                    // Stop capturing
                    logger.Information("Stopping Windows audio capture...");
                    await windowsAudioDataSource.StopAsync();
                }
                else
                {
                    logger.Error("Failed to prepare Windows audio capture");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during Windows audio capture demo");
            }
        }

        private static void ProcessWindowsAudioData(byte[] audioData, NewDataEventData eventData)
        {
            // Create Windows-Audio folder if it doesn't exist
            string folderName = "Windows-Audio";
            if (!Directory.Exists(folderName))
            {
                Directory.CreateDirectory(folderName);
                Console.WriteLine($"Created Windows audio folder: {folderName}");
            }

            // Create filename with timestamp
            // string fileName = $"windows_audio_capture_{eventData.Timestamp:yyyyMMdd_HHmmss_fff}.wav";
            string fileName = $"windows_audio_capture_{eventData.Timestamp:yyyyMMdd_HHmmss_fff}.mp3";
            string fullPath = Path.Combine(folderName, fileName);
            
            try
            {
                File.WriteAllBytes(fullPath, audioData);
                Console.WriteLine($"Saved Windows audio segment to {fullPath} ({audioData.Length} bytes)");
                
                // Windows-specific processing options:
                // - Send to Windows Speech API
                // - Use Windows Media Foundation
                // - Integrate with Cortana/Windows Voice Assistant
                // - Process with Windows ML audio models
                // - Send to Azure Cognitive Services (Speech-to-Text)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving Windows audio file: {ex.Message}");
            }
        }
    }
}