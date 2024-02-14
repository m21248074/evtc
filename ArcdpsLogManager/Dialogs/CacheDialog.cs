using System;
using Eto.Drawing;
using Eto.Forms;

namespace GW2Scratch.ArcdpsLogManager.Dialogs
{
	public class CacheDialog : Dialog
	{
		public CacheDialog(ManagerForm managerForm)
		{
			Title = "��x�֨� - arcdps ��x�޲z��";
			ClientSize = new Size(500, -1);
			var formLayout = new DynamicLayout();

			var item = new Button {Text = "����"};
			item.Click += (sender, args) => Close();
			PositiveButtons.Add(item);

			var deleteButton = new Button
			{
				Text = "�R���֨�",
			};

			var pruneButton = new Button
			{
				Text = "�װſ򥢪���x",
			};

			var countLabel = new Label {Text = "�S���[��" };
			var unloadedCountLabel = new Label {Text = "�S���[��"};
			var sizeLabel = new Label {Text = "�S���ɮ�"};

			formLayout.BeginVertical(new Padding(10), new Size(0, 0));
			{
				formLayout.AddRow(new Label
				{
					Text = "��x���B�z���e�O�s�b�֨��ɮפ��H�`�ٮɶ��C " +
						   "�z�i�H�b���B�R���֨������G�H�A���B�z��x�A" +
						   "�ΧR�����A��󱽴y�ؿ�������x�����G�C",
					Wrap = WrapMode.Word
				});
			}
			formLayout.EndVertical();
			formLayout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				UpdateLabelTexts(managerForm, countLabel, unloadedCountLabel, sizeLabel);
				formLayout.AddRow(new Label {Text = "�֨���x�`��:" }, countLabel);
				formLayout.AddRow(new Label {Text = "���[���֨���x:" }, unloadedCountLabel);
				formLayout.AddRow(new Label {Text = "�֨��ɮפj�p:"}, sizeLabel);
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
					    $"�T�w�װŧ֨�? �ثe���[����x�� {unloadedLogs} �ӵ��G�N�|�Q�R���C " +
						"�p�G�H�᭫�s�s�W��x�A�h�������s�B�z�C",
					    MessageBoxButtons.OKCancel) == DialogResult.Ok)
				{
					int pruned = managerForm.LogCache?.Prune(managerForm.LoadedLogs) ?? 0;
					MessageBox.Show($"�֨��w�װšA {pruned} �ӵ��G�Q�R���C");
					managerForm.LogCache?.SaveToFile();
					UpdateLabelTexts(managerForm, countLabel, unloadedCountLabel, sizeLabel);
					managerForm.ReloadLogs();
				}
			};

			deleteButton.Click += (sender, args) =>
			{
				int logCount = managerForm.LogCache?.LogCount ?? 0;
				if (MessageBox.Show(
					    $"�T�w�R���֨�? �Ҧ� {logCount} �ӧ֨���x�����G���N�Q�ѰO�C " +
						"�Ҧ���x���������s�B�z�C",
					    MessageBoxButtons.OKCancel) == DialogResult.Ok)
				{
					managerForm.LogCache?.Clear();
					managerForm.LogCache?.SaveToFile();
					MessageBox.Show($"�֨��w�R���A {logCount} �ӵ��G�Q�R���C");
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
			string text = "�S���[��";

			if (managerForm.LogCache != null)
			{
				text = $"{managerForm.LogCache?.LogCount}";
			}

			label.Text = text;
		}

		private void UpdateUnloadedCacheCountLabel(ManagerForm managerForm, Label label)
		{
			string text = "�S���[��";

			if (managerForm.LogCache != null)
			{
				text = $"{managerForm.LogCache?.GetUnloadedLogCount(managerForm.LoadedLogs)}";
			}

			label.Text = text;
		}

		private void UpdateCacheSizeText(ManagerForm managerForm, Label label)
		{
			string text = "�S���[��";

			if (managerForm.LogCache != null)
			{
				var fileInfo = managerForm.LogCache.GetCacheFileInfo();
				text = fileInfo.Exists ? $"{fileInfo.Length / 1000.0 / 1000.0:0.00} MB" : "�S���ɮ�";
			}

			label.Text = text;
		}
	}
}