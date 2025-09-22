using NAudio.Wave;
using System;

namespace AIPOC
{
    public static class NAudioTest
    {
        public static void TestNAudioAvailability()
        {
            try
            {
                Console.WriteLine("=== NAudio Availability Test ===");
                
                // Test device enumeration
                int deviceCount = WaveInEvent.DeviceCount;
                Console.WriteLine($"NAudio detected {deviceCount} recording device(s)");
                
                if (deviceCount > 0)
                {
                    // List available devices
                    for (int i = 0; i < deviceCount; i++)
                    {
                        var capabilities = WaveInEvent.GetCapabilities(i);
                        Console.WriteLine($"Device {i}: {capabilities.ProductName} - {capabilities.Channels} channels");
                    }
                    
                    // Test creating a WaveInEvent
                    using var waveIn = new WaveInEvent();
                    waveIn.WaveFormat = new WaveFormat(44100, 16, 1);
                    Console.WriteLine("✅ NAudio WaveInEvent created successfully");
                    Console.WriteLine($"✅ Wave format: {waveIn.WaveFormat.SampleRate}Hz, {waveIn.WaveFormat.BitsPerSample}-bit, {waveIn.WaveFormat.Channels} channel(s)");
                }
                else
                {
                    Console.WriteLine("⚠️  No recording devices found");
                }
                
                Console.WriteLine("✅ NAudio is available and functional!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ NAudio test failed: {ex.Message}");
            }
        }
    }
}