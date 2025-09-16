# AudioCapture

A cross-platform .NET C# console application for real-time audio capture with configurable intervals and event-driven architecture.

## Features

- **Cross-Platform Audio Capture**: Supports macOS (via SoX) and Windows (via PowerShell/MCI)
- **Configurable Capture Interval**: Default 2-second intervals, fully customizable
- **Event-Driven Architecture**: Real-time callbacks when new audio data is available
- **Multiple Audio Formats**: WAV file output with configurable sample rates and channels
- **Platform-Specific Storage**: Organized file storage per operating system
- **Mock Audio Support**: Fallback mock audio generation for testing

## Architecture

The project follows a modular data source pattern:

- `DataSource` - Abstract base class for all data sources
- `AudioCaptureDataSource` - macOS audio capture implementation
- `WindowsAudioCaptureDataSource` - Windows audio capture implementation  
- `TestDataSource` - Mock data source for testing

## Audio Capture Configuration

### Default Settings
- **Capture Interval**: 2000ms (2 seconds)
- **Sample Rate**: 44100 Hz
- **Channels**: 2 (stereo)
- **Bit Depth**: 16-bit
- **Format**: WAV

### Configuring Capture Interval

The audio capture interval is configurable through the `CaptureIntervalMs` setting:

```csharp
var settings = new Dictionary<string, string>
{
    { "CaptureIntervalMs", "2000" } // Capture every 2 seconds
};
```

**Examples:**
- `"1000"` - Capture every 1 second
- `"5000"` - Capture every 5 seconds  
- `"500"` - Capture every 0.5 seconds

## Platform-Specific Behavior

### macOS
- Uses **SoX** (Sound eXchange) for real audio capture
- Requires SoX installation: `brew install sox`
- Real microphone access when SoX is available
- Falls back to mock audio if SoX is not installed
- Audio files saved to: `~/Documents/AudioCapture/`

### Windows
- Uses **PowerShell** with Media Control Interface (MCI)
- Built-in Windows audio capture capabilities
- Falls back to mock audio if capture fails
- Audio files saved to: `%USERPROFILE%\Documents\AudioCapture\`

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
Audio capture initialized - SampleRate: 44100, Channels: 2, Interval: 2000ms, RealAudio: True
Starting audio capture. Press any key to stop...
[12:34:56] Audio data received: 176400 bytes, saved to /Users/username/Documents/AudioCapture/audio_20240915_123456.wav
[12:34:58] Audio data received: 176400 bytes, saved to /Users/username/Documents/AudioCapture/audio_20240915_123458.wav
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
- **SoX** (macOS only) - `brew install sox`
- **PowerShell** (Windows) - Built-in

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
   - **Windows**: PowerShell (pre-installed)

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

### macOS Issues
- **SoX not found**: Install with `brew install sox`
- **Permission denied**: Grant microphone access in System Preferences

### Windows Issues  
- **PowerShell execution policy**: Run `Set-ExecutionPolicy RemoteSigned`
- **No audio device**: Check default recording device in Sound settings

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