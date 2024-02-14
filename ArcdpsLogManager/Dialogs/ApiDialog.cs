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
			Title = "API ��� - arcdps ��x�޲z��";
			ClientSize = new Size(500, -1);
			var formLayout = new DynamicLayout();
			Content = formLayout;

			var item = new Button {Text = "����"};
			item.Click += (sender, args) => Close();
			PositiveButtons.Add(item);

			var deleteButton = new Button
			{
				Text = "�R���֨�",
			};

			var enableCheckbox = new CheckBox {Text = "�ϥοE��2 API", Checked = Settings.UseGW2Api};
			enableCheckbox.CheckedChanged += (sender, args) => Settings.UseGW2Api = enableCheckbox.Checked ?? false;

			var guildCountLabel = new Label {Text = "�S���[��" };
			var sizeLabel = new Label {Text = "�S���ɮ�"};

			UpdateLabelTexts(apiProcessor.ApiData, guildCountLabel, sizeLabel);

			formLayout.BeginVertical(new Padding(10), new Size(0, 0));
			{
				formLayout.AddRow(new Label
				{
					Text = "���|�W�٩M���ҥ����q�E��2�x�� API �[���A�]�� EVTC ��x�ȥ]�t GUID �ȡC",
					Wrap = WrapMode.Word
				});
			}
			formLayout.EndVertical();
			formLayout.BeginVertical(new Padding(10), new Size(5, 5));
			{
				formLayout.AddRow(new Label {Text = "���|��:"}, guildCountLabel);
				formLayout.AddRow(new Label {Text = "�֨��ɮפj�p:"}, sizeLabel);
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
					    $"�T�w�R�� API �֨�? �Ҧ� {apiProcessor.ApiData.CachedGuildCount} �Ӥ��|�� API ��Ƴ��N�Q�R���C " +
						"���s�R�W�����|�W��/���ҷ|��s�A���Ѵ������|��ƱN�L�k�A�˯��C " +
						"�z�i��ݭn���s�Ұʵ{���~���s�Ҧ����e�C",
					    MessageBoxButtons.OKCancel) == DialogResult.Ok)
				{
					apiProcessor.ApiData.Clear();
					apiProcessor.ApiData.SaveDataToFile();
					MessageBox.Show("API �֨��w�R���C");
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
			string text = "�S���[��";

			if (apiData != null)
			{
				text = $"{apiData.CachedGuildCount}";
			}

			label.Text = text;
		}

		private void UpdateCacheSizeText(ApiData apiData, Label label)
		{
			string text = "�S���[��";

			if (apiData != null)
			{
				FileInfo fileInfo = apiData.GetCacheFileInfo();
				text = fileInfo.Exists ? $"{fileInfo.Length / 1000.0 / 1000.0:0.00} MB" : "�S���ɮ�";
			}

			label.Text = text;
		}
	}
}