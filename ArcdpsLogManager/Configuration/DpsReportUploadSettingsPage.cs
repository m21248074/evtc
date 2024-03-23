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
			Text = "上傳 - dps.report";

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

			uploadDetailedWvwCheckbox = new CheckBox { Text = "詳細的 WvW 日誌報告 (大檔案可能會失敗)", Checked = Settings.DpsReportUploadDetailedWvw };

			autoUploadCheckbox = new CheckBox { Text = "自動上傳日誌", Checked = Settings.DpsReportAutoUpload };

			var userTokenTextBox = new TextBox { ReadOnly = true, Text = "************", Enabled = false };

			var showUserTokenButton = new Button { Text = "顯示" };
			showUserTokenButton.Click += (sender, args) =>
			{
				userTokenTextBox.Text = Settings.DpsReportUserToken;
				userTokenTextBox.Enabled = true;
			};
			var changeUserTokenButton = new Button { Text = "修改" };
			changeUserTokenButton.Click += (_, _) =>
			{
				if (EditingUserToken)
				{
					EditingUserToken = false;

					Settings.DpsReportUserToken = userTokenTextBox.Text;
					userTokenTextBox.ReadOnly = true;
					userTokenTextBox.Text = "************";
					userTokenTextBox.Enabled = false;

					changeUserTokenButton.Text = "修改";
					showUserTokenButton.Visible = true;
				}
				else
				{
					EditingUserToken = true;

					userTokenTextBox.ReadOnly = false;
					userTokenTextBox.Text = Settings.DpsReportUserToken;
					userTokenTextBox.Enabled = true;

					changeUserTokenButton.Text = "保存";
					showUserTokenButton.Visible = false;
				}
			};

			var layout = new DynamicLayout();
			layout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				layout.BeginGroup("自動上傳", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(new Label
					{
						Text = "如果啟用，新發現的日誌如果產生時間不到一天，將會自動排隊等待上傳。",
						Wrap = WrapMode.Word,
					});
					layout.AddRow(autoUploadCheckbox);
				}
				layout.EndGroup();

				layout.BeginGroup("上傳選項", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(uploadDetailedWvwCheckbox);
				}
				layout.EndGroup();

				layout.BeginGroup("上傳域名", new Padding(5), new Size(5, 5));
				{
					layout.AddRow(domainList);
					layout.AddRow(domainDescriptionLabel);
				}
				layout.EndGroup();

				layout.BeginGroup("使用者令牌", new Padding(5), new Size(5, 5));
				{
					layout.BeginVertical();
					{
						layout.AddRow(new Label
						{
							Text = "用於 dps.report 上傳的使用者令牌。 將其視為密碼，可用於查看之前所有上傳的內容。",
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