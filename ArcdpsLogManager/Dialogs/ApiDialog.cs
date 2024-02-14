using System;
using System.IO;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Configuration;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Processing;

namespace GW2Scratch.ArcdpsLogManager.Dialogs
{
	public class ApiDialog : Dialog
	{
		public ApiDialog(ApiProcessor apiProcessor)
		{
			Title = "API 資料 - arcdps 日誌管理器";
			ClientSize = new Size(500, -1);
			var formLayout = new DynamicLayout();
			Content = formLayout;

			var item = new Button {Text = "關閉"};
			item.Click += (sender, args) => Close();
			PositiveButtons.Add(item);

			var deleteButton = new Button
			{
				Text = "刪除快取",
			};

			var enableCheckbox = new CheckBox {Text = "使用激戰2 API", Checked = Settings.UseGW2Api};
			enableCheckbox.CheckedChanged += (sender, args) => Settings.UseGW2Api = enableCheckbox.Checked ?? false;

			var guildCountLabel = new Label {Text = "沒有加載" };
			var sizeLabel = new Label {Text = "沒有檔案"};

			UpdateLabelTexts(apiProcessor.ApiData, guildCountLabel, sizeLabel);

			formLayout.BeginVertical(new Padding(10), new Size(0, 0));
			{
				formLayout.AddRow(new Label
				{
					Text = "公會名稱和標籤必須從激戰2官方 API 加載，因為 EVTC 日誌僅包含 GUID 值。",
					Wrap = WrapMode.Word
				});
			}
			formLayout.EndVertical();
			formLayout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				formLayout.AddRow(new Label {Text = "公會數:"}, guildCountLabel);
				formLayout.AddRow(new Label {Text = "快取檔案大小:"}, sizeLabel);
			}
			formLayout.EndVertical();
			formLayout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				formLayout.AddRow(enableCheckbox);
				formLayout.AddRow(deleteButton);
			}
			formLayout.EndVertical();

			void OnGuildAdded(object sender, EventArgs _)
			{
				Application.Instance.AsyncInvoke(() =>
				{
					UpdateLabelTexts(apiProcessor.ApiData, guildCountLabel, sizeLabel);
				});
			}

			deleteButton.Click += (sender, args) =>
			{
				if (MessageBox.Show(
					    $"確定刪除 API 快取? 所有 {apiProcessor.ApiData.CachedGuildCount} 個公會的 API 資料都將被刪除。 " +
						"重新命名的公會名稱/標籤會更新，但解散的公會資料將無法再檢索。 " +
						"您可能需要重新啟動程式才能更新所有內容。",
					    MessageBoxButtons.OKCancel) == DialogResult.Ok)
				{
					apiProcessor.ApiData.Clear();
					apiProcessor.ApiData.SaveDataToFile();
					MessageBox.Show("API 快取已刪除。");
					UpdateLabelTexts(apiProcessor.ApiData, guildCountLabel, sizeLabel);
				}
			};

			apiProcessor.Processed += OnGuildAdded;

			Closed += (sender, args) => apiProcessor.Processed -= OnGuildAdded;
		}

		private void UpdateLabelTexts(ApiData apiData, Label countLabel, Label cacheSizeLabel)
		{
			UpdateGuildCountText(apiData, countLabel);
			UpdateCacheSizeText(apiData, cacheSizeLabel);
		}

		private void UpdateGuildCountText(ApiData apiData, Label label)
		{
			string text = "沒有加載";

			if (apiData != null)
			{
				text = $"{apiData.CachedGuildCount}";
			}

			label.Text = text;
		}

		private void UpdateCacheSizeText(ApiData apiData, Label label)
		{
			string text = "沒有加載";

			if (apiData != null)
			{
				FileInfo fileInfo = apiData.GetCacheFileInfo();
				text = fileInfo.Exists ? $"{fileInfo.Length / 1000.0 / 1000.0:0.00} MB" : "沒有檔案";
			}

			label.Text = text;
		}
	}
}