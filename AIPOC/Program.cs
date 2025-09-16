using AIPOC;
using AIPOC.WindowsAudio;
using System.Runtime.InteropServices;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("AIPOC - Cross-Platform Audio Capture Demo");

// Detect platform and run appropriate demo
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    Console.WriteLine("Running on Windows - Starting Windows Audio Capture Demo");
    await WindowsAudioDemo.RunDemo();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    Console.WriteLine("Running on macOS - Starting macOS Audio Capture Demo");
    await AudioCaptureDemo.RunDemo();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    Console.WriteLine("Running on Linux - Starting Cross-Platform Demo");
    await AudioCaptureDemo.RunDemo();
}
else
{
    Console.WriteLine("Unknown platform - Starting Mock Audio Demo");
    await AudioCaptureDemo.RunDemo();
}

Console.WriteLine("Demo completed. Press any key to exit...");
Console.ReadKey();

// for example usage

// var audioDataSource = new AudioCaptureDataSource();

// // Set up event callback
// audioDataSource.OnNewData = (audioEventData) => {
//     byte[] audioData = (byte[])audioEventData.Data;
//     // Process your audio data here
//     Console.WriteLine($"Captured {audioData.Length} bytes at {audioEventData.Timestamp}");
// };

// // Configure and start
// await audioDataSource.InitializeAsync(options, logger);
// await audioDataSource.PrepareAsync(eventData);
// await audioDataSource.StartAsync();