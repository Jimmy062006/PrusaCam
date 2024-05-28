namespace PrusaCam
{
	public class Configuration
	{
		public string? StreamURL { get; set; } //RTSP URL to your RTSP Camera
		public int Delay { get; set; } //Delay between picture images
		public string? Token { get; set; } //API Key for the Camera from https://connect.prusa3d.com/printer
		public string? BaseUrl { get; set; } //Base URL to API Server
		public string? FfmpegPathWindows { get; set; } //Path to ffmpeg Windows
		public string? FfmpegPathLinux { get; set; } //Path to ffmpeg Linux, run whereis ffmpeg
	}
}
