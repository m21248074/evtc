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
			Text = "��x";

			minDurationCheckBox = new CheckBox
			{
				Text = "�ư��u��x",
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
				Text = "�̵u�԰��ɶ�(�H�����):", VerticalAlignment = VerticalAlignment.Center
			};

			var layout = new DynamicLayout();
			layout.BeginVertical(spacing: new Size(5, 5), padding: new Padding(10));
			{
				layout.BeginGroup("��x�ؿ�", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(new Label
					{
						Text = "�x�s arcdps ��x���ؿ��C �l�ؿ��]�|�Q�j���A" +
							   "���n��ܥ]�t��h��������󪺤��ؿ��A���D�A���w�B�~�����ݡC",
						Wrap = WrapMode.Word,
						Height = 70
					});
					layout.AddRow(directoryList);
				}
				layout.EndGroup();
				layout.BeginGroup("��x�z��", new Padding(5), new Size(5, 5));
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