using System;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace PrusaCam
{
	public class HardwareIdGenerator
	{
		public static string GetHardwareId()
		{
			var cpuId = GetCpuId();
			var motherboardId = GetMotherboardId();
			var macAddress = GetMacAddress();

			var combinedId = $"{cpuId}-{motherboardId}-{macAddress}";
			return GenerateSha256Hash(combinedId);
		}

		private static string GetCpuId()
		{
			return GetWmiProperty("Win32_Processor", "ProcessorId");
		}

		private static string GetMotherboardId()
		{
			return GetWmiProperty("Win32_BaseBoard", "SerialNumber");
		}

		private static string GetMacAddress()
		{
			return GetWmiProperty("Win32_NetworkAdapterConfiguration", "MACAddress", "IPEnabled");
		}

		private static string GetWmiProperty(string wmiClass, string wmiProperty, string conditionProperty = null)
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher($"SELECT {wmiProperty} FROM {wmiClass}"))
				{
					foreach (var obj in searcher.Get())
					{
						if (conditionProperty == null || (bool)obj[conditionProperty])
						{
							return obj[wmiProperty]?.ToString() ?? string.Empty;
						}
					}
				}
			}
			catch
			{
				// Handle exceptions as needed
			}

			return string.Empty;
		}

		private static string GenerateSha256Hash(string input)
		{
			using (var sha256 = SHA256.Create())
			{
				var bytes = Encoding.UTF8.GetBytes(input);
				var hash = sha256.ComputeHash(bytes);
				return BitConverter.ToString(hash).Replace("-", string.Empty);

			}
		}
	}
}
