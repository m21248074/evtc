﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Configuration;
using GW2Scratch.ArcdpsLogManager.Controls;
using GW2Scratch.ArcdpsLogManager.Dialogs;
using GW2Scratch.ArcdpsLogManager.GameData;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Processing;
using GW2Scratch.EVTCAnalytics.GameData.Encounters;
using GW2Scratch.EVTCAnalytics.Model;
using GW2Scratch.EVTCAnalytics.Processing.Encounters.Modes;
using GW2Scratch.EVTCAnalytics.Processing.Encounters.Results;

namespace GW2Scratch.ArcdpsLogManager.Sections
{
	public class LogList : Panel
	{
		private readonly LogCache logCache;
		private readonly ApiData apiData;
		private readonly LogDataProcessor logProcessor;
		private readonly UploadProcessor uploadProcessor;
		private readonly ImageProvider imageProvider;
		private readonly ILogNameProvider nameProvider;

		private readonly GridView<LogData> logGridView;
		private readonly LogDetailPanel logDetailPanel;
		private readonly MultipleLogPanel multipleLogPanel;

		private const int PlayerIconSize = 20;
		private const int PlayerIconSpacing = 5;

		private GridViewSorter<LogData> sorter;
		private FilterCollection<LogData> dataStore;

		private static readonly Dictionary<string, string> Abbreviations = new Dictionary<string, string>
		{
			{ "★", "最愛" },
			{ "CM", "挑戰或膽量模式" },
			{ "霧鎖異變", "迷霧碎層, FotM" },
			{ "碎層難度係數", "迷霧碎層, FotM)" },
			{ "", "遭遇戰圖示" },
		};
		
		public bool ReadOnly { get; init; }

		public FilterCollection<LogData> DataStore
		{
			get => dataStore;
			set
			{
				dataStore = value;
				logGridView.DataStore = dataStore;
				sorter.UpdateDataStore();
			}
		}

		public IEnumerable<LogData> SelectedItems { get => logGridView.SelectedItems; }

		public LogList(LogCache logCache, ApiData apiData, LogDataProcessor logProcessor,
			UploadProcessor uploadProcessor, ImageProvider imageProvider, ILogNameProvider nameProvider, bool readOnly = false)
		{
			ReadOnly = readOnly;
			this.logCache = logCache;
			this.apiData = apiData;
			this.logProcessor = logProcessor;
			this.uploadProcessor = uploadProcessor;
			this.imageProvider = imageProvider;
			this.nameProvider = nameProvider;

			logDetailPanel = ConstructLogDetailPanel();
			multipleLogPanel = ConstructMultipleLogPanel();
			logGridView = ConstructLogGridView(logDetailPanel, multipleLogPanel);

			var logLayout = new DynamicLayout();
			logLayout.BeginVertical();
			{
				logLayout.BeginHorizontal();
				{
					logLayout.Add(logGridView, true);
					logLayout.Add(logDetailPanel);
					logLayout.Add(multipleLogPanel);
				}
				logLayout.EndHorizontal();
			}
			logLayout.EndVertical();

			Content = logLayout;
		}

		public void ReloadData()
		{
			logGridView.ReloadData(new Range<int>(0, DataStore.Count - 1));
		}

		public void ReloadData(int row)
		{
			logGridView.ReloadData(row);
		}

		private LogDetailPanel ConstructLogDetailPanel()
		{
			return new LogDetailPanel(logCache, apiData, logProcessor, uploadProcessor, imageProvider, nameProvider, ReadOnly);
		}

		private MultipleLogPanel ConstructMultipleLogPanel()
		{
			return new MultipleLogPanel(logCache, apiData, logProcessor, uploadProcessor, nameProvider, imageProvider, ReadOnly);
		}

		private GridView<LogData> ConstructLogGridView(LogDetailPanel detailPanel, MultipleLogPanel multipleLogPanel)
		{
			var gridView = new GridView<LogData> { AllowMultipleSelection = true };

			var favoritesColumn = new GridColumn()
			{
				HeaderText = "★",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(data => data.IsFavorite ? "★" : "☆")
				}
			};

			gridView.CellClick += (sender, args) =>
			{
				if (args.GridColumn == favoritesColumn)
				{
					var data = (LogData) args.Item;
					data.IsFavorite = !data.IsFavorite;

					logCache.CacheLogData(data);

					(sender as GridView)?.Invalidate();
				}
			};

			gridView.Columns.Add(favoritesColumn);

			var encounterIconCell = new DrawableCell();
			encounterIconCell.Paint += (sender, args) =>
			{
				if (!(args.Item is LogData log)) return;
				if (log.ParsingStatus != ParsingStatus.Parsed) return;

				// On Gtk, the images look rather distorted without this interpolation settings.
				// On WPF, rendering breaks with this interpolation setting, so we do not use it there.
				if (Application.Instance.Platform.IsGtk)
				{
					args.Graphics.ImageInterpolation = ImageInterpolation.High;
				}

				var origin = args.ClipRectangle.Location;
				var rectangle = new RectangleF(origin, new SizeF(PlayerIconSize, PlayerIconSize));

				// If the log has no corresponding icon it will not be drawn.
				var wvwIcon = imageProvider.GetWvWMapIcon(log.MapId);
				var encounterIcon = imageProvider.GetTinyEncounterIcon(log.Encounter);
				if (encounterIcon != null)
				{
					args.Graphics.DrawImage(encounterIcon, rectangle);
				}
				else if (wvwIcon != null)
				{
					args.Graphics.DrawImage(wvwIcon, rectangle);
				}
				else if (log.Encounter == Encounter.Map)
				{
					args.Graphics.DrawImage(imageProvider.GetTinyInstanceIcon(), rectangle);
				}
			};

			gridView.Columns.Add(new GridColumn()
			{
				HeaderText = "",
				DataCell = encounterIconCell,
			});

			gridView.Columns.Add(new GridColumn()
			{
				HeaderText = "遭遇戰名稱",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x =>
					{
						if (x.ParsingStatus is ParsingStatus.Unparsed or ParsingStatus.Parsing)
						{
							return "處理中...";
						}

						return nameProvider.GetName(x);
					})
				}
			});
			
			gridView.Columns.Add(new GridColumn()
			{
				HeaderText = "Mode",
				DataCell = new TextBoxCell
				{
					TextAlignment = TextAlignment.Center,
					Binding = new DelegateBinding<LogData, string>(x =>
					{
						switch (x.EncounterMode)
						{
							case EncounterMode.LegendaryChallenge:
								return "LM";
							case EncounterMode.Challenge:
								return "CM";
							case EncounterMode.Normal:
								return "";
							case EncounterMode.Emboldened1:
								return "E1";
							case EncounterMode.Emboldened2:
								return "E2";
							case EncounterMode.Emboldened3:
								return "E3";
							case EncounterMode.Emboldened4:
								return "E4";
							case EncounterMode.Emboldened5:
								return "E5";
							case EncounterMode.Unknown:
								return "?";
							default:
								throw new ArgumentOutOfRangeException();
						}
					})
				}
			});

			var resultColumn = new GridColumn()
			{
				HeaderText = "結果",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x =>
					{
						switch (x.EncounterResult)
						{
							case EncounterResult.Success:
								return "成功";
							case EncounterResult.Failure:
								if (Settings.ShowFailurePercentagesInLogList && x.HealthPercentage.HasValue)
								{
									return $"失敗 ({x.HealthPercentage * 100:0.00}%)";
								}
								else
								{
									return "失敗";
								}
							case EncounterResult.Unknown:
								return "未知";
							default:
								throw new ArgumentOutOfRangeException();
						}
					})
				}
			};
			gridView.Columns.Add(resultColumn);
			
			var fractalScaleColumn = new GridColumn
			{
				HeaderText = "碎層難度係數",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x => x.LogExtras?.FractalExtras?.FractalScale?.ToString() ?? "")
				}
			};
			gridView.Columns.Add(fractalScaleColumn);

			var instabilityCell = new DrawableCell();
			instabilityCell.Paint += (sender, args) =>
			{
				if (!(args.Item is LogData log)) return;
				if (log.ParsingStatus != ParsingStatus.Parsed) return;

				var instabilities = log.LogExtras?.FractalExtras?.MistlockInstabilities
					                    ?.OrderBy(GameNames.GetInstabilityName)
					                    ?.ToArray()
				                    ?? Array.Empty<MistlockInstability>();
				
				var origin = args.ClipRectangle.Location;
				for (int i = 0; i < instabilities.Length; i++)
				{
					var icon = imageProvider.GetMistlockInstabilityIcon(instabilities[i]);
					var point = origin + new PointF(i * (PlayerIconSize + PlayerIconSpacing), 0);
					var rectangle = new RectangleF(point, new SizeF(PlayerIconSize, PlayerIconSize));
					args.Graphics.DrawImage(icon, rectangle);
				}
			};
			
			var instabilityColumn = new GridColumn
			{
				HeaderText = "霧鎖異變",
				DataCell = instabilityCell,
				Width = 3 * (PlayerIconSize + PlayerIconSpacing)
			};
			gridView.Columns.Add(instabilityColumn);
			
			var dateColumn = new GridColumn()
			{
				HeaderText = "日期",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x =>
					{
						if (x.EncounterStartTime == default)
						{
							return "未知";
						}

						string prefix = x.IsEncounterStartTimePrecise ? "" : "~";
						string encounterTime = x.EncounterStartTime.ToLocalTime().DateTime
							.ToString(CultureInfo.CurrentCulture);
						return $"{prefix}{encounterTime}";
					})
				}
			};
			gridView.Columns.Add(dateColumn);

			var durationColumn = new GridColumn()
			{
				HeaderText = "戰鬥時間",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x => x.ShortDurationString)
				}
			};
			gridView.Columns.Add(durationColumn);

			gridView.Columns.Add(new GridColumn
			{
				HeaderText = "使用角色",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x => x.PointOfView?.CharacterName ?? "未知")
				}
			});

			gridView.Columns.Add(new GridColumn
			{
				HeaderText = "Account",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x => x.PointOfView?.AccountName.TrimStart(':') ?? "Unknown")
				}
			});

			var compositionCell = new DrawableCell();
			compositionCell.Paint += (sender, args) =>
			{
				if (!(args.Item is LogData log)) return;
				if (log.ParsingStatus != ParsingStatus.Parsed) return;

				var players = log.Players
					.OrderBy(player => player.Profession)
					.ThenBy(player => player.EliteSpecialization)
					.ToArray();
				
				var origin = args.ClipRectangle.Location;
				for (int i = 0; i < players.Length; i++)
				{
					var icon = imageProvider.GetTinyProfessionIcon(players[i]);
					var point = origin + new PointF(i * (PlayerIconSize + PlayerIconSpacing), 0);
					args.Graphics.DrawImage(icon, point);
				}
			};

			var compositionColumn = new GridColumn()
			{
				HeaderText = "玩家",
				DataCell = compositionCell,
				Width = 11 * (PlayerIconSize + PlayerIconSpacing) // There are logs with 11 players here and there
			};

			gridView.Columns.Add(compositionColumn);

			gridView.Columns.Add(new GridColumn
			{
				HeaderText = "地圖 ID",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x => x.MapId?.ToString() ?? "Unknown")
				}
			});

			gridView.Columns.Add(new GridColumn
			{
				HeaderText = "遊戲版本",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x => x.GameBuild?.ToString() ?? "Unknown")
				}
			});

			gridView.Columns.Add(new GridColumn
			{
				HeaderText = "arcdps 版本",
				DataCell = new TextBoxCell
				{
					Binding = new DelegateBinding<LogData, string>(x => x.EvtcVersion)
				}
			});

			foreach (var column in gridView.Columns)
			{
				column.Visible = !Settings.HiddenLogListColumns.Contains(column.HeaderText);
			}

			Settings.HiddenLogListColumnsChanged += (sender, args) =>
			{
				foreach (var column in gridView.Columns)
				{
					column.Visible = !Settings.HiddenLogListColumns.Contains(column.HeaderText);
				}
			};

			// Some extra height is needed on the WPF platform to properly fit the icons
			gridView.RowHeight = Math.Max(gridView.RowHeight, PlayerIconSize + 2);

			gridView.SelectionChanged += (sender, args) =>
			{
				RefreshSelectionForDetailPanels();
			};

			gridView.ContextMenu = ConstructLogGridViewContextMenu(gridView);

			gridView.KeyUp += (_, args) =>
			{
				if (args.Key == Keys.Delete && !ReadOnly)
				{
					if (gridView.SelectedItems?.Any() ?? false)
					{
						var deleteDialog = new LogDeleteDialog(gridView.SelectedItems, logCache, apiData,
							logProcessor, uploadProcessor, imageProvider, nameProvider);
						deleteDialog.ShowModal(this);
					}
				}
			};

			sorter = new GridViewSorter<LogData>(gridView, new Dictionary<GridColumn, Comparison<LogData>>
			{
				{ favoritesColumn, (x, y) => x.IsFavorite.CompareTo(y.IsFavorite) },
				{
					compositionColumn, (x, y) =>
					{
						int xPlayerCount = x.Players?.Count() ?? -1;
						int yPlayerCount = y.Players?.Count() ?? -1;
						return xPlayerCount.CompareTo(yPlayerCount);
					}
				},
				{ durationColumn, (x, y) => x.EncounterDuration.CompareTo(y.EncounterDuration) },
				{ dateColumn, (x, y) => x.EncounterStartTime.CompareTo(y.EncounterStartTime) },
				{
					resultColumn, (x, y) =>
					{
						if (x.EncounterResult == EncounterResult.Failure &&
						    y.EncounterResult == EncounterResult.Failure)
						{
							float xHealth = x.HealthPercentage ?? -1.0f;
							float yHealth = y.HealthPercentage ?? -1.0f;
							return yHealth.CompareTo(xHealth); // Smaller is closer to success
						}

						return x.EncounterResult.CompareTo(y.EncounterResult);
					}
				},
				{
					instabilityColumn, (x, y) =>
					{
						int xCount = x.LogExtras?.FractalExtras?.MistlockInstabilities?.Count ?? 0;
						int yCount = y.LogExtras?.FractalExtras?.MistlockInstabilities?.Count ?? 0;

						int countComparison = xCount.CompareTo(yCount);
						if (countComparison != 0)
						{
							// If the counts are not the same, we sort by counts first
							return countComparison;
						}
						
						// If counts are the same, we sort by the name of the first instability (ordered by names)
						string xName = x.LogExtras?.FractalExtras?.MistlockInstabilities?.Select(GameNames.GetInstabilityName)?.OrderBy(name => name)?.FirstOrDefault() ?? "";
						string yName = y.LogExtras?.FractalExtras?.MistlockInstabilities?.Select(GameNames.GetInstabilityName)?.OrderBy(name => name)?.FirstOrDefault() ?? "";
						
						return String.Compare(xName, yName, StringComparison.InvariantCulture);
					}
				}
			});

			sorter.EnableSorting();
			sorter.SortByDescending(dateColumn);

			return gridView;
		}

		public void RefreshSelectionForDetailPanels()
		{
			try
			{
				logDetailPanel.LogData = logGridView.SelectedItem;
				multipleLogPanel.LogData = logGridView.SelectedItems;
			}
			catch
			{
				// The gridview implementations on some platforms result in exceptions being thrown
				// in case rows that were selected no longer exist. Instead of trying to divine
				// all possible causes of these exceptions, we just catch them all.
				
				logDetailPanel.LogData = null;
				multipleLogPanel.LogData = null;
			}
		}

		public void ClearSelection()
		{
			logGridView.SelectedRows = [];
		}

		private ContextMenu ConstructLogGridViewContextMenu(GridView<LogData> gridView)
		{
			var contextMenu = new ContextMenu();
			foreach (var column in gridView.Columns)
			{
				var menuItem = new CheckMenuItem
				{
					Checked = !Settings.HiddenLogListColumns.Contains(column.HeaderText),
					Text = $"{column.HeaderText}"
				};

				if (Abbreviations.TryGetValue(menuItem.Text, out string fullName))
				{
					menuItem.Text = string.IsNullOrWhiteSpace(menuItem.Text) ? $"{fullName}" : $"{menuItem.Text} ({fullName})";
				}

				menuItem.CheckedChanged += (item, args) =>
				{
					bool shown = menuItem.Checked;
					if (!shown)
					{
						Settings.HiddenLogListColumns =
							Settings.HiddenLogListColumns.Append(column.HeaderText).Distinct().ToList();
					}
					else
					{
						Settings.HiddenLogListColumns =
							Settings.HiddenLogListColumns.Where(x => x != column.HeaderText).ToList();
					}
				};
				Settings.HiddenLogListColumnsChanged += (sender, args) =>
				{
					bool shown = !Settings.HiddenLogListColumns.Contains(column.HeaderText);
					menuItem.Checked = shown;
				};
				contextMenu.Items.Add(menuItem);
			}

			return contextMenu;
		}
	}
}