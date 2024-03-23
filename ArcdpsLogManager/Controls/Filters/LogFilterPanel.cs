using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Filters;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Processing;

namespace GW2Scratch.ArcdpsLogManager.Controls.Filters
{
	public class LogFilterPanel : DynamicLayout
	{
		private LogFilters Filters { get; }
		private IReadOnlyList<LogData> Logs { get; set; } = new List<LogData>();

		private readonly LogEncounterFilterTree encounterTree;
		private bool enabled = true;

		private event EventHandler<IReadOnlyList<LogData>> LogsUpdated;
		private event EventHandler EnabledChanged;

		public bool Enabled
		{
			get => enabled;
			set
			{
				if (enabled != value)
				{
					enabled = value;
					EnabledChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		private void BindEnabled(Control control)
		{
			control.Enabled = Enabled;
			EnabledChanged += (_, _) => control.Enabled = Enabled;
		}

		public LogFilterPanel(LogCache logCache, ApiData apiData, LogDataProcessor logProcessor,
			UploadProcessor uploadProcessor, ImageProvider imageProvider, ILogNameProvider logNameProvider,
			LogFilters filters)
		{
			Filters = filters;

			encounterTree = new LogEncounterFilterTree(imageProvider, Filters, logNameProvider);
			BindEnabled(encounterTree);
			

			var successCheckBox = new CheckBox {Text = "成功"};
			successCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowSuccessfulLogs);
			BindEnabled(successCheckBox);
			var failureCheckBox = new CheckBox {Text = "失敗"};
			failureCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowFailedLogs);
			BindEnabled(failureCheckBox);
			var unknownCheckBox = new CheckBox {Text = "未知"};
			unknownCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowUnknownLogs);
			BindEnabled(unknownCheckBox);

			var normalModeCheckBox = new CheckBox {Text = "普通", ToolTip = "普通模式"};
			normalModeCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowNormalModeLogs);
			BindEnabled(normalModeCheckBox);
			var emboldenedCheckBox = new CheckBox {Text = "膽量", ToolTip = "膽量模式，無論疊了多少層的膽量"};
			emboldenedCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowEmboldenedModeLogs);
			BindEnabled(emboldenedCheckBox);
			var challengeModeCheckBox = new CheckBox {Text = "挑戰", ToolTip = "挑戰模式" };
			challengeModeCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowChallengeModeLogs);
			BindEnabled(challengeModeCheckBox);
			var legendaryModeCheckBox = new CheckBox {Text = "傳奇", ToolTip = "傳奇挑戰模式 (Temple of Febe...)"};
			legendaryModeCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowLegendaryChallengeModeLogs);
			BindEnabled(legendaryModeCheckBox);

			var nonFavoritesCheckBox = new CheckBox {Text = "☆ 非最愛"};
			nonFavoritesCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowNonFavoriteLogs);
			BindEnabled(nonFavoritesCheckBox);
			var favoritesCheckBox = new CheckBox {Text = "★ 最愛"};
			favoritesCheckBox.CheckedBinding.Bind(this, x => x.Filters.ShowFavoriteLogs);
			BindEnabled(favoritesCheckBox);

			// TODO: This is currently only a one-way binding
			var tagText = new TextBox();
			tagText.TextChanged += (source, args) =>
			{
				var tags = tagText.Text.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(tag => tag.Trim()).ToList();
				filters.RequiredTags = tags;
			};
			BindEnabled(tagText);

			var startDateTimePicker = new DateTimePicker {Mode = DateTimePickerMode.DateTime};
			startDateTimePicker.ValueBinding.Bind(this, x => x.Filters.MinDateTime);
			BindEnabled(startDateTimePicker);
			var endDateTimePicker = new DateTimePicker {Mode = DateTimePickerMode.DateTime};
			endDateTimePicker.ValueBinding.Bind(this, x => x.Filters.MaxDateTime);
			BindEnabled(endDateTimePicker);

			var lastDayButton = new Button {Text = "過去 24 小時" };
			lastDayButton.Click += (sender, args) =>
			{
				// We round the time to the previous minute as the interface for picking seconds
				// is hard to use on some platforms.
				var newTime = DateTime.Now - TimeSpan.FromDays(1);
				newTime -= TimeSpan.FromSeconds(newTime.Second);
				startDateTimePicker.Value = newTime;
				endDateTimePicker.Value = null;
			};
			BindEnabled(lastDayButton);
			
			var lastWeekButton = new Button {Text = "過去 7 天"};
			lastWeekButton.Click += (sender, args) =>
			{
				// We round the time to the previous minute as the interface for picking seconds
				// is hard to use on some platforms.
				var newTime = DateTime.Now - TimeSpan.FromDays(7);
				newTime -= TimeSpan.FromSeconds(newTime.Second);
				startDateTimePicker.Value = newTime;
				endDateTimePicker.Value = null;
			};
			BindEnabled(lastWeekButton);

			var allTimeButton = new Button {Text = "所有時間"};
			allTimeButton.Click += (sender, args) => {
				startDateTimePicker.Value = null;
				endDateTimePicker.Value = null;
			};
			BindEnabled(allTimeButton);


			var advancedFiltersButton = new Button {Text = "進階篩選器"};
			BindEnabled(advancedFiltersButton);

			advancedFiltersButton.Click += (sender, args) =>
			{
				var advancedFilterPanel = new AdvancedFilterPanel(logCache, apiData, logProcessor, uploadProcessor,
					imageProvider, logNameProvider, filters);
				
				var form = new Form
				{
					Title = "進階篩選器 - arcdps 日誌管理器",
					Content = advancedFilterPanel,
				};
				
				// Make sure we update the logs within the advanced filter form now and with each update later.
				advancedFilterPanel.UpdateLogs(Logs);
				var updateLogs = new EventHandler<IReadOnlyList<LogData>>((_, logs) => advancedFilterPanel.UpdateLogs(logs));
				LogsUpdated += updateLogs;
				form.Closed += (_, _) => LogsUpdated -= updateLogs;
				
				form.Show();
			};
			filters.PropertyChanged += (sender, args) =>
			{
				var nonDefaultCount = AdvancedFilterPanel.CountNonDefaultAdvancedFilters(filters);
				if (nonDefaultCount > 0)
				{
					advancedFiltersButton.Text = $"進階篩選器 ({nonDefaultCount} 個設定)";
				}
				else
				{
					advancedFiltersButton.Text = "進階篩選器";
				}
			};

			BeginVertical(new Padding(0, 0, 0, 4), spacing: new Size(4, 4));
			{
				BeginGroup("結果", new Padding(4, 0, 4, 2), spacing: new Size(6, 0));
				{
					BeginHorizontal();
					{
						Add(successCheckBox);
						Add(failureCheckBox);
						Add(unknownCheckBox);
					}
					EndHorizontal();
				}
				EndGroup();
				BeginGroup("模式", new Padding(4, 0, 4, 2), spacing: new Size(6, 0));
				{
					BeginHorizontal();
					{
						Add(normalModeCheckBox);
						Add(emboldenedCheckBox);
					}
					EndHorizontal();
					BeginHorizontal();
					{
						Add(challengeModeCheckBox);
						Add(legendaryModeCheckBox);
					}
					EndHorizontal();
				}
				EndGroup();
				BeginGroup("日期", new Padding(4, 0, 4, 2), spacing: new Size(4, 4));
				{
					BeginVertical(spacing: new Size(4, 2));
					{
						BeginHorizontal();
						{
							Add(new Label {Text = "從", VerticalAlignment = VerticalAlignment.Center});
							Add(startDateTimePicker);
							Add(null, xscale: true);
						}
						EndHorizontal();
						BeginHorizontal();
						{
							Add(new Label {Text = "到", VerticalAlignment = VerticalAlignment.Center});
							Add(endDateTimePicker);
							Add(null, xscale: true);
						}
						EndHorizontal();
					}
					EndVertical();
					BeginVertical(spacing: new Size(4, 0));
					{
						BeginHorizontal();
						{
							Add(allTimeButton, xscale: false);
							Add(lastWeekButton, xscale: false);
							Add(lastDayButton, xscale: false);
							Add(null, xscale: true);
						}
						EndHorizontal();
					}
					EndVertical();
				}
				EndGroup();
				BeginGroup("最愛", new Padding(4, 0, 4, 2), spacing: new Size(6, 4));
				{
					BeginHorizontal();
					{
						Add(nonFavoritesCheckBox);
						Add(favoritesCheckBox);
					}
					EndHorizontal();
				}
				EndGroup();
				BeginGroup("標籤 (逗號分隔)", new Padding(4, 0, 4, 2), spacing: new Size(6, 4));
				{
					BeginVertical();
					{
						BeginHorizontal();
						{
							Add(tagText);
						}
						EndHorizontal();
					}
					EndVertical();
				}
				EndGroup();

				Add(encounterTree, yscale: true);
				Add(advancedFiltersButton);
			}
			EndVertical();
		}

		/// <summary>
		/// Needs to be called to update selections that depend on the available logs, such as
		/// filtering by encounters.
		/// </summary>
		/// <param name="logs">Logs to be available for filtering.</param>
		public void UpdateLogs(IReadOnlyList<LogData> logs)
		{
			Logs = logs;
			encounterTree.UpdateLogs(logs);
			LogsUpdated?.Invoke(this, logs);
		}
	}
}