# AudioCapture

A cross-platform .NET C# console application for real-time audio capture with configurable intervals and event-driven architecture.

## Features

- **Cross-Platform Audio Capture**: Supports macOS (via SoX) and Windows (via NAudio)
- **Continuous Recording**: Non-blocking continuous audio capture with buffering
- **Configurable Capture Interval**: Default 2-second intervals, fully customizable
- **Event-Driven Architecture**: Real-time callbacks when new audio data is available
- **Multiple Audio Formats**: MP3 and WAV output with configurable sample rates and channels
- **Platform-Specific Storage**: Organized file storage per operating system
- **Mock Audio Support**: Fallback mock audio generation for testing
- **High-Performance**: Eliminates Thread.Sleep blocking for precise timing

## Architecture

The project follows a modular data source pattern with continuous recording:

- `DataSource` - Abstract base class for all data sources
- `AudioCaptureDataSource` - macOS audio capture implementation
- `WindowsAudioCaptureDataSource` - Windows NAudio-based continuous capture implementation  
- `TestDataSource` - Mock data source for testing

### Windows Audio Capture Architecture

The Windows implementation uses a **continuous recording architecture** that eliminates blocking issues:

1. **Continuous WaveInEvent**: Single `WaveInEvent` instance runs continuously throughout the session
2. **Event-Driven Buffering**: Audio data is automatically buffered as it arrives via `DataAvailable` events
3. **Non-Blocking Timer**: Timer processes buffered data without blocking, ensuring precise intervals
4. **Smart Buffer Management**: Automatic buffer size management prevents memory issues
5. **Efficient MP3 Encoding**: Real-time PCM to MP3 conversion using LAME encoder

#### Performance Benefits:
- **Precise Timing**: No Thread.Sleep blocking - consistent 2-second intervals
- **No Audio Gaps**: Continuous recording eliminates gaps between segments  
- **Resource Efficient**: Reuses audio objects instead of recreating them
- **High Throughput**: Processes 4-5 segments in 10 seconds vs 2 with old approach

## Audio Capture Configuration

### Default Settings
- **Capture Interval**: 2000ms (2 seconds)
- **Sample Rate**: 16000 Hz (Windows) / 44100 Hz (macOS)
- **Channels**: 1 (mono)
- **Bit Depth**: 16-bit
- **Format**: MP3 (Windows) / WAV (macOS)

### Windows-Specific Settings
- **MP3 Bitrate**: 128 kbps
- **Buffer Size**: Calculated automatically (64,000 bytes for 2-second segments at 16kHz)
- **Continuous Recording**: Always enabled for optimal performance

### Configuring Capture Interval

The audio capture interval is configurable through the `CaptureIntervalMs` setting:

```csharp
var settings = new Dictionary<string, string>
{
    { "SampleRate", "16000" },           // 16kHz for Windows
    { "Channels", "1" },                 // Mono for efficiency  
    { "CaptureIntervalMs", "2000" }      // Capture every 2 seconds
};
```

**Examples:**
- `"1000"` - Capture every 1 second
- `"5000"` - Capture every 5 seconds  
- `"500"` - Capture every 0.5 seconds

**Note**: Windows implementation uses continuous recording, so intervals represent when segments are processed from the buffer, not when recording starts/stops.

## Platform-Specific Behavior

### Windows
- Uses **NAudio** with **LAME MP3 encoder** for real audio capture
- **Continuous Recording**: Single WaveInEvent runs throughout the session
- **Event-Driven Buffering**: Audio data buffered automatically via DataAvailable events
- **Non-Blocking Processing**: Timer processes segments without blocking for precise timing
- Real microphone access when NAudio devices are available
- Falls back to mock audio if capture fails
- Audio files saved to: `Windows-Audio/` folder
- **Output Format**: MP3 at 128 kbps

### macOS
- Uses **SoX** (Sound eXchange) for real audio capture
- Requires SoX installation: `brew install sox`
- Real microphone access when SoX is available
- Falls back to mock audio if SoX is not installed
- Audio files saved to: `~/Documents/AudioCapture/`
- **Output Format**: WAV

### Other Platforms
- Mock audio generation for testing and development
- Simulated audio data with configurable intervals

## Usage

### Running the Application

```bash
dotnet run
```

The application automatically detects your operating system and runs the appropriate demo.

### Example Output

```
NAudio Windows capture initialized - SampleRate: 16000, Channels: 1, Interval: 2000ms, BytesPerSegment: 64000, RealAudio: True
Started continuous audio recording
NAudio Windows capture started (Real microphone via NAudio)
Windows audio capture running for 10 seconds...
[10:07:38] Processed buffered audio: 64000 PCM bytes -> 33984 MP3 bytes
[10:07:38] Windows Audio captured at 09/22/2025 10:07:38: 33984 bytes
Saved Windows audio segment to Windows-Audio\windows_audio_capture_20250922_100738_381.mp3 (33984 bytes)
[10:07:40] Processed buffered audio: 64000 PCM bytes -> 33984 MP3 bytes
[10:07:40] Windows Audio captured at 09/22/2025 10:07:40: 33984 bytes
Saved Windows audio segment to Windows-Audio\windows_audio_capture_20250922_100740_393.mp3 (33984 bytes)
```

## Event Handling

The audio capture uses an event-driven pattern:

```csharp
audioSource.OnDataReceived += (sender, data) =>
{
    Console.WriteLine($"Audio data received: {data.Length} bytes");
    // Process audio data here
};
```

## File Structure

```
AIPOC/
├── Program.cs                          # Cross-platform entry point
├── DataSource.cs                       # Abstract base class
├── AudioCaptureDataSource.cs           # macOS implementation
├── AudioCaptureDemo.cs                 # macOS demo
├── TestDataSource.cs                   # Test/mock implementation
├── Constants.cs                        # Application constants
├── WindowsAudio/
│   ├── WindowsAudioCaptureDataSource.cs # Windows implementation
│   └── WindowsAudioDemo.cs             # Windows demo
└── Exceptions/
    ├── SettingNotFoundException.cs      # Custom exceptions
    └── SettingWrongTypeException.cs
```

## Dependencies

- **.NET 6.0+**
- **Serilog** - Structured logging
- **NAudio** (Windows) - Audio capture and processing
- **NAudio.Lame** (Windows) - MP3 encoding
- **SoX** (macOS only) - `brew install sox`

## Installation

1. Clone the repository:
```bash
git clone https://github.com/tduangbo/AudioCapture.git
cd AudioCapture/AIPOC
```

2. Install dependencies:
```bash
dotnet restore
```

3. Install platform-specific tools:
   - **macOS**: `brew install sox`
   - **Windows**: NAudio and NAudio.Lame (included via NuGet)

4. Run the application:
```bash
dotnet run
```

## Customization

### Changing Audio Settings

Modify the settings dictionary in the demo files:

```csharp
var settings = new Dictionary<string, string>
{
    { "SampleRate", "48000" },           // 48kHz sample rate
    { "Channels", "1" },                 // Mono audio
    { "CaptureIntervalMs", "1000" },     // 1-second intervals
    { "RealAudio", "true" }              // Enable real audio capture
};
```

### Adding Custom Data Sources

Inherit from `DataSource` and implement the required methods:

```csharp
public class MyCustomDataSource : DataSource
{
    protected override async Task StartDataGeneration(CancellationToken cancellationToken)
    {
        // Custom implementation
    }
}
```

## Troubleshooting

### Windows Issues  
- **NAudio not found**: Ensure NAudio and NAudio.Lame packages are installed
- **No audio device**: Check default recording device in Sound settings
- **MP3 encoding errors**: Verify LAME encoder is properly installed with NAudio.Lame

### macOS Issues
- **SoX not found**: Install with `brew install sox`
- **Permission denied**: Grant microphone access in System Preferences

### General Issues
- **No audio files created**: Check folder permissions and disk space
- **Mock audio only**: Verify real audio dependencies are installed

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.