using System;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Controls;

namespace GW2Scratch.ArcdpsLogManager.Configuration
{
	public class LogsSettingsPage : SettingsPage
	{
		private static readonly string[] DefaultLogLocation = {"Guild Wars 2", "addons", "arcdps", "arcdps.cbtlogs"};

		private readonly DirectoryListControl directoryList;
		private readonly CheckBox minDurationCheckBox;
		private readonly NumericMaskedTextBox<int> minDurationTextBox;

		public LogsSettingsPage()
		{
			Text = "日誌";

			minDurationCheckBox = new CheckBox
			{
				Text = "排除短日誌",
				Checked = Settings.MinimumLogDurationSeconds.HasValue,
				ThreeState = false
			};

			minDurationTextBox = new NumericMaskedTextBox<int>
			{
				Value = Settings.MinimumLogDurationSeconds ?? 5,
				Enabled = minDurationCheckBox.Checked ?? false,
				Width = 50
			};

			minDurationCheckBox.CheckedChanged += (sender, args) =>
				minDurationTextBox.Enabled = minDurationCheckBox.Checked ?? false;

			directoryList = new DirectoryListControl();

			var durationLabel = new Label
			{
				Text = "最短戰鬥時間(以秒為單位):", VerticalAlignment = VerticalAlignment.Center
			};

			var layout = new DynamicLayout();
			layout.BeginVertical(spacing: new Size(5, 5), padding: new Padding(10));
			{
				layout.BeginGroup("日誌目錄", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(new Label
					{
						Text = "儲存 arcdps 日誌的目錄。 子目錄也會被搜索，" +
							   "不要選擇包含更多不相關文件的父目錄，除非你喜歡額外的等待。",
						Wrap = WrapMode.Word,
						Height = 70
					});
					layout.AddRow(directoryList);
				}
				layout.EndGroup();
				layout.BeginGroup("日誌篩選", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(minDurationCheckBox);
					layout.AddRow(durationLabel, minDurationTextBox, null);
				}
				layout.EndGroup();
			}
			layout.EndVertical();

			Content = layout;

			if (Settings.LogRootPaths.Any())
			{
				directoryList.Directories = Settings.LogRootPaths;
			}
			else
			{
				string defaultDirectory = GetDefaultLogDirectory();
				if (Directory.Exists(defaultDirectory))
				{
					directoryList.Directories = new[] { defaultDirectory };
				}
				else
				{
					directoryList.Directories = Enumerable.Empty<string>();
				}
			}
		}

		public override void SaveSettings()
		{
			if (!directoryList.Directories.Select(x => x.Trim()).SequenceEqual(Settings.LogRootPaths))
			{
				Settings.LogRootPaths = directoryList.Directories.Select(x => x.Trim()).ToList();
			}

			bool minDurationChecked = minDurationCheckBox.Checked ?? false;
			Settings.MinimumLogDurationSeconds = minDurationChecked ? (int?) minDurationTextBox.Value : null;
		}

		private static string GetDefaultLogDirectory()
		{
			// We need to do this to get the correct separators on all platforms
			var pathParts = new[] {Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}
				.Concat(DefaultLogLocation)
				.ToArray();

			return Path.Combine(pathParts);
		}
	}
}