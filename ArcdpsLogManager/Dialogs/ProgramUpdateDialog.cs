using System.Diagnostics;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Configuration;
using GW2Scratch.ArcdpsLogManager.Updates;

namespace GW2Scratch.ArcdpsLogManager.Dialogs
{
	public class ProgramUpdateDialog : Dialog
	{
		public ProgramUpdateDialog(Release release)
		{
			Title = $"可用更新";
			ClientSize = new Size(-1, -1);
			var layout = new DynamicLayout();
			Content = layout;

			var changelog = new Button {Text = "查看更新內容"};
			var later = new Button {Text = "稍後"};
			var ignore = new Button {Text = "忽略"};
			var download = new Button {Text = "下載"};

			layout.BeginVertical(new Padding(10), new Size(10, 10));
			{
				layout.AddRow(new Label
				{
					Text = $"日誌管理器 {release.Version} 可供下載。"
				});
				layout.AddRow(changelog);
			}
			layout.EndVertical();

			later.Click += (sender, args) => Close();
			ignore.Click += (sender, args) =>
			{
				Settings.IgnoredUpdateVersions = Settings.IgnoredUpdateVersions.Append(release.Version).Distinct().ToList();
				Close();
			};
			changelog.Click += (sender, args) =>
			{
				var processInfo = new ProcessStartInfo()
				{
					FileName = release.ChangelogUrl,
					UseShellExecute = true
				};
				Process.Start(processInfo);
			};
			download.Click += (sender, args) =>
			{
				var processInfo = new ProcessStartInfo()
				{
					FileName = release.ToolSiteUrl,
					UseShellExecute = true
				};
				Process.Start(processInfo);
				Close();
			};

			AbortButton = later;
			DefaultButton = download;
			PositiveButtons.Add(download);
			NegativeButtons.Add(later);
			NegativeButtons.Add(ignore);
		}
	}
}