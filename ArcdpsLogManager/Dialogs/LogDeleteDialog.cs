using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Compressing;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Processing;
using GW2Scratch.ArcdpsLogManager.Sections;
using GW2Scratch.EVTCAnalytics.GameData.Encounters;
using GW2Scratch.EVTCAnalytics.Processing.Encounters.Results;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GW2Scratch.ArcdpsLogManager.Dialogs
{
	/// <summary>
	/// Lists files before deleting them, the user can remove files from the list as well before confirming.
	/// </summary>
	public class LogDeleteDialog : Dialog
	{
		private readonly ILogNameProvider nameProvider;
		private readonly FilterCollection<LogData> dataStore;

		private readonly Button confirmDeleteButton = new Button { Text = "永久刪除所列的日誌" };
		private readonly Button abortButton = new Button { Text = "取消" };
		private readonly Label savedSpaceLabel = new Label { Text = "..." };

		public LogDeleteDialog(IEnumerable<LogData> files, LogCache logCache, ApiData apiData,
			LogDataProcessor logProcessor, UploadProcessor uploadProcessor, ImageProvider imageProvider,
			ILogNameProvider nameProvider)
		{
			this.nameProvider = nameProvider;
			dataStore = new FilterCollection<LogData>(files);
			var layout = new DynamicLayout();

			Icon = Resources.GetProgramIcon();
			Title = "刪除確認 - arcdps 日誌管理器";
			ClientSize = new Size(750, 500);
			Resizable = true;
			Content = layout;

			var logList = new LogList(logCache, apiData, logProcessor, uploadProcessor, imageProvider, nameProvider,
				true);
			logList.DataStore = dataStore;

			abortButton.Click += (_, _) => Close();
			confirmDeleteButton.Click += ConfirmDeleteButtonClicked;

			AbortButton = abortButton;
			NegativeButtons.Add(abortButton);
			PositiveButtons.Add(confirmDeleteButton);

			var standardFontSize = SystemFonts.Bold().Size;
			var topWarningLabel = new Label
			{
				Text = "您確定要永久刪除以下日誌嗎？",
				Font = SystemFonts.Bold(standardFontSize * 1.5f),
				TextAlignment = TextAlignment.Center
			};

			var warningLabel = new Label
			{
				Text = "此操作無法撤消，並且這些日誌的統計資料將會遺失。",
				Wrap = WrapMode.Word,
				TextAlignment = TextAlignment.Center
			};

			Task.Run(() =>
			{
				long totalBytes = 0;

				foreach (var file in dataStore)
				{
					try
					{
						totalBytes += file.FileInfo.Length;
					}
					catch
					{
						return null;
					}
				}

				return (long?) totalBytes;
			}).ContinueWith(totalBytes =>
			{
				Application.Instance.AsyncInvoke(() =>
				{
					var bytes = totalBytes.Result;
					if (bytes.HasValue)
					{
						double megabytes = bytes.Value / 1000.0 / 1000.0; // Yes, 1000-based MB.
						savedSpaceLabel.Text = $"{megabytes:0.00} MB";
					}
					else
					{
						savedSpaceLabel.Text = "???";
					}
				});
			});

			int uncompressedLogCount = dataStore.Count(x => !CompressionUtils.HasZipExtension(x.FileName));

			var compressButton = new Button { Text = "Compress logs" };
			compressButton.Click += (_, _) =>
			{
				if (Application.Instance.MainForm is ManagerForm managerForm)
				{
					var logs = managerForm.LoadedLogs.ToList();
					new CompressDialog(logs, managerForm.LogCompressionProcessor).ShowModal(this);
				}
			};

			layout.BeginVertical(padding: new Padding(5, 5), spacing: new Size(5, 5));
			{
				layout.AddRow(topWarningLabel);
				layout.AddRow(warningLabel);
				layout.AddRow();

				layout.BeginVertical(spacing: new Size(5, 5));
				{
					layout.BeginCentered(spacing: new Size(5, 5));
					{
						layout.AddRow("檔案總數:", $"{dataStore.Count}");
						layout.AddRow("檔案總大小:", savedSpaceLabel);
					}
					layout.EndCentered();
				}
				layout.EndVertical();

				if (uncompressedLogCount > 0)
				{
					layout.BeginVertical();
					{
						layout.BeginCentered(spacing: new Size(5, 5));
						{
							layout.AddRow(new Label
							{
								Text = UncompressedLogsLabelText(uncompressedLogCount),
								Font = SystemFonts.Bold(decoration: FontDecoration.Underline),
							});
							layout.AddRow(compressButton);
						}
						layout.EndCentered();
					}
					layout.EndVertical();
				}

				layout.Add(logList, yscale: true);
			}
			layout.EndVertical();
		}

		private static string UncompressedLogsLabelText(int uncompressedLogCount)
		{
			if (uncompressedLogCount == 1)
			{
				return $"{uncompressedLogCount} log may be compressed to save a significant amount of space.";
			}

			return $"{uncompressedLogCount} logs may be compressed to save a significant amount of space.";
		}

		private void ConfirmDeleteButtonClicked(object sender, EventArgs e)
		{
			if (UnreliableLogsFoundDialog.IsApplicable(dataStore))
			{
				UnreliableLogsFoundDialog dialog = new UnreliableLogsFoundDialog(dataStore, nameProvider);
				if (dialog.ShowDialog(this))
				{
					DeleteLogsAndClose(dataStore);
				}
			}
			else
			{
				DeleteLogsAndClose(dataStore);
			}
		}

		private void DeleteLogsAndClose(IEnumerable<LogData> logs)
		{
			DeleteLogs(logs);
			if (Application.Instance.MainForm is ManagerForm managerForm)
			{
				managerForm.ReloadLogs();
			}

			Close();
		}

		private void DeleteLogs(IEnumerable<LogData> logs)
		{
			foreach (var log in logs)
			{
				File.Delete(log.FileName);
			}
		}
	}
}