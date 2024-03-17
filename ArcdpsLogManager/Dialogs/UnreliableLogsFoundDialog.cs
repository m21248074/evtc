using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.EVTCAnalytics.GameData.Encounters;
using System.Collections.Generic;
using System.Linq;

namespace GW2Scratch.ArcdpsLogManager.Dialogs
{
	class UnreliableLogsFoundDialog : Dialog
	{
		// This is a bit of a hack, ideally we would have reliability information within
		// EVTCAnalytics (or, even better, no unreliable detections).
		private static Encounter[] UnreliableEncounters { get; } =
		{
			Encounter.BanditTrio, Encounter.Artsariiv, Encounter.Arkk, Encounter.Other
		};

		private bool Confirmed { get; set; }

		public UnreliableLogsFoundDialog(IEnumerable<LogData> logs, ILogNameProvider nameProvider)
		{
			Title = "警告: 不可靠的日誌結果";
			ShowInTaskbar = true;
			var layout = new DynamicLayout();
			Content = layout;

			var confirmButton = new Button { Text = "執意刪除" };
			PositiveButtons.Add(confirmButton);

			confirmButton.Click += (_, _) =>
			{
				Confirmed = true;
				Close();
			};

			AbortButton = new Button { Text = "取消" };
			NegativeButtons.Add(AbortButton);
			AbortButton.Click += (_, _) =>
			{
				Close();
			};

			Padding = new Padding(10);
			layout.Spacing = new Size(5, 5);
			layout.AddRow(new Label
			{
				Text = "目前，有些選定的日誌在極少數情況下的成功檢測並不可靠。\n" +
					   "即使成功，日誌也很可能會顯示失敗。",
				Wrap = WrapMode.Word,
				Width = 500,
			});
			layout.BeginGroup("受影響的遭遇戰", padding: new Padding(5));
			{
				foreach (var encounter in GetUnreliableEncounterNames(logs, nameProvider))
				{
					layout.AddRow($" • {encounter}");
				}
			}
			layout.EndGroup();

			// This is required on the WPF platform due to a bug in Eto.

			// If not done, the group with affected encounters doesn't get enough space
			// and at least one row will be missing.
			Shown += (_, _) =>
			{
				AutoSize = false;
				AutoSize = true;
			};
		}

		public bool ShowDialog(Control owner)
		{
			this.ShowModal(owner);

			return Confirmed;
		}

		public static bool IsApplicable(IEnumerable<LogData> logs)
		{
			return GetPresentUnreliableLogs(logs).Any();
		}

		private static IEnumerable<LogData> GetPresentUnreliableLogs(IEnumerable<LogData> logs)
		{
			return logs.Where(x => UnreliableEncounters.Contains(x.Encounter));
		}

		private static IEnumerable<string> GetUnreliableEncounterNames(IEnumerable<LogData> logs,
			ILogNameProvider nameProvider)
		{
			var unreliableLogs = GetPresentUnreliableLogs(logs);
			return unreliableLogs.Select(nameProvider.GetName).Distinct().OrderBy(x => x);
		}
	}
}