using Nager.VideoStream;
using PrusaCam;
using PrusaCameraAPI;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;
using System.Runtime.InteropServices;
using System.Timers;
using System.IO;
using System.Text.Json;

internal class Program
{
	private static readonly HttpClient httpClient = new();
	static DateTime lastRun = DateTime.MinValue;
	static openapiClient apiClient;
	private static readonly SemaphoreSlim semaphoreSlim = new(1, 1); // Semaphore to limit concurrent access
	private static readonly System.Timers.Timer uploadTimer = new System.Timers.Timer(); // Timer for controlling upload frequency
	private static bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	private static PrusaCam.Configuration config;

	static async Task Main(string[] args)
	{
		LoadConfiguration();
		if (config == null)
		{
			Console.WriteLine("Configuration loading failed. Exiting...");
			return;
		}

		httpClient.DefaultRequestHeaders.Add("Token", config.Token);
		httpClient.DefaultRequestHeaders.Add("Fingerprint", HardwareIdGenerator.GetHardwareId());

		apiClient = new PrusaCameraAPI.openapiClient(httpClient)
		{
			BaseUrl = config.BaseUrl
		};

		// Set up the upload timer
		uploadTimer.Interval = config.Delay * 1000; // Convert delay to milliseconds
		uploadTimer.Elapsed += UploadTimer_Elapsed;
		uploadTimer.Start();

		Console.WriteLine($"First Upload will run at {DateTime.UtcNow.AddSeconds(config.Delay)}");

		await ListenForEventsAsync();
	}

	private static void LoadConfiguration()
	{
		try
		{
			var configFile = "appsettings.json";
			if (!File.Exists(configFile))
			{
				Console.WriteLine($"Configuration file {configFile} not found. Creating from default settings...");
				CreateConfigurationFromDefaults();
			}

			var configJson = File.ReadAllText(configFile);
			config = JsonSerializer.Deserialize<PrusaCam.Configuration>(configJson);

			if (config == null)
			{
				Console.WriteLine("Configuration file is invalid or empty.");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error loading configuration: {ex.Message}");
		}
	}

	private static void CreateConfigurationFromDefaults()
	{
		try
		{
			var defaultConfigFile = "default-appsettings.json";
			if (!File.Exists(defaultConfigFile))
			{
				Console.WriteLine($"Default configuration file {defaultConfigFile} not found.");
				return;
			}

			var defaultConfigJson = File.ReadAllText(defaultConfigFile);
			var defaultConfig = JsonSerializer.Deserialize<PrusaCam.Configuration>(defaultConfigJson);

			if (defaultConfig == null)
			{
				Console.WriteLine("Default configuration file is invalid or empty.");
				return;
			}

			Console.WriteLine("Please enter the following settings:");
			defaultConfig.Token = PromptForSetting("Token", defaultConfig.Token);
			defaultConfig.BaseUrl = PromptForSetting("BaseUrl", defaultConfig.BaseUrl);
			defaultConfig.StreamURL = PromptForSetting("StreamURL", defaultConfig.StreamURL);
			defaultConfig.Delay = int.Parse(PromptForSetting("Delay", defaultConfig.Delay.ToString()));
			defaultConfig.FfmpegPathWindows = PromptForSetting("FfmpegPathWindows", defaultConfig.FfmpegPathWindows);
			defaultConfig.FfmpegPathLinux = PromptForSetting("FfmpegPathLinux", defaultConfig.FfmpegPathLinux);

			var newConfigJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText("appsettings.json", newConfigJson);
			Console.WriteLine("Configuration saved to appsettings.json.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error creating configuration: {ex.Message}");
		}
	}

	private static string PromptForSetting(string settingName, string defaultValue)
	{
		Console.Write($"{settingName} [{defaultValue}]: ");
		var input = Console.ReadLine();
		return string.IsNullOrEmpty(input) ? defaultValue : input;
	}

	private static async void UploadTimer_Elapsed(object sender, ElapsedEventArgs e)
	{
		if (!await semaphoreSlim.WaitAsync(0))
		{
			return; // Another upload is already in progress
		}

		try
		{
			Console.WriteLine($"Processing new image at {DateTime.UtcNow}");
			await CaptureAndUploadImageAsync();
			lastRun = DateTime.UtcNow;
			Console.WriteLine($"Image processed and uploaded at {DateTime.UtcNow}. Next allowed upload at {lastRun.AddSeconds(config.Delay)}");
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
		}
		finally
		{
			semaphoreSlim.Release();
		}
	}

	private static async Task CaptureAndUploadImageAsync()
	{
		var cancellationTokenSource = new CancellationTokenSource();
		var inputSource = new StreamInputSource(config.StreamURL);
		VideoStreamClient client;

		if (isWindows)
		{
			client = new VideoStreamClient(config.FfmpegPathWindows);
		}
		else
		{
			client = new VideoStreamClient(config.FfmpegPathLinux);
		}

		byte[] capturedImage = null;

		client.NewImageReceived += (imageData) =>
		{
			capturedImage = imageData;
			cancellationTokenSource.Cancel(); // Stop the stream after receiving the image
		};

		await client.StartFrameReaderAsync(inputSource, OutputImageFormat.Png, cancellationTokenSource.Token);

		if (capturedImage != null)
		{
			await ProcessAndUploadImageAsync(capturedImage);
		}
		else
		{
			Console.WriteLine("No image captured.");
		}

		client.NewImageReceived -= (imageData) => { capturedImage = imageData; };
	}

	private static async Task ProcessAndUploadImageAsync(byte[] imageData)
	{
		try
		{
			using (Image<Rgba32> image = Image.Load<Rgba32>(new MemoryStream(imageData)))
			{
				byte[] resizedImageBytes = ResizeImageToFitSize(image, 10 * 1024 * 1024);

				using (var stream2 = new MemoryStream(resizedImageBytes))
				{
					if (stream2.Length == 0)
					{
						Console.WriteLine("Resized image is empty."); //Opps
						return;
					}

					Console.WriteLine($"Uploading {stream2.Length} bytes");
					await apiClient.SnapshotAsync(stream2); //Send the image to Prusa API
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error during image processing and uploading: " + ex.Message);
		}
	}

	private static byte[] ResizeImageToFitSize(Image<Rgba32> image, long maxSizeInBytes)
	{
		float scalingFactor = 1.0f;
		const int maxIterations = 20;
		int iterations = 0;
		byte[] resizedImageBytes;

		do
		{
			using (var memoryStream = new MemoryStream())
			{
				int newWidth = (int)(image.Width * scalingFactor);
				int newHeight = (int)(image.Height * scalingFactor);

				var resizedImage = image.Clone(x => x.Resize(newWidth, newHeight));
				resizedImage.Save(memoryStream, new JpegEncoder());

				resizedImageBytes = memoryStream.ToArray();

				if (resizedImageBytes.Length > maxSizeInBytes)
				{
					scalingFactor *= 0.9f; // Reduce size
				}
				else
				{
					break;
				}

				iterations++;
			}
		} while (resizedImageBytes.Length > maxSizeInBytes && iterations < maxIterations);

		return resizedImageBytes;
	}

	static async Task ListenForEventsAsync()
	{
		while (true)
		{
			await Task.Delay(2000); // Non-blocking delay
		}
	}
}
