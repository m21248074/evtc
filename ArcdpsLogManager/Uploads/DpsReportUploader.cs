using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Updates;
using GW2Scratch.EVTCAnalytics.GameData.Encounters;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;

namespace GW2Scratch.ArcdpsLogManager.Uploads
{
	public class DpsReportUploader : IDisposable
	{
		public static readonly DpsReportDomain DefaultDomain = new DpsReportDomain("https://dps.report",
			"Cloudflare. 支援 HTTPS。 在東歐和亞洲國家可能不可靠");

		private static readonly DpsReportDomain DomainA = new DpsReportDomain("http://a.dps.report",
			"Imperva. 僅支援 HTTP。 相當可靠，作為最後手段。");

		private static readonly DpsReportDomain DomainB = new DpsReportDomain("https://b.dps.report",
			"Sucuri. 支援 HTTPS。 支援 HTTPS 的替代服務網域。");

		public static IReadOnlyList<DpsReportDomain> AvailableDomains { get; } = new[]
		{
			DefaultDomain, DomainA, DomainB
		};

		private const string UploadEndpoint = "/uploadContent";

		private readonly HttpClient httpClient = new HttpClient
		{
			Timeout = Timeout.InfiniteTimeSpan,
			DefaultRequestHeaders =
			{
				UserAgent =
				{
					new ProductInfoHeaderValue("arcdpsLogManager", typeof(ProgramUpdateChecker).Assembly.GetName().Version?.ToString())
				}
			}
			
		};

		public string Domain { get; set; }
		
		public bool UploadDetailedWvw { get; set; }

		public DpsReportUploader() : this(DefaultDomain)
		{
		}

		public DpsReportUploader(bool detailedWvw) : this(DefaultDomain, detailedWvw)
		{
		}

		public DpsReportUploader(DpsReportDomain domain, bool detailedWvw = false)
		{
			Domain = domain.Domain;
			UploadDetailedWvw = detailedWvw;
		}

		public DpsReportUploader(string domain)
		{
			Domain = domain;
		}

		public async Task<DpsReportResponse> UploadLogAsync(LogData log, CancellationToken cancellationToken,
			string userToken = null)
		{
			string query = Domain + UploadEndpoint + "?json=1&generator=ei";
			
			if (log.Encounter == Encounter.WorldVersusWorld && UploadDetailedWvw)
			{
				query += "&detailedwvw=true";
			}
			
			if (userToken != null)
			{
				query += $"&userToken={userToken}";
			}

			bool isZipped = IsZippedLog(log.FileName);

			HttpResponseMessage response;
			while (true)
			{
				using (var content = new MultipartFormDataContent())
				{
					if (isZipped)
					{
						await using (var stream = log.FileInfo.OpenRead())
						{
							content.Add(new StreamContent(stream), "file", log.FileInfo.Name);
							response = await httpClient.PostAsync(query, content, cancellationToken);
						}
					}
					else
					{
						string filename = $"{log.FileInfo.Name}.zevtc";

						await using var evtcStream = log.FileInfo.OpenRead();

						await using var archiveMemoryStream = new MemoryStream();
						using (var archive = new ZipArchive(archiveMemoryStream, ZipArchiveMode.Create, true))
						{
							var entry = archive.CreateEntry(filename);

							await using var entryStream = entry.Open();
							evtcStream.CopyTo(entryStream);
						}

						archiveMemoryStream.Seek(0, SeekOrigin.Begin);
						content.Add(new StreamContent(archiveMemoryStream), "file", filename);
						response = await httpClient.PostAsync(query, content, cancellationToken);
					}
				}

				// The DPS report API has a rate limit.
				// We could read its size in the response; it can change depending
				// on the current load and we could adapt according to that.
				// As of early 2024, it sits somewhere between 25 and 15 requests per minute.
				// As a simple solution that should also alleviate the load on the API, we
				// just always wait a specific time when the limit is hit. It is likely
				// inefficient (we could hit the API harder), but it should be good enough.
				if (response.StatusCode == HttpStatusCode.TooManyRequests)
				{
					response.Dispose();
					await Task.Delay(10000, cancellationToken);
				}
				else
				{
					break;
				}
			}

			string json = await response.Content.ReadAsStringAsync(cancellationToken);
			var responseData = JsonConvert.DeserializeObject<DpsReportResponse>(json);

			return responseData;
		}

		public Task<DpsReportResponse> UploadLogAsync(LogData log, string userToken = null)
		{
			return UploadLogAsync(log, CancellationToken.None, userToken);
		}

		public void Dispose()
		{
			httpClient?.Dispose();
		}

		private bool IsZippedLog(string filename)
		{
			// This is not exactly the best approach, but as far as I know there is no built-in
			// function for checking if a zip is valid.
			try
			{
				using var archive = ZipFile.OpenRead(filename);
				return archive.Entries.Count > 0;
			}
			catch
			{
				return false;
			}
		}
	}
}