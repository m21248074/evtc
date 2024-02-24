using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Configuration;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Filters;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Logs.Tagging;
using GW2Scratch.ArcdpsLogManager.Processing;
using GW2Scratch.EVTCAnalytics.Processing.Encounters.Results;
using System.Runtime.InteropServices;
using Button = Eto.Forms.Button;
using Label = Eto.Forms.Label;

namespace GW2Scratch.ArcdpsLogManager.Controls
{
	public sealed class MultipleLogPanel : DynamicLayout
	{
		private LogData[] logData;
		private ILogNameProvider nameProvider;
		private ImageProvider imageProvider;

		private readonly Label countLabel = new Label {Font = Fonts.Sans(12)};
		private readonly Label parseTimeLabel = new Label();
		private readonly Label totalDurationLabel = new Label();
		private readonly Label successCountLabel = new Label();
		private readonly Label failureCountLabel = new Label();
		private readonly Label dpsReportNotUploadedLabel = new Label();
		private readonly Label dpsReportUploadingLabel = new Label();
		private readonly Label dpsReportUploadedLabel = new Label();
		private readonly Label dpsReportProcessingFailedLabel = new Label();
		private readonly Label dpsReportUploadFailedLabel = new Label();
		private readonly TextArea dpsReportLinkTextArea = new TextArea {ReadOnly = true};
		private readonly Button dpsReportUploadButton = new Button();
		private readonly Button dpsReportCancelButton = new Button {Text = "取消"};
		private readonly Button dpsReportOpenButton = new Button {Text = "在瀏覽器中開啟已上傳的日誌", Enabled = false};
		private readonly Button copyButton;
		private readonly ProgressBar dpsReportUploadProgressBar = new ProgressBar();
		private readonly DynamicTable dpsReportUploadFailedRow;
		private readonly DynamicTable dpsReportProcessingFailedRow;
		private readonly TagControl tagControl;

		public IEnumerable<LogData> LogData
		{
			get => logData;
			set
			{
				SuspendLayout();
				logData = value?.ToArray();

				if (logData == null || logData.Length < 2)
				{
					Visible = false;
					return;
				}

				Visible = true;

				countLabel.Text = $"已選擇 {logData.Length} 條日誌";
				var totalDuration = logData.Where(x => x.ParsingStatus == ParsingStatus.Parsed).Aggregate(TimeSpan.Zero, (x, y) => x + y.EncounterDuration);
				totalDurationLabel.Text = FormatTimeSpan(totalDuration);
				successCountLabel.Text = logData.Count(x => x.ParsingStatus == ParsingStatus.Parsed && x.EncounterResult == EncounterResult.Success).ToString();
				failureCountLabel.Text = logData.Count(x => x.ParsingStatus == ParsingStatus.Parsed && x.EncounterResult == EncounterResult.Failure).ToString();
				parseTimeLabel.Text = $"{logData.Select(x => x.ParseMilliseconds).Sum()} 毫秒";

				UpdateDpsReportUploadStatus();

				UpdateTags();

				ResumeLayout();
			}
		}

		private void UpdateTags()
		{
			if (logData?.Any() != true)
			{
				tagControl.Tags = Enumerable.Empty<TagInfo>();
				return;
			}

			var allTags = logData.Select(it => it.Tags).ToList();

			var commonTags = new HashSet<TagInfo>(allTags.First());
			foreach (var tagSet in allTags.Skip(1))
			{
				commonTags.IntersectWith(tagSet);
			}

			tagControl.Tags = commonTags;
		}

		private void UpdateDpsReportUploadStatus()
		{
			if (logData == null) return;

			int notUploaded = logData.Count(x => x.DpsReportEIUpload.UploadState == UploadState.NotUploaded);
			int queued = logData.Count(x => x.DpsReportEIUpload.UploadState == UploadState.Queued);
			int uploading = logData.Count(x => x.DpsReportEIUpload.UploadState == UploadState.Uploading);
			int uploaded = logData.Count(x => x.DpsReportEIUpload.UploadState == UploadState.Uploaded);
			int uploadsFailed = logData.Count(x => x.DpsReportEIUpload.UploadState == UploadState.UploadError);
			int processingFailed = logData.Count(x => x.DpsReportEIUpload.UploadState == UploadState.ProcessingError);

			int finished = uploaded + uploadsFailed + processingFailed;
			int totalRequested = queued + uploading + uploaded + uploadsFailed + processingFailed;
			dpsReportUploadProgressBar.MaxValue = totalRequested > 0 ? totalRequested : 1;
			dpsReportUploadProgressBar.Value = finished;
			dpsReportUploadButton.Enabled = notUploaded + uploadsFailed > 0;
			dpsReportCancelButton.Enabled = queued > 0;
			dpsReportUploadButton.Text = $"上傳缺少的日誌 ({notUploaded + uploadsFailed})";
			dpsReportNotUploadedLabel.Text = notUploaded.ToString();
			dpsReportUploadingLabel.Text = (uploading + queued).ToString();
			dpsReportUploadedLabel.Text = uploaded.ToString();
			dpsReportUploadFailedLabel.Text = uploadsFailed.ToString();
			dpsReportUploadFailedRow.Table.Visible = uploadsFailed > 0;
			dpsReportProcessingFailedLabel.Text = processingFailed.ToString();
			dpsReportProcessingFailedRow.Table.Visible = processingFailed > 0;
			dpsReportOpenButton.Enabled = uploaded > 0;
			dpsReportLinkTextArea.Text = string.Join(Environment.NewLine,
				logData.Where(x => x.DpsReportEIUpload.Url != null).Select(x => x.DpsReportEIUpload.Url));
			copyButton.Enabled = uploaded > 0;
			if (Application.Instance.Platform.IsWpf)
			{
				bool copyEnabled = uploaded > 0;
				copyButton.Image = copyEnabled ? imageProvider.GetCopyButtonEnabledImage() : imageProvider.GetCopyButtonDisabledImage();
			}
		}

		public MultipleLogPanel(LogCache logCache, ApiData apiData, LogDataProcessor logProcessor,
			UploadProcessor uploadProcessor, ILogNameProvider nameProvider, ImageProvider imageProvider,
			bool readOnly = false)
		{
			this.nameProvider = nameProvider;
			this.imageProvider = imageProvider;

			Padding = new Padding(10);
			Width = 350;
			Visible = false;

			var reparseButton = new Button {Text = "Reprocess all"};
			reparseButton.Click += (sender, args) =>
			{
				foreach (var log in logData)
				{
					logProcessor.Schedule(log);
				}
			};

			dpsReportUploadButton.Click += (sender, args) =>
			{
				foreach (var log in logData)
				{
					var state = log.DpsReportEIUpload.UploadState;
					if (state == UploadState.NotUploaded || state == UploadState.UploadError)
					{
						uploadProcessor.ScheduleDpsReportEIUpload(log);
					}
				}
			};

			dpsReportOpenButton.Click += (sender, args) =>
			{
				var uploadedLogs = logData.Where(x => x.DpsReportEIUpload.UploadState == UploadState.Uploaded).ToList();
				if (uploadedLogs.Count > 5)
				{
					var result = MessageBox.Show(
						$"您確定要在瀏覽器中一次開啟 {uploadedLogs.Count} 個日誌嗎?",
						"在瀏覽器中開啟上傳的日誌",
						MessageBoxButtons.YesNo,
						MessageBoxType.Question);

					if (result != DialogResult.Yes)
					{
						return;
					}
				}

				foreach (var log in logData)
				{
					var state = log.DpsReportEIUpload.UploadState;
					if (state == UploadState.Uploaded)
					{
						var processInfo = new ProcessStartInfo()
						{
							FileName = log.DpsReportEIUpload.Url,
							UseShellExecute = true
						};
						Process.Start(processInfo);
					}
				}
			};

			dpsReportCancelButton.Click += (sender, args) => { uploadProcessor.CancelDpsReportEIUpload(logData); };

			DynamicGroup debugSection;

			tagControl = new TagControl {ReadOnly = readOnly};
			tagControl.TagAdded += (sender, args) =>
			{
				var added = false;
				foreach (var log in logData)
				{
					added |= log.Tags.Add(new TagInfo(args.Name));
					if (added)
					{
						logCache.CacheLogData(log);
					}
				}
			};

			tagControl.TagRemoved += (sender, args) =>
			{
				var removed = false;
				foreach (var log in logData)
				{
					removed |= log.Tags.Remove(new TagInfo(args.Name));
					if (removed)
					{
						logCache.CacheLogData(log);
					}
				}
			};

			if (Application.Instance.Platform.IsWpf)
			{
				copyButton = new Button { Image = imageProvider.GetCopyButtonEnabledImage(), Height = 25, Width = 25 };
			}
			else
			{
				// The height is not working correctly on Gtk, and the icon may have clashing colors depending on the Gtk theme.
				copyButton = new Button { Text = "複製" };
			}

			copyButton.Click += (sender, args) =>
			{
				var urls = "";
				foreach (var log in logData)
				{
					var state = log.DpsReportEIUpload.UploadState;
					if (state == UploadState.Uploaded)
					{
						urls += log.DpsReportEIUpload.Url + Environment.NewLine;
					}
				}
				var copyClipboard = new Clipboard()
				{
					Text = urls,
				};
			};


			BeginVertical(spacing: new Size(0, 30));
			{
				BeginVertical();
				{
					//Add(new Label {Text = "Batch log management", Font = Fonts.Sans(16, FontStyle.Bold)});
					Add(countLabel);
				}
				EndVertical();
				BeginGroup("統計數據", new Padding(5), new Size(5, 5));
				{
					AddRow("戰勝日誌數: ", successCountLabel);
					AddRow("戰敗日誌數: ", failureCountLabel);
					AddRow("總持續時間: ", totalDurationLabel);
				}
				EndGroup();
				debugSection = BeginGroup("除錯資料", new Padding(5), new Size(5, 5));
				{
					AddRow("處理所花費的時間: ", parseTimeLabel);
					Add(reparseButton);
				}
				EndGroup();

				BeginHorizontal();
				{
					Add(new Scrollable {Content = tagControl, Border = BorderType.None});
				}
				EndHorizontal();

				BeginGroup("上傳至 dps.report (Elite Insights)", padding: new Padding(5), spacing: new Size(0, 5), yscale: true);
				{
					BeginVertical(spacing: new Size(5, 5));
					{
						AddRow("尚未上傳:", dpsReportNotUploadedLabel);
						AddRow("上傳中:", dpsReportUploadingLabel);
						AddRow("已上傳:", dpsReportUploadedLabel);
						dpsReportUploadFailedRow = BeginVertical(spacing: new Size(5, 5));
						{
							AddRow("上傳失敗:", dpsReportUploadFailedLabel);
						}
						EndVertical();
						dpsReportProcessingFailedRow = BeginVertical(spacing: new Size(5, 5));
						{
							AddRow("dps.report 錯誤:", dpsReportProcessingFailedLabel);
						}
						EndVertical();
					}
					EndVertical();
					
					if (!readOnly)
					{
						BeginVertical(spacing: new Size(5, 5));
						{
							BeginHorizontal();
							{
								Add(dpsReportUploadButton, true);
								Add(dpsReportCancelButton);
							}
							EndHorizontal();
						}
						EndVertical();
					}
					BeginVertical(yscale: true);
					{
						AddRow(dpsReportUploadProgressBar);
						BeginHorizontal(yscale: true);
						{
							Add(dpsReportLinkTextArea);
						}
						EndHorizontal();
					}
					EndVertical();
					BeginVertical(yscale: false, spacing: new Size(5, 5));
					{
						BeginHorizontal();
						{
							Add(copyButton);
							Add(dpsReportOpenButton);
						}
						EndHorizontal();
						
					}
					EndVertical();
				}
				EndGroup();
			}
			EndVertical();

			// We need to hide the inner groupbox as hiding just the section does not work.
			Settings.ShowDebugDataChanged += (sender, args) =>
			{
				debugSection.Visible = Settings.ShowDebugData;
				debugSection.GroupBox.Visible = Settings.ShowDebugData;
			};

			Shown += (sender, args) =>
			{
				// Assigning visibility in the constructor does not work
				debugSection.Visible = Settings.ShowDebugData;
				debugSection.GroupBox.Visible = Settings.ShowDebugData;
			};

			uploadProcessor.Processed += OnUploadProcessorUpdate;
			uploadProcessor.Unscheduled += OnUploadProcessorUpdate;
			uploadProcessor.Scheduled += OnUploadProcessorUpdate;
		}
		
		private string FormatTimeSpan(TimeSpan time)
		{
			var hour = time.ToString("hh") == "00" ? "" : time.ToString("hh") + "小時 ";
			var str = hour + time.ToString("mm") + "分 " + time.ToString("ss") + "秒";
			if (time.Days > 0)
			{
				str = $@"{time:%d\d} " + str;
			}
			return str;
		}

		private void OnUploadProcessorUpdate(object sender, EventArgs e)
		{
			Application.Instance.Invoke(UpdateDpsReportUploadStatus);
		}
	}
}