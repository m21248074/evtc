using System.Collections.Generic;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Logs.Updates;
using GW2Scratch.ArcdpsLogManager.Processing;

namespace GW2Scratch.ArcdpsLogManager.Dialogs
{
	public class ProcessingUpdateDialog : Dialog
	{
		public ProcessingUpdateDialog(LogDataProcessor logProcessor, IReadOnlyList<LogUpdateList> updates)
		{
			Title = "管理器更新 - arcdps 日誌管理器";
			ClientSize = new Size(500, -1);
			var layout = new DynamicLayout();
			Content = layout;

			layout.BeginVertical(new Padding(10), new Size(10, 20));
			{
				layout.BeginVertical(spacing: new Size(0, 0));
				{
					layout.AddRow(new Label
					{
						Width = 600,
						Text = "新版本的日誌管理器改進了一些日誌的處理。 " +
							   "必須再次處理它們才能正確更新資料。 " +
							   "下面列出了受影響的日誌。 您想現在更新嗎?",
						Wrap = WrapMode.Word
					});
					layout.AddRow(null);
				}
				layout.EndVertical();
				layout.AddRow(ConstructGridView(updates));
			}
			layout.EndVertical();

			var later = new Button {Text = "稍後"};
			var yes = new Button {Text = "確認"};
			later.Click += (sender, args) => Close();
			yes.Click += (sender, args) =>
			{
				Task.Run(() =>
				{
					foreach (var update in updates)
					{
						foreach (var log in update.UpdateableLogs)
						{
							logProcessor.Schedule(log);
						}
					}
				});
				Close();
			};

			AbortButton = later;
			DefaultButton = yes;
			PositiveButtons.Add(yes);
			NegativeButtons.Add(later);
		}

		private GridView<LogUpdateList> ConstructGridView(IReadOnlyList<LogUpdateList> updates)
		{
			var grid = new GridView<LogUpdateList>();
			grid.Columns.Add(new GridColumn
			{
				HeaderText = "日誌數",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogUpdateList, string>(x => x.UpdateableLogs.Count.ToString())
				}
			});
			grid.Columns.Add(new GridColumn
			{
				HeaderText = "原因",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogUpdateList, string>(x => x.Update.Reason)
				}
			});
			grid.DataStore = updates;
			grid.SelectedRow = -1;
			grid.Height = 300;

			return grid;
		}
	}
}