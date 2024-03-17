using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Dialogs;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Filters;
using GW2Scratch.ArcdpsLogManager.Logs.Filters.Players;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Processing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GW2Scratch.ArcdpsLogManager.Controls.Filters;

public class PlayerFilterPanel : DynamicLayout
{
	private IReadOnlyList<LogData> Logs { get; set; }

	public PlayerFilterPanel(LogCache logCache, ApiData apiData, LogDataProcessor logProcessor,
		UploadProcessor uploadProcessor, ImageProvider imageProvider, ILogNameProvider logNameProvider,
		PlayerFilters filters)
	{
		var typeRadios = ConstructTypeRadios(filters);
		var addPlayerButton = ConstructAddPlayerButton(logCache, apiData, logProcessor, uploadProcessor,
			imageProvider, logNameProvider, filters);
		var grid = ConstructFilterGrid(filters, imageProvider);
		var removePlayerButton = ConstructRemoveButton(grid, filters);

		BeginVertical(spacing: new Size(5, 5));
		{
			BeginGroup("所需玩家", new Padding(5), new Size(5, 5));
			{
				AddRow(typeRadios);
				BeginVertical();
				{
					Add(grid);
				}
				EndVertical();
				BeginVertical(spacing: new Size(5, 5));
				{
					AddRow(addPlayerButton, removePlayerButton, null);
				}
				EndVertical();
			}
			EndGroup();
			BeginGroup("玩家數量", new Padding(5), new Size(5, 5));
			{
				AddRow("最少玩家數", ConstructMinCountStepper(filters));
				AddRow("最多玩家數", ConstructMaxCountStepper(filters));
			}
			EndGroup();
		}
		EndVertical();
		Add(null);
	}
	
	private NumericStepper ConstructMinCountStepper(PlayerFilters filter)
	{
		var numericStepper = new NumericStepper {Value = 0, MinValue = 0};
		numericStepper.ValueBinding
			.BindDataContext(
				Binding.Property((PlayerFilters x) => x.MinPlayerCount)
					.Convert(r => (double) r, v => (int) v)
			);
		numericStepper.DataContext = filter;

		return numericStepper;
	}
	
	private NumericStepper ConstructMaxCountStepper(PlayerFilters filter)
	{
		var numericStepper = new NumericStepper {Value = 0, MinValue = 0};
		numericStepper.ValueBinding
			.BindDataContext(
				Binding.Property((PlayerFilters x) => x.MaxPlayerCount)
					.Convert(r => (double) r, v => (int) v)
			);
		numericStepper.DataContext = filter;

		return numericStepper;
	}

	private GridView<RequiredPlayerFilter> ConstructFilterGrid(PlayerFilters filters, ImageProvider imageProvider)
	{
		var grid = new GridView<RequiredPlayerFilter>();
		grid.AllowMultipleSelection = true;
		grid.DataStore = filters.RequiredPlayers;
		grid.Columns.Add(new GridColumn
		{
			HeaderText = "帳號名",
			DataCell = new TextBoxCell
			{
				Binding = new DelegateBinding<RequiredPlayerFilter, string>(x => x.AccountName[1..])
			}
		});

		// TODO: Implement a way to choose characters
		/*
		grid.Columns.Add(new GridColumn
		{
			HeaderText = "Character",
			DataCell = new ImageTextCell
			{
				TextBinding =
					new DelegateBinding<RequiredPlayerFilter, string>(x => x.CharacterName ?? "Any"),
				ImageBinding = new DelegateBinding<RequiredPlayerFilter, Image>(x =>
					x.Profession != null ? imageProvider.GetTinyProfessionIcon(x.Profession.Value) : null),
			}
		});
		*/
		return grid;
	}

	private Button ConstructAddPlayerButton(LogCache logCache, ApiData apiData, LogDataProcessor logProcessor,
		UploadProcessor uploadProcessor, ImageProvider imageProvider, ILogNameProvider logNameProvider,
		PlayerFilters filters)
	{
		var addPlayerButton = new Button { Text = "新增玩家" };
		addPlayerButton.Click += (sender, args) =>
		{
			var dialog = new PlayerSelectDialog(logCache, apiData, logProcessor, uploadProcessor, imageProvider,
				logNameProvider, Logs);
			var selectedPlayer = dialog.ShowDialog(this);
			if (selectedPlayer != null)
			{
				var filter = new RequiredPlayerFilter(selectedPlayer.AccountName);
				filters.RequiredPlayers.Add(filter);
			}
		};
		return addPlayerButton;
	}

	private Button ConstructRemoveButton(GridView<RequiredPlayerFilter> grid, PlayerFilters filters)
	{
		var removeButton = new Button { Text = "移除所選玩家", Enabled = grid.SelectedItems.Any() };
		removeButton.Click += (_, _) =>
		{
			// We need to save the items in ToList() first as we are modifying the collection the grid is bound to.
			foreach (var player in grid.SelectedItems.ToList())
			{
				filters.RequiredPlayers.Remove(player);
			}
		};
		grid.SelectionChanged += (_, _) =>
		{
			removeButton.Enabled = grid.SelectedItems.Any();
		};
		return removeButton;
	}

	private Control ConstructTypeRadios(PlayerFilters filters)
	{
		var typeRadios = new EnumRadioButtonList<PlayerFilters.FilterType>
		{
			Spacing = new Size(5, 5),
			GetText = type => type switch
			{
				PlayerFilters.FilterType.All => "包含全部所選",
				PlayerFilters.FilterType.Any => "任一個所選",
				PlayerFilters.FilterType.None => "排除全部所選",
				_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
			},
		};
		typeRadios.SelectedValueBinding.Bind(filters, nameof(filters.Type));
		return typeRadios;
	}

	public void UpdateLogs(IReadOnlyList<LogData> logs)
	{
		Logs = logs;
	}
}