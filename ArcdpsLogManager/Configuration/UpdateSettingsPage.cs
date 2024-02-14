using Eto.Drawing;
using Eto.Forms;

namespace GW2Scratch.ArcdpsLogManager.Configuration
{
	public class UpdateSettingsPage : SettingsPage
	{
		private readonly CheckBox updateCheckbox;

		public UpdateSettingsPage()
		{
			Text = "更新";
			updateCheckbox = new CheckBox {Text = "啟動時檢查更新", Checked = Settings.CheckForUpdates};

			var layout = new DynamicLayout();
			layout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				layout.BeginGroup("日誌管理器更新", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(new Label
					{
						Text = "程式可以在啟動時自動尋找更新，並在有新版本可用時通知您。",
						Wrap = WrapMode.Word,
						Height = 50
					});
					layout.AddRow(updateCheckbox);
				}
				layout.EndGroup();
				layout.AddRow(null);
			}
			layout.EndVertical();


			Content = layout;
		}

		public override void SaveSettings()
		{
			Settings.CheckForUpdates = updateCheckbox.Checked ?? false;
		}
	}
}