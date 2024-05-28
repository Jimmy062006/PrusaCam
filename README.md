# PrusaCam Image Capture and Upload

## Overview

This project captures images from a video stream, processes them, and uploads them to a specified server. It is designed to work with PrusaCam, utilizing the Nager.VideoStream library for video stream handling, and SixLabors.ImageSharp for image processing. The application is configured via a JSON file and supports both Windows and Linux operating systems.

## Features

- Captures images from a video stream URL.
- Processes images to fit a maximum size constraint.
- Periodically uploads processed images to a server.
- Configuration via `appsettings.json` file.
- Supports Windows and Linux.

## Requirements

- .NET 6.0 or later
- FFMPEG
- PrusaCam API token

## Configuration

The application requires a configuration file named `appsettings.json` in the root directory. If this file does not exist, the application will attempt to create one based on `default-appsettings.json`. The configuration file should contain the following settings:

```json
{
  "Token": "your-api-token",
  "BaseUrl": "http://your-server-url",
  "StreamURL": "http://your-stream-url",
  "Delay": 60,
  "FfmpegPathWindows": "path-to-ffmpeg-windows",
  "FfmpegPathLinux": "path-to-ffmpeg-linux"
}
```

## Usage

1. **Load Configuration:**
   Ensure `appsettings.json` exists in the root directory. If not, the application will guide you through creating one using default settings.

2. **Run the Application:**
   Execute the application. It will start capturing images from the specified stream URL and uploading them to the server at intervals defined by the `Delay` setting.

3. **Monitor Output:**
   The application will log messages to the console, indicating the status of image capture, processing, and upload operations.

## Key Components

### Main Program

- Initializes configuration and HTTP client.
- Sets up a timer for periodic uploads.
- Starts listening for events (keeps the application running).

### Configuration Handling

- Loads configuration from `appsettings.json`.
- If the configuration file is missing, prompts the user to create one based on defaults.

### Image Capture and Upload

- Uses `Nager.VideoStream` to capture images from the stream URL.
- Processes the captured image using `SixLabors.ImageSharp` to ensure it fits within a 10 MB size limit.
- Uploads the processed image to the server via PrusaCam API.

### Semaphore for Concurrent Access

- Ensures that only one upload operation occurs at a time using `SemaphoreSlim`.

## Error Handling

The application logs any errors encountered during configuration loading, image processing, or upload operations to the console.

## Example Console Output

```plaintext
Configuration loaded successfully.
First Upload will run at 2024-05-28T12:00:00Z
Processing new image at 2024-05-28T12:00:00Z
Uploading 1048576 bytes
Image processed and uploaded at 2024-05-28T12:00:05Z. Next allowed upload at 2024-05-28T12:01:05Z
```

## Building and Running

To build and run the application, use the following commands:

```bash
dotnet build
dotnet run
```

Ensure FFMPEG is installed and accessible from the paths specified in the configuration.

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request.

## License

This project is licensed under the MIT License.

---

For more detailed information on the API usage and configuration, refer to the official [PrusaCam API documentation](https://example.com).
