using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LiveStreamingNotifier.Services;
internal class WebImageCacheService(
	ILogger<WebImageCacheService> logger,
	IHttpClientFactory httpClientFactory,
	TimeProvider timeProvider)
{
	private string cacheDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(LiveStreamingNotifier), "cache");

	public async ValueTask<string> DownloadAsync(string uri, bool force, CancellationToken cancellationToken)
	{
		// HashName
		var fileName = "";
		var uriByte = Encoding.UTF8.GetBytes(uri);
		using (var sha256 = SHA256.Create())
		{
			var shaByte = sha256.ComputeHash(uriByte);
			fileName = $"{Convert.ToHexString(shaByte)}{System.IO.Path.GetExtension(uri)}";
		}

		// Path
		var fullPath = System.IO.Path.Combine(cacheDirectory, fileName);

		// Check
		if (force && System.IO.File.Exists(fullPath))
		{
			System.IO.File.Delete(fullPath);
		}

		if (System.IO.File.Exists(fullPath))
		{
			try
			{
				var fileInfo = new FileInfo(fullPath);
				fileInfo.LastWriteTime = timeProvider.GetLocalNow().LocalDateTime;
			}
			catch { }
			return fullPath;
		}

		// Download
		// Dir
		var parentDir = System.IO.Path.GetDirectoryName(fullPath);
		if (parentDir != null && !System.IO.Directory.Exists(parentDir))
		{
			System.IO.Directory.CreateDirectory(parentDir);
		}
		var httpClient = httpClientFactory.CreateClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken: cancellationToken);
		response.EnsureSuccessStatusCode();

		using var readStream = await response.Content.ReadAsStreamAsync(cancellationToken: cancellationToken);
		using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, true);
		readStream.CopyTo(fileStream);
		return fullPath;
	}
}
