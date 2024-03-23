using Eto.Drawing;
using Eto.Forms;
using System;

namespace GW2Scratch.ArcdpsLogManager.Configuration
{
	public class SettingsForm : Form
	{
		public event EventHandler SettingsSaved;

		public SettingsForm()
		{
			Title = "設定 - arcdps 日誌管理器";
			ClientSize = new Size(400, -1);
			MinimumSize = new Size(400, 500);

			var pages = new SettingsPage[]
			{
				new LogsSettingsPage(),
				new ApiSettingsPage(),
				new DpsReportUploadSettingsPage(),
				new UpdateSettingsPage()
			};

			var tabs = new TabControl();
			foreach (var page in pages)
			{
				tabs.Pages.Add(page);
			}

			var saveButton = new Button {Text = "保存"};
			saveButton.Click += (sender, args) =>
			{
				foreach (var page in pages)
				{
					page.SaveSettings();
				}
				SettingsSaved?.Invoke(this, EventArgs.Empty);

				Close();
			};

			var layout = new DynamicLayout();
			layout.BeginVertical(new Padding(10));
			{
				layout.Add(tabs);
			}
			layout.EndVertical();
			layout.Add(null);
			layout.BeginVertical(new Padding(10));
			{
				layout.BeginHorizontal();
				{
					layout.Add(null, xscale: true);
					layout.Add(saveButton, xscale: false);
				}
				layout.EndHorizontal();
			}
			layout.EndVertical();

			Content = layout;
		}
	}
}