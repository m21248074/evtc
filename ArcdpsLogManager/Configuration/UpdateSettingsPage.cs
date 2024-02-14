using Eto.Drawing;
using Eto.Forms;

namespace GW2Scratch.ArcdpsLogManager.Configuration
{
	public class UpdateSettingsPage : SettingsPage
	{
		private readonly CheckBox updateCheckbox;

		public UpdateSettingsPage()
		{
			Text = "��s";
			updateCheckbox = new CheckBox {Text = "�Ұʮ��ˬd��s", Checked = Settings.CheckForUpdates};

			var layout = new DynamicLayout();
			layout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				layout.BeginGroup("��x�޲z����s", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(new Label
					{
						Text = "�{���i�H�b�Ұʮɦ۰ʴM���s�A�æb���s�����i�ήɳq���z�C",
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