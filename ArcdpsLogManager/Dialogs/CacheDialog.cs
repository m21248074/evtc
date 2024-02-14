using System;
using Eto.Drawing;
using Eto.Forms;

namespace GW2Scratch.ArcdpsLogManager.Dialogs
{
	public class CacheDialog : Dialog
	{
		public CacheDialog(ManagerForm managerForm)
		{
			Title = "日誌快取 - arcdps 日誌管理器";
			ClientSize = new Size(500, -1);
			var formLayout = new DynamicLayout();

			var item = new Button {Text = "關閉"};
			item.Click += (sender, args) => Close();
			PositiveButtons.Add(item);

			var deleteButton = new Button
			{
				Text = "刪除快取",
			};

			var pruneButton = new Button
			{
				Text = "修剪遺失的日誌",
			};

			var countLabel = new Label {Text = "沒有加載" };
			var unloadedCountLabel = new Label {Text = "沒有加載"};
			var sizeLabel = new Label {Text = "沒有檔案"};

			formLayout.BeginVertical(new Padding(10), new Size(0, 0));
			{
				formLayout.AddRow(new Label
				{
					Text = "日誌的處理內容保存在快取檔案中以節省時間。 " +
						   "您可以在此處刪除快取的結果以再次處理日誌，" +
						   "或刪除不再位於掃描目錄中的日誌的結果。",
					Wrap = WrapMode.Word
				});
			}
			formLayout.EndVertical();
			formLayout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				UpdateLabelTexts(managerForm, countLabel, unloadedCountLabel, sizeLabel);
				formLayout.AddRow(new Label {Text = "快取日誌總數:" }, countLabel);
				formLayout.AddRow(new Label {Text = "未加載快取日誌:" }, unloadedCountLabel);
				formLayout.AddRow(new Label {Text = "快取檔案大小:"}, sizeLabel);
			}
			formLayout.EndVertical();
			formLayout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				formLayout.AddRow(pruneButton);
				formLayout.AddRow(deleteButton);
			}
			formLayout.EndVertical();

			pruneButton.Click += (sender, args) =>
			{
				int unloadedLogs = managerForm.LogCache?.GetUnloadedLogCount(managerForm.LoadedLogs) ?? 0;
				if (MessageBox.Show(
					    $"確定修剪快取? 目前未加載日誌的 {unloadedLogs} 個結果將會被刪除。 " +
						"如果以後重新新增日誌，則必須重新處理。",
					    MessageBoxButtons.OKCancel) == DialogResult.Ok)
				{
					int pruned = managerForm.LogCache?.Prune(managerForm.LoadedLogs) ?? 0;
					MessageBox.Show($"快取已修剪， {pruned} 個結果被刪除。");
					managerForm.LogCache?.SaveToFile();
					UpdateLabelTexts(managerForm, countLabel, unloadedCountLabel, sizeLabel);
					managerForm.ReloadLogs();
				}
			};

			deleteButton.Click += (sender, args) =>
			{
				int logCount = managerForm.LogCache?.LogCount ?? 0;
				if (MessageBox.Show(
					    $"確定刪除快取? 所有 {logCount} 個快取日誌的結果都將被忘記。 " +
						"所有日誌都必須重新處理。",
					    MessageBoxButtons.OKCancel) == DialogResult.Ok)
				{
					managerForm.LogCache?.Clear();
					managerForm.LogCache?.SaveToFile();
					MessageBox.Show($"快取已刪除， {logCount} 個結果被刪除。");
					UpdateLabelTexts(managerForm, countLabel, unloadedCountLabel, sizeLabel);
					managerForm.ReloadLogs();
				}
			};

			UpdateLabelTexts(managerForm, countLabel, unloadedCountLabel, sizeLabel);

			Content = formLayout;
		}

		private void UpdateLabelTexts(ManagerForm managerForm, Label countLabel, Label unloadedCountLabel,
			Label cacheSizeLabel)
		{
			UpdateCacheCountText(managerForm, countLabel);
			UpdateUnloadedCacheCountLabel(managerForm, unloadedCountLabel);
			UpdateCacheSizeText(managerForm, cacheSizeLabel);
		}

		private void UpdateCacheCountText(ManagerForm managerForm, Label label)
		{
			string text = "沒有加載";

			if (managerForm.LogCache != null)
			{
				text = $"{managerForm.LogCache?.LogCount}";
			}

			label.Text = text;
		}

		private void UpdateUnloadedCacheCountLabel(ManagerForm managerForm, Label label)
		{
			string text = "沒有加載";

			if (managerForm.LogCache != null)
			{
				text = $"{managerForm.LogCache?.GetUnloadedLogCount(managerForm.LoadedLogs)}";
			}

			label.Text = text;
		}

		private void UpdateCacheSizeText(ManagerForm managerForm, Label label)
		{
			string text = "沒有加載";

			if (managerForm.LogCache != null)
			{
				var fileInfo = managerForm.LogCache.GetCacheFileInfo();
				text = fileInfo.Exists ? $"{fileInfo.Length / 1000.0 / 1000.0:0.00} MB" : "沒有檔案";
			}

			label.Text = text;
		}
	}
}