using Eto.Drawing;
using Eto.Forms;

namespace GW2Scratch.ArcdpsLogManager.Configuration
{
	public class ApiSettingsPage : SettingsPage
	{
		private readonly CheckBox apiDataCheckbox;

		public ApiSettingsPage()
		{
			Text = "激戰2 API";
			apiDataCheckbox = new CheckBox {Text = "使用激戰2 API", Checked = Settings.UseGW2Api};

			var layout = new DynamicLayout();
			layout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				layout.BeginGroup("API 資料", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(new Label
					{
						Text = "該程式可以使用激戰2 官方API 來檢索公會資料和地圖名稱。 " +
							   "不需要 API 金鑰。 " +
							   "如果未啟用此功能，則公會名稱及其標籤以及地圖名稱將不可用。",
						Wrap = WrapMode.Word,
						//Height = 60
					});
					layout.AddRow(apiDataCheckbox);
				}
				layout.EndGroup();
				layout.AddRow(null);
			}
			layout.EndVertical();


			Content = layout;
		}

		public override void SaveSettings()
		{
			Settings.UseGW2Api = apiDataCheckbox.Checked ?? false;
		}
	}
}