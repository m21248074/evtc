using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Uploads;

namespace GW2Scratch.ArcdpsLogManager.Configuration
{
	public class DpsReportUploadSettingsPage : SettingsPage
	{
		private readonly RadioButtonList domainList;
		private readonly CheckBox uploadDetailedWvwCheckbox;
		private readonly CheckBox autoUploadCheckbox;

		private bool EditingUserToken { get; set; }

		public DpsReportUploadSettingsPage()
		{
			Text = "�W�� - dps.report";

			domainList = new RadioButtonList();
			// The binding has to be set before the data store is as it's only used when the radio buttons are created.
			domainList.ItemTextBinding = new DelegateBinding<DpsReportDomain, string>(x => x.Domain);
			domainList.Orientation = Orientation.Vertical;
			domainList.DataStore = DpsReportUploader.AvailableDomains;

			// It is possible that the domain in settings does not exist anymore,
			// in case we can't match it with a current one, we add it as an extra
			// option to properly reflect the current state. If the user chooses
			// a different option after this, it won't be available anymore.
			var currentDomain = DpsReportUploader.AvailableDomains.FirstOrDefault(x => x.Domain == Settings.DpsReportDomain);
			if (currentDomain == null)
			{
				var savedDomain = new DpsReportDomain(Settings.DpsReportDomain, "");
				domainList.DataStore = DpsReportUploader.AvailableDomains.Append(savedDomain).ToList();
				currentDomain = savedDomain;
			}

			// The following does not work, TODO: Report
			// domainList.SelectedValue = currentDomain;

			// Because setting SelectedValue directly does not work,
			// we fall back to this workaround.
			int domainIndex = domainList.DataStore.TakeWhile(element => element != currentDomain).Count();
			domainList.SelectedIndex = domainIndex;

			var domainDescriptionLabel = new Label { Wrap = WrapMode.Word, Height = 50, Text = ((DpsReportDomain) domainList.SelectedValue).Description };

			domainList.SelectedValueChanged += (sender, args) =>
			{
				domainDescriptionLabel.Text = ((DpsReportDomain) domainList.SelectedValue).Description;
			};

			uploadDetailedWvwCheckbox = new CheckBox { Text = "�ԲӪ� WvW ��x���i (�j�ɮץi��|����)", Checked = Settings.DpsReportUploadDetailedWvw };

			autoUploadCheckbox = new CheckBox { Text = "�۰ʤW�Ǥ�x", Checked = Settings.DpsReportAutoUpload };

			var userTokenTextBox = new TextBox { ReadOnly = true, Text = "************", Enabled = false };

			var showUserTokenButton = new Button { Text = "���" };
			showUserTokenButton.Click += (sender, args) =>
			{
				userTokenTextBox.Text = Settings.DpsReportUserToken;
				userTokenTextBox.Enabled = true;
			};
			var changeUserTokenButton = new Button { Text = "�ק�" };
			changeUserTokenButton.Click += (_, _) =>
			{
				if (EditingUserToken)
				{
					EditingUserToken = false;

					Settings.DpsReportUserToken = userTokenTextBox.Text;
					userTokenTextBox.ReadOnly = true;
					userTokenTextBox.Text = "************";
					userTokenTextBox.Enabled = false;

					changeUserTokenButton.Text = "�ק�";
					showUserTokenButton.Visible = true;
				}
				else
				{
					EditingUserToken = true;

					userTokenTextBox.ReadOnly = false;
					userTokenTextBox.Text = Settings.DpsReportUserToken;
					userTokenTextBox.Enabled = true;

					changeUserTokenButton.Text = "�O�s";
					showUserTokenButton.Visible = false;
				}
			};

			var layout = new DynamicLayout();
			layout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				layout.BeginGroup("�۰ʤW��", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(new Label
					{
						Text = "�p�G�ҥΡA�s�o�{����x�p�G���ͮɶ�����@�ѡA�N�|�۰ʱƶ����ݤW�ǡC",
						Wrap = WrapMode.Word,
					});
					layout.AddRow(autoUploadCheckbox);
				}
				layout.EndGroup();

				layout.BeginGroup("�W�ǿﶵ", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(uploadDetailedWvwCheckbox);
				}
				layout.EndGroup();

				layout.BeginGroup("�W�ǰ�W", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(domainList);
					layout.AddRow(domainDescriptionLabel);
				}
				layout.EndGroup();

				layout.BeginGroup("�ϥΪ̥O�P", new Padding(5), new Size(5, 5));
				{
					layout.BeginVertical();
					{
						layout.AddRow(new Label
						{
							Text = "�Ω� dps.report �W�Ǫ��ϥΪ̥O�P�C �N������K�X�A�i�Ω�d�ݤ��e�Ҧ��W�Ǫ����e�C",
							Wrap = WrapMode.Word,
						});
					}
					layout.EndVertical();
					layout.BeginVertical(spacing: new Size(5, 5));
					{
						layout.BeginHorizontal();
						{
							layout.Add(userTokenTextBox, true);
							layout.Add(showUserTokenButton, false);
							layout.Add(changeUserTokenButton, false);
						}
						layout.EndHorizontal();
					}
					layout.EndVertical();
				}
				layout.EndGroup();
			}
			layout.EndVertical();
			layout.AddRow();

			Content = layout;
		}

		public override void SaveSettings()
		{
			Settings.DpsReportDomain = ((DpsReportDomain) domainList.SelectedValue).Domain;
			if (uploadDetailedWvwCheckbox.Checked.HasValue)
			{
				Settings.DpsReportUploadDetailedWvw = uploadDetailedWvwCheckbox.Checked.Value;
			}

			if (autoUploadCheckbox.Checked.HasValue)
			{
				Settings.DpsReportAutoUpload = autoUploadCheckbox.Checked.Value;
			}
		}
	}
}