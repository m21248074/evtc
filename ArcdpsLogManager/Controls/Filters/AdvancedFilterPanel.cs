using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Filters;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Processing;
using System.Collections.Generic;

namespace GW2Scratch.ArcdpsLogManager.Controls.Filters
{
	public class AdvancedFilterPanel : DynamicLayout
	{
		private LogFilters Filters { get; }

		private readonly PlayerFilterPanel playerFilterPanel;

		public AdvancedFilterPanel(LogCache logCache, ApiData apiData, LogDataProcessor logProcessor,
			UploadProcessor uploadProcessor, ImageProvider imageProvider, ILogNameProvider logNameProvider,
			LogFilters filters)
		{
			Filters = filters;
			playerFilterPanel = ConstructPlayers(logCache, apiData, logProcessor, uploadProcessor, imageProvider,
				logNameProvider, filters);

			var compositionTab = new TabPage
			{
				Text = "團隊陣容",
				Content = ConstructSquadComposition(imageProvider, filters),
				Padding = new Padding(5),
			};
			var instabilityTab = new TabPage
			{
				Text = "霧鎖異變",
				Content = ConstructMistlockInstabilities(imageProvider, filters),
				Padding = new Padding(5),
			};
			var playerTab = new TabPage
			{
				Text = "玩家", Content = playerFilterPanel, Padding = new Padding(5),
			};
			var processingTab = new TabPage
			{
				Text = "處理狀態", Content = ConstructProcessingStatus(), Padding = new Padding(5),
			};

			void UpdateTabNames()
			{
				compositionTab.Text = "團隊陣容" + (filters.CompositionFilters.IsDefault ? "" : " •");
				instabilityTab.Text = "霧鎖異變" + (filters.InstabilityFilters.IsDefault ? "" : " •");
				playerTab.Text = "玩家" + (filters.PlayerFilters.IsDefault ? "" : " •");
				processingTab.Text = "處理狀態" + (AreProcessingFiltersDefault(filters) ? "" : " •");
			}

			var tabs = new TabControl();
			tabs.Pages.Add(compositionTab);
			tabs.Pages.Add(instabilityTab);
			tabs.Pages.Add(playerTab);
			tabs.Pages.Add(processingTab);
			UpdateTabNames();

			filters.PropertyChanged += (_, _) => UpdateTabNames();

			Add(tabs);
		}

		private Control ConstructSquadComposition(ImageProvider imageProvider, LogFilters filters)
		{
			return new SquadCompositionFilterPanel(imageProvider, filters);
		}

		private Control ConstructMistlockInstabilities(ImageProvider imageProvider, LogFilters filters)
		{
			return new InstabilityFilterPanel(imageProvider, filters);
		}

		private PlayerFilterPanel ConstructPlayers(LogCache logCache, ApiData apiData, LogDataProcessor logProcessor,
			UploadProcessor uploadProcessor, ImageProvider imageProvider, ILogNameProvider logNameProvider,
			LogFilters filters)
		{
			return new PlayerFilterPanel(logCache, apiData, logProcessor, uploadProcessor, imageProvider, logNameProvider, filters.PlayerFilters);
		}

		private Control ConstructProcessingStatus()
		{
			var unparsedCheckBox = new CheckBox { Text = "未處理" };
			unparsedCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowParseUnparsedLogs);
			var parsingCheckBox = new CheckBox { Text = "處理中" };
			parsingCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowParseParsingLogs);
			var parsedCheckBox = new CheckBox { Text = "已處理" };
			parsedCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowParseParsedLogs);
			var failedCheckBox = new CheckBox { Text = "處理失敗" };
			failedCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowParseFailedLogs);
			var layout = new DynamicLayout();
			layout.BeginVertical(spacing: new Size(5, 5));
			{
				layout.Add(unparsedCheckBox);
				layout.Add(parsingCheckBox);
				layout.Add(parsedCheckBox);
				layout.Add(failedCheckBox);
				layout.Add(null);
			}
			layout.EndVertical();
			return layout;
		}

		private static bool AreProcessingFiltersDefault(LogFilters filters)
		{
			return filters.ShowParseUnparsedLogs && filters.ShowParseParsingLogs && filters.ShowParseParsedLogs &&
			       filters.ShowParseFailedLogs;
		}

		public static int CountNonDefaultAdvancedFilters(LogFilters filters)
		{
			int count = 0;
			if (!filters.CompositionFilters.IsDefault) { count += 1; }
			if (!filters.InstabilityFilters.IsDefault) { count += 1; }
			if (!filters.PlayerFilters.IsDefault) { count += 1; }

			if (!AreProcessingFiltersDefault(filters))
			{
				// We count the whole processing status section as one.
				count += 1;
			}

			return count;
		}

		public void UpdateLogs(IReadOnlyList<LogData> logs)
		{
			playerFilterPanel.UpdateLogs(logs);
		}
	}
}