using Eto.Drawing;
using Eto.Forms;

namespace GW2Scratch.ArcdpsLogManager.Configuration
{
	public class ApiSettingsPage : SettingsPage
	{
		private readonly CheckBox apiDataCheckbox;

		public ApiSettingsPage()
		{
			Text = "�E��2 API";
			apiDataCheckbox = new CheckBox {Text = "�ϥοE��2 API", Checked = Settings.UseGW2Api};

			var layout = new DynamicLayout();
			layout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				layout.BeginGroup("API ���", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(new Label
					{
						Text = "�ӵ{���i�H�ϥοE��2 �x��API ���˯����|��ƩM�a�ϦW�١C " +
							   "���ݭn API ���_�C " +
							   "�p�G���ҥΦ��\��A�h���|�W�٤Ψ���ҥH�Φa�ϦW�ٱN���i�ΡC",
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