using AIPOC;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("AIPOC - Audio Capture Demo");

// Run the audio capture demo
await AudioCaptureDemo.RunDemo();

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