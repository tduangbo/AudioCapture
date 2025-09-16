using AIPOC.DataSources;
using AIPOC.Models;
using AIPOC.Options;
using Serilog;

namespace AIPOC
{
    public class AudioCaptureDemo
    {
        public static async Task RunDemo()
        {
            // Configure Serilog logger
            var logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            // Create audio capture data source
            var audioDataSource = new AudioCaptureDataSource();

            // Set up event callback to handle captured audio
            audioDataSource.OnNewData = (audioEventData) =>
            {
                logger.Information("Audio captured at {Timestamp}: {Size} bytes", 
                    audioEventData.Timestamp, 
                    ((byte[])audioEventData.Data).Length);
                
                // Here you can process the audio data
                // For example: save to file, send to speech recognition, etc.
                ProcessAudioData((byte[])audioEventData.Data, audioEventData);
            };

            // Configure options
            var options = new DataSourceOptions
            {
                Type = "AudioCapture",
                Name = "Demo Audio Capture",
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
                // Initialize the audio capture
                await audioDataSource.InitializeAsync(options, logger);

                // Prepare for capturing
                var eventData = new StartEventData
                {
                    ConfirmationNumber = "DEMO-001",
                    Mode = "All"
                };

                if (await audioDataSource.PrepareAsync(eventData))
                {
                    logger.Information("Starting audio capture demo...");
                    
                    // Start capturing
                    await audioDataSource.StartAsync();

                    // Let it run for 10 seconds
                    logger.Information("Audio capture running for 10 seconds...");
                    await Task.Delay(10000);

                    // Stop capturing
                    logger.Information("Stopping audio capture...");
                    await audioDataSource.StopAsync();
                }
                else
                {
                    logger.Error("Failed to prepare audio capture");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during audio capture demo");
            }
        }

        private static void ProcessAudioData(byte[] audioData, NewDataEventData eventData)
        {
            // Create DEMO-001 folder if it doesn't exist
            string folderName = "DEMO-001";
            if (!Directory.Exists(folderName))
            {
                Directory.CreateDirectory(folderName);
                Console.WriteLine($"Created folder: {folderName}");
            }

            // Create filename with timestamp
            string fileName = $"audio_capture_{eventData.Timestamp:yyyyMMdd_HHmmss_fff}.wav";
            string fullPath = Path.Combine(folderName, fileName);
            
            try
            {
                File.WriteAllBytes(fullPath, audioData);
                Console.WriteLine($"Saved audio segment to {fullPath} ({audioData.Length} bytes)");
                
                // You could also:
                // - Send to speech-to-text service
                // - Analyze audio for specific patterns
                // - Stream to real-time processing pipeline
                // - Store in database with metadata
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving audio file: {ex.Message}");
            }
        }
    }
}