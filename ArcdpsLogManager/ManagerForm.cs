﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Analytics;
using GW2Scratch.ArcdpsLogManager.Collections;
using GW2Scratch.ArcdpsLogManager.Commands;
using GW2Scratch.ArcdpsLogManager.Configuration;
using GW2Scratch.ArcdpsLogManager.Controls;
using GW2Scratch.ArcdpsLogManager.Controls.Filters;
using GW2Scratch.ArcdpsLogManager.Dialogs;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Caching;
using GW2Scratch.ArcdpsLogManager.Logs.Filters;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Logs.Updates;
using GW2Scratch.ArcdpsLogManager.Processing;
using GW2Scratch.ArcdpsLogManager.Sections;
using GW2Scratch.ArcdpsLogManager.Timing;
using GW2Scratch.ArcdpsLogManager.Updates;
using GW2Scratch.ArcdpsLogManager.Uploads;
using GW2Scratch.EVTCAnalytics;
using GW2Scratch.EVTCAnalytics.Events;
using GW2Scratch.EVTCAnalytics.GameData;
using GW2Scratch.EVTCAnalytics.Processing;
using Gw2Sharp;

namespace GW2Scratch.ArcdpsLogManager
{
	public sealed class ManagerForm : Form
	{
		private static readonly TimeSpan LogCacheAutoSavePeriod = TimeSpan.FromSeconds(60);
		private readonly Cooldown gridRefreshCooldown = new Cooldown(TimeSpan.FromSeconds(10));
		private readonly Cooldown filterRefreshCooldown = new Cooldown(TimeSpan.FromSeconds(10));

		private ProgramUpdateChecker ProgramUpdateChecker { get; } = new ProgramUpdateChecker("http://gw2scratch.com/releases/manager.json");
		private ImageProvider ImageProvider { get; } = new ImageProvider();
		private LogFinder LogFinder { get; } = new LogFinder();

		private LogAnalytics LogAnalytics { get; } = new LogAnalytics(
			new EVTCParser() { SinglePassFilteringOptions = { PruneForEncounterData = true, ExtraRequiredEventTypes = new [] {typeof(AgentTagEvent) }} },
			new LogProcessor(),
			new FractalInstabilityDetector(),
			log => new LogAnalyzer(log)
		);

		private ApiProcessor ApiProcessor { get; }
		private UploadProcessor UploadProcessor { get; }
		private LogDataProcessor LogDataProcessor { get; }
		public LogCompressionProcessor LogCompressionProcessor { get; }
		private ILogNameProvider LogNameProvider { get; }
		private LogDataUpdater LogDataUpdater { get; } = new LogDataUpdater();
		private LogCacheAutoSaver LogCacheAutoSaver { get; }

		private readonly BulkObservableCollection<LogData> logs = new BulkObservableCollection<LogData>();
		private readonly FilterCollection<LogData> logsFiltered;

		private CancellationTokenSource logLoadTaskTokenSource = null;

		public IEnumerable<LogData> LoadedLogs => logs;
		public LogCache LogCache { get; }
		private ApiData ApiData { get; }
		private LogFilters Filters { get; }

		private readonly List<FileSystemWatcher> fileSystemWatchers = new List<FileSystemWatcher>();

		public ManagerForm(LogCache logCache, ApiData apiData)
		{
			LogCache = logCache ?? throw new ArgumentNullException(nameof(logCache));
			ApiData = apiData ?? throw new ArgumentNullException(nameof(apiData));

			// Background processors
			var dpsReportUploader = new DpsReportUploader(Settings.DpsReportUploadDetailedWvw);
			UploadProcessor = new UploadProcessor(dpsReportUploader, LogCache);
			ApiProcessor = new ApiProcessor(ApiData, new Gw2Client());
			LogDataProcessor = new LogDataProcessor(LogCache, ApiProcessor, LogAnalytics);
			LogCompressionProcessor = new LogCompressionProcessor(LogCache);
			LogNameProvider = new TranslatedLogNameProvider(GameLanguage.English);
			LogCacheAutoSaver = LogCacheAutoSaver.StartNew(logCache, LogCacheAutoSavePeriod);

			LogDataProcessor.StoppingWithError += (sender, args) =>
			{
				Application.Instance.InvokeAsync(() => MessageBox.Show(this,
					$"The background processor for logs has failed critically. " +
					$"Please report the following error:\n\nException: {args.Exception}", "Error", MessageBoxType.Error));
			};
			
			ApiProcessor.StoppingWithError += (sender, args) =>
			{
				Application.Instance.InvokeAsync(() => MessageBox.Show(this,
					$"The background processor for API requests has failed critically. " +
					$"Please report the following error:\n\nException: {args.Exception}", "Error", MessageBoxType.Error));
			};
			
			UploadProcessor.StoppingWithError += (sender, args) =>
			{
				Application.Instance.InvokeAsync(() => MessageBox.Show(this,
					$"The background processor for log uploads has failed critically. " +
					$"Please report the following error:\n\nException: {args.Exception}", "Error", MessageBoxType.Error));
			};

			Filters = new LogFilters(LogNameProvider, new SettingsFilters());
			Filters.PropertyChanged += (sender, args) => logsFiltered.Refresh();

			if (Settings.UseGW2Api)
			{
				ApiProcessor.StartBackgroundTask();
			}

			Settings.UseGW2ApiChanged += (sender, args) =>
			{
				if (Settings.UseGW2Api)
				{
					ApiProcessor.StartBackgroundTask();
				}
				else
				{
					ApiProcessor.StopBackgroundTask();
				}
			};

			Settings.DpsReportDomainChanged += (sender, args) => { dpsReportUploader.Domain = Settings.DpsReportDomain; };
			Settings.DpsReportUploadDetailedWvwChanged += (sender, args) => { dpsReportUploader.UploadDetailedWvw = Settings.DpsReportUploadDetailedWvw; };

			// Form layout
			Icon = Resources.GetProgramIcon();
			Title = "arcdps 日誌管理器";
			ClientSize = new Size(1300, 768);
			var formLayout = new DynamicLayout();
			Content = formLayout;

			Menu = ConstructMenuBar();

			formLayout.BeginVertical(new Padding(5), yscale: false);
			{
				formLayout.Add(ConstructMainSplitter(), yscale: true);
				formLayout.Add(ConstructStatusPanel());
			}
			formLayout.EndVertical();

			// Event handlers
			ApiProcessor.Processed += (sender, args) =>
			{
				bool last = args.CurrentScheduledItems == 0;

				if (last)
				{
					ApiData.SaveDataToFile();
				}
			};

			Settings.LogRootPathChanged += (sender, args) => Application.Instance.Invoke(ReloadLogs);

			Closing += (sender, args) =>
			{
				if (LogCache?.ChangedSinceLastSave ?? false)
				{
					LogCache?.SaveToFile();
				}

				ApiData?.SaveDataToFile();
			};
			LogSearchFinished += (sender, args) =>
			{
				var updates = LogDataUpdater.GetUpdates(logs).ToList();
				if (updates.Count > 0)
				{
					new ProcessingUpdateDialog(LogDataProcessor, updates).ShowModal(this);
				}
			};

			// Collection initialization
			logsFiltered = new FilterCollection<LogData>(logs);
			logsFiltered.CollectionChanged += (sender, args) => FilteredLogsUpdated?.Invoke(this, EventArgs.Empty);
			logsFiltered.Filter = Filters.FilterLog;
			LogCollectionsInitialized?.Invoke(this, EventArgs.Empty);
			LogDataProcessor.Processed += (sender, args) =>
			{
				bool last = args.CurrentScheduledItems == 0;
				if (last)
				{
					Application.Instance.AsyncInvoke(logsFiltered.Refresh);
				}
			};

			Shown += (sender, args) => ReloadLogs();
			Shown += (sender, args) => CheckUpdates();
		}

		private void CheckUpdates()
		{
			if (!Settings.CheckForUpdates)
			{
				return;
			}

			Task.Run(ProgramUpdateChecker.CheckUpdates).ContinueWith(t =>
			{
				var release = t.Result;
				if (release != null)
				{
					Application.Instance.Invoke(() => new ProgramUpdateDialog(release).ShowModal(this));
				}
			});
		}

		private Splitter ConstructMainSplitter()
		{
			var filters = ConstructLogFilters();
			var tabs = ConstructMainTabControl();

			var sidebar = new Panel {Content = filters, Padding = new Padding(0, 0, 4, 2)};
			var filterPage = new TabPage {Text = "Filters", Visible = false};
			tabs.Pages.Insert(0, filterPage);

			var mainSplitter = new Splitter
			{
				Orientation = Orientation.Horizontal,
				Panel1 = sidebar,
				Panel2 = tabs,
				Position = 320
			};

			// This is a workaround for an Eto 2.4 bug.
			// See comment below when value is checked.
			bool updatingSidebar = true;
			void UpdateSidebarFromSetting()
			{
				updatingSidebar = true;
				// The filters from the sidebar are moved into
				// their own tab when the sidebar is collapsed
				if (Settings.ShowFilterSidebar)
				{
					filterPage.Content = null;
					filterPage.Visible = false;
					sidebar.Content = filters;
					// This may be called when enlarging the sidebar, we don't want to change the size in that case.
					if (mainSplitter.Position == 0)
					{
						mainSplitter.Position = 300;
					}
				}
				else
				{
					sidebar.Content = null;
					mainSplitter.Position = 0;
					filterPage.Content = filters;
					filterPage.Visible = true;
				}

				updatingSidebar = false;
			}

			UpdateSidebarFromSetting();
			Settings.ShowFilterSidebarChanged += (sender, args) => UpdateSidebarFromSetting();
			Settings.LogRootPathChanged += (sender, args) => SetupFileSystemWatchers();

			mainSplitter.PositionChanged += (sender, args) =>
			{
				if (updatingSidebar)
				{
					// This is a workaround for an Eto 2.4 bug on WPF where setting mainSplitter.Position = 300
					// invokes this event while Position is still set to 0. This results in an infinite loop and
					// ultimately a stack-overflow exception.
					// TODO: Remove on Eto 2.5 if fixed.
					return;
				}

				if (mainSplitter.Position <= 10)
				{
					Settings.ShowFilterSidebar = false;
				}
				else
				{
					Settings.ShowFilterSidebar = true;
				}
			};

			return mainSplitter;
		}

		private DynamicLayout ConstructStatusPanel()
		{
			// Log count label
			var logCountLabel = new Label();
			LogSearchStarted += (sender, args) => logCountLabel.Text = "找尋日誌中...";
			LogSearchFinished += (sender, args) => logCountLabel.Text = $"找到 {logs.Count} 條日誌。";
			FilteredLogsUpdated += (sender, args) =>
			{
				int fullCount = logs.Count;
				int filteredCount = logsFiltered.Count;
				if (filteredCount != fullCount)
				{
					logCountLabel.Text = $"找到 {fullCount} 條日誌，顯示 {filteredCount} 條。";
				} else {
					logCountLabel.Text = $"找到 {fullCount} 條日誌。";
				}
			};
			
			// Processing label
			var processingLabel = new Label();

			void UpdateProcessingLabel(object sender, BackgroundProcessorEventArgs args)
			{
				Application.Instance.AsyncInvoke(() =>
				{
					bool finished = args.CurrentScheduledItems == 0;
					processingLabel.Text = finished
						? ""
						: $"Processing logs: {args.TotalProcessedItems}/{args.TotalScheduledItems}";
				});
			}

			LogDataProcessor.Processed += UpdateProcessingLabel;
			LogDataProcessor.Scheduled += UpdateProcessingLabel;
			LogDataProcessor.Unscheduled += UpdateProcessingLabel;

			// Upload state label
			var uploadLabel = new Label();

			void UpdateUploadLabel(object sender, BackgroundProcessorEventArgs args)
			{
				Application.Instance.AsyncInvoke(() =>
				{
					bool finished = args.CurrentScheduledItems == 0;
					uploadLabel.Text = finished
						? ""
						: $"Uploading: {args.TotalProcessedItems}/{args.TotalScheduledItems}";
				});
			}

			UploadProcessor.Processed += UpdateUploadLabel;
			UploadProcessor.Scheduled += UpdateUploadLabel;
			UploadProcessor.Unscheduled += UpdateUploadLabel;

			// API state label
			var apiLabel = new Label();

			void UpdateApiLabel(object sender, BackgroundProcessorEventArgs args)
			{
				Application.Instance.AsyncInvoke(() =>
				{
					bool finished = args.CurrentScheduledItems == 0;
					apiLabel.Text = finished ? "" : $"Downloading guild data: {args.TotalProcessedItems}/{args.TotalScheduledItems}";
				});
			}

			ApiProcessor.Processed += UpdateApiLabel;
			ApiProcessor.Scheduled += UpdateApiLabel;
			ApiProcessor.Unscheduled += UpdateApiLabel;

			// Layout of the status bar
			var layout = new DynamicLayout();
			layout.BeginHorizontal();
			{
				layout.Add(logCountLabel, xscale: true);
				layout.Add(processingLabel, xscale: true);
				layout.Add(uploadLabel, xscale: true);
				layout.Add(apiLabel, xscale: true);
			}
			layout.EndHorizontal();

			return layout;
		}

		private LogFilterPanel ConstructLogFilters()
		{
			var filterPanel = new LogFilterPanel(LogCache, ApiData, LogDataProcessor, UploadProcessor, ImageProvider, LogNameProvider, Filters);
			LogCollectionsInitialized += (sender, args) => logs.CollectionChanged += (s, a) => { filterPanel.UpdateLogs(logs); };
			LogDataProcessor.Processed += (sender, args) =>
			{
				bool last = args.CurrentScheduledItems == 0;

				if (last || filterRefreshCooldown.TryUse(DateTime.Now))
				{
					Application.Instance.AsyncInvoke(() => filterPanel.UpdateLogs(logs));
				}
			};
			// Required in case the map name API request takes longer than creation of the panel.
			LogNameProvider.MapNamesUpdated += (_, _) => filterPanel.UpdateLogs(logs);

			return filterPanel;
		}

		private MenuBar ConstructMenuBar()
		{
			var updateMenuItem = new ButtonMenuItem {Text = "&使用過期資料更新日誌" };
			updateMenuItem.Click += (sender, args) =>
			{
				new ProcessingUpdateDialog(LogDataProcessor, LogDataUpdater.GetUpdates(logs).ToList()).ShowModal(this);
			};
			LogSearchFinished += (sender, args) => { updateMenuItem.Enabled = LogDataUpdater.GetUpdates(logs).Any(); };
			LogDataProcessor.Processed += (sender, args) =>
			{
				if (args.CurrentScheduledItems == 0)
				{
					bool updatesFound = LogDataUpdater.GetUpdates(logs).Any();
					Application.Instance.AsyncInvoke(() => updateMenuItem.Enabled = updatesFound);
				}
			};

			var compressLogsItem = new ButtonMenuItem {Text = "&壓縮日誌" };
			compressLogsItem.Click += (sender, args) => { new CompressDialog(logs.ToList(), LogCompressionProcessor).ShowModal(); };

			var logCacheMenuItem = new ButtonMenuItem {Text = "&日誌快取"};
			logCacheMenuItem.Click += (sender, args) => { new CacheDialog(this).ShowModal(this); };

			var apiDataMenuItem = new ButtonMenuItem {Text = "&API 快取"};
			apiDataMenuItem.Click += (sender, args) => { new ApiDialog(ApiProcessor).ShowModal(this); };

			var settingsFormMenuItem = new ButtonMenuItem {Text = "&設定"};
			settingsFormMenuItem.Click += (sender, args) => { new SettingsForm().Show(); };

			var debugDataMenuItem = new CheckMenuItem {Text = "顯示&除錯資料"};
			debugDataMenuItem.Checked = Settings.ShowDebugData;
			debugDataMenuItem.CheckedChanged += (sender, args) => { Settings.ShowDebugData = debugDataMenuItem.Checked; };

			var showGuildTagsMenuItem = new CheckMenuItem {Text = "在日誌詳細資料中顯示&公會標籤" };
			showGuildTagsMenuItem.Checked = Settings.ShowGuildTagsInLogDetail;
			showGuildTagsMenuItem.CheckedChanged += (sender, args) => { Settings.ShowGuildTagsInLogDetail = showGuildTagsMenuItem.Checked; };

			var showFailurePercentagesMenuItem = new CheckMenuItem {Text = "在日誌清單中顯示敗戰Boss&生命百分比" };
			showFailurePercentagesMenuItem.Checked = Settings.ShowFailurePercentagesInLogList;
			showFailurePercentagesMenuItem.CheckedChanged += (sender, args) =>
			{
				Settings.ShowFailurePercentagesInLogList = showFailurePercentagesMenuItem.Checked;
			};

			var showSidebarMenuItem = new CheckMenuItem {Text = "在側邊欄中顯示&篩選器" };
			showSidebarMenuItem.Checked = Settings.ShowFilterSidebar;
			Settings.ShowFilterSidebarChanged += (sender, args) => showSidebarMenuItem.Checked = Settings.ShowFilterSidebar;
			showSidebarMenuItem.CheckedChanged += (sender, args) => { Settings.ShowFilterSidebar = showSidebarMenuItem.Checked; };

			// TODO: Implement
			/*
			var arcdpsSettingsMenuItem = new ButtonMenuItem {Text = "&arcdps settings", Enabled = false};

			var arcdpsMenuItem = new ButtonMenuItem {Text = "&arcdps", Enabled = false};
			arcdpsMenuItem.Items.Add(arcdpsSettingsMenuItem);
			*/

			var dataMenuItem = new ButtonMenuItem {Text = "&資料"};
			dataMenuItem.Items.Add(updateMenuItem);
			dataMenuItem.Items.Add(compressLogsItem);
			dataMenuItem.Items.Add(new SeparatorMenuItem());
			dataMenuItem.Items.Add(logCacheMenuItem);
			dataMenuItem.Items.Add(apiDataMenuItem);

			var viewMenuItem = new ButtonMenuItem {Text = "&檢視"};
			viewMenuItem.Items.Add(showSidebarMenuItem);
			viewMenuItem.Items.Add(showGuildTagsMenuItem);
			viewMenuItem.Items.Add(showFailurePercentagesMenuItem);
			viewMenuItem.Items.Add(new SeparatorMenuItem());
			viewMenuItem.Items.Add(debugDataMenuItem);

			var settingsMenuItem = new ButtonMenuItem {Text = "&設定"};
			settingsMenuItem.Items.Add(settingsFormMenuItem);

			var helpMenuItem = new ButtonMenuItem {Text = "幫助"};
			helpMenuItem.Items.Add(new About());

			return new MenuBar(dataMenuItem, viewMenuItem, settingsMenuItem, helpMenuItem);
		}

		private TabControl ConstructMainTabControl()
		{
			// Main log list
			var logList = new LogList(LogCache, ApiData, LogDataProcessor, UploadProcessor, ImageProvider, LogNameProvider);
			LogCollectionsInitialized += (sender, args) => logList.DataStore = logsFiltered;
			FilteredLogsUpdated += (sender, args) => logList.RefreshSelectionForDetailPanels();
	
			LogDataProcessor.Processed += (sender, args) =>
			{
				bool last = args.CurrentScheduledItems == 0;

				if (last || gridRefreshCooldown.TryUse(DateTime.Now))
				{
					Application.Instance.AsyncInvoke(() => { logList.ReloadData(); });
				}
			};
	
			// Player list
			var playerList = new PlayerList(LogCache, ApiData, LogDataProcessor, UploadProcessor, ImageProvider, LogNameProvider);
			FilteredLogsUpdated += (sender, args) => playerList.UpdateDataFromLogs(logsFiltered);

			// Guild list
			var guildList = new GuildList(LogCache, ApiData, LogDataProcessor, UploadProcessor, ImageProvider, LogNameProvider);
			FilteredLogsUpdated += (sender, args) => guildList.UpdateDataFromLogs(logsFiltered);

			// Statistics
			var statistics = new StatisticsSection(ImageProvider, LogNameProvider);
			FilteredLogsUpdated += (sender, args) => statistics.UpdateDataFromLogs(logsFiltered.ToList());
			LogNameProvider.MapNamesUpdated += (_, _) => statistics.UpdateDataFromLogs(logsFiltered.ToList());

			// Game data collecting
			var gameDataCollecting = new GameDataCollecting(logList, LogCache, ApiData, LogDataProcessor, UploadProcessor, ImageProvider, LogNameProvider);
			var gameDataPage = new TabPage
			{
				Text = "遊戲資料", Content = gameDataCollecting, Visible = Settings.ShowDebugData
			};
			Settings.ShowDebugDataChanged += (sender, args) => gameDataPage.Visible = Settings.ShowDebugData;

			// Service status
			var serviceStatus = new DynamicLayout {Spacing = new Size(10, 10), Padding = new Padding(5)};
			serviceStatus.BeginHorizontal();
			{
				serviceStatus.Add(new GroupBox
				{
					Text = "上傳",
					Content = new BackgroundProcessorDetail {BackgroundProcessor = UploadProcessor}
				}, xscale: true);
				serviceStatus.Add(new GroupBox
				{
					Text = "激戰2 API",
					Content = new BackgroundProcessorDetail {BackgroundProcessor = ApiProcessor}
				}, xscale: true);
			}
			serviceStatus.EndHorizontal();
			serviceStatus.BeginHorizontal();
			{
				serviceStatus.Add(new GroupBox
				{
					Text = "日誌處理",
					Content = new BackgroundProcessorDetail {BackgroundProcessor = LogDataProcessor}
				}, xscale: true);
				serviceStatus.Add(new GroupBox
				{
					Text = "日誌壓縮",
					Content = new BackgroundProcessorDetail {BackgroundProcessor = LogCompressionProcessor}
				}, xscale: true);
			}
			serviceStatus.EndHorizontal();
			serviceStatus.AddRow(null);
			var servicePage = new TabPage
			{
				Text = "服務", Content = serviceStatus, Visible = Settings.ShowDebugData
			};
			Settings.ShowDebugDataChanged += (sender, args) => servicePage.Visible = Settings.ShowDebugData;

			var tabs = new TabControl();
			tabs.Pages.Add(new TabPage {Text = "日誌", Content = logList});
			tabs.Pages.Add(new TabPage {Text = "玩家", Content = playerList});
			tabs.Pages.Add(new TabPage {Text = "公會", Content = guildList});
			tabs.Pages.Add(new TabPage {Text = "統計數據", Content = statistics});
			tabs.Pages.Add(gameDataPage);
			tabs.Pages.Add(servicePage);

			// This is needed to avoid a Gtk platform issue where the tab is changed to the last one.
			Shown += (sender, args) => tabs.SelectedIndex = 1;

			return tabs;
		}

		public void ReloadLogs()
		{
			logLoadTaskTokenSource?.Cancel();
			logLoadTaskTokenSource = new CancellationTokenSource();

			logs.Clear();
			LogSearchStarted?.Invoke(this, EventArgs.Empty);
			Task.Run(() => FindLogs(logLoadTaskTokenSource.Token))
				.ContinueWith(t => Application.Instance.Invoke(() => LogSearchFinished?.Invoke(null, EventArgs.Empty)));

			SetupFileSystemWatchers();
		}


		private void SetupFileSystemWatchers()
		{
			foreach (var watcher in fileSystemWatchers)
			{
				watcher.Dispose();
			}

			// We do not want to process logs before they are fully written,
			// mid-compression and the like, so we add a delay after detecting one.
			var delay = TimeSpan.FromSeconds(5);

			fileSystemWatchers.Clear();
			foreach (var directory in Settings.LogRootPaths)
			{
				try
				{
					var watcher = new FileSystemWatcher(directory);
					watcher.IncludeSubdirectories = true;
					watcher.Filter = "*";
					watcher.Created += (sender, args) =>
					{
						Task.Run(async () =>
						{
							await Task.Delay(delay);
							if (File.Exists(args.FullPath) && LogFinder.IsLikelyEvtcLog(args.FullPath))
							{
								Application.Instance.AsyncInvoke(() => AddNewLog(args.FullPath));
							}
						});
					};
					watcher.Renamed += (sender, args) =>
					{
						Task.Run(async () =>
						{
							await Task.Delay(delay);
							if (File.Exists(args.FullPath) && LogFinder.IsLikelyEvtcLog(args.FullPath))
							{
								Application.Instance.AsyncInvoke(() => AddNewLog(args.FullPath));
							}
						});
					};
					watcher.EnableRaisingEvents = true;

					fileSystemWatchers.Add(watcher);
				}
				catch (Exception e)
				{
					// TODO: Replace with proper logging
					Console.Error.WriteLine($"Failed to set up FileSystemWatcher: {e.Message}");
				}
			}
		}

		private void AddNewLog(string fullName)
		{
			if (logs.Any(x => x.FileName == fullName))
			{
				return;
			}

			if (!LogCache.TryGetLogData(fullName, out var log))
			{
				log = new LogData(fullName);
			}

			if (log.ParsingStatus != ParsingStatus.Parsed)
			{
				LogDataProcessor.Schedule(log);
			}

			logs.Add(log);
		}

		/// <summary>
		/// Discover logs and process them.
		/// </summary>
		private void FindLogs(CancellationToken cancellationToken)
		{
			LogDataProcessor.UnscheduleAll();

			// TODO: Fix the counters being off if a log is currently being processed
			LogDataProcessor.ResetTotalCounters();
			try
			{
				var newLogs = new List<LogData>();

				//foreach (var log in LogFinder.GetTesting())
				foreach (var log in Settings.LogRootPaths.SelectMany(x => LogFinder.GetFromDirectory(x, LogCache)))
				{
					newLogs.Add(log);

					if (log.ParsingStatus == ParsingStatus.Parsed)
					{
						ApiProcessor.RegisterLog(log);
					}
					else
					{
						LogDataProcessor.Schedule(log);
					}

					cancellationToken.ThrowIfCancellationRequested();
				}

				Application.Instance.Invoke(() => { logs.AddRange(newLogs); });
			}
			catch (Exception e) when (!(e is OperationCanceledException))
			{
				Application.Instance.Invoke(() =>
				{
					MessageBox.Show(this, $"Logs could not be found.\nReason: {e.Message}", "Log Discovery Error",
						MessageBoxType.Error);
				});
			}

			if (LogCache.ChangedSinceLastSave)
			{
				LogCache.SaveToFile();
			}
		}

		private event EventHandler LogSearchStarted;
		private event EventHandler LogSearchFinished;
		private event EventHandler FilteredLogsUpdated;
		private event EventHandler LogCollectionsInitialized;
	}
}