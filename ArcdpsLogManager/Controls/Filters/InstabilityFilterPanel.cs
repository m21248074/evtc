using System;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.GameData;
using GW2Scratch.ArcdpsLogManager.Logs.Filters;
using GW2Scratch.EVTCAnalytics.Model;

namespace GW2Scratch.ArcdpsLogManager.Controls.Filters
{
	public class InstabilityFilterPanel : DynamicLayout
	{
		public InstabilityFilterPanel(ImageProvider imageProvider, LogFilters filters)
		{
			BeginVertical(spacing: new Size(5, 5), padding: new Padding(0, 0, 0, 5));
			{
				var typeRadios = new EnumRadioButtonList<InstabilityFilters.FilterType>
				{
					Spacing = new Size(5, 5),
					GetText = type => type switch
					{
						InstabilityFilters.FilterType.All => "包含全部所選",
						InstabilityFilters.FilterType.Any => "任一個所選",
						InstabilityFilters.FilterType.None => "排除全部所選",
						_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
					},
				};
				typeRadios.SelectedValueBinding.Bind(filters.InstabilityFilters,
					nameof(filters.InstabilityFilters.Type));
				AddRow(typeRadios);
			}
			EndVertical();
			BeginVertical(spacing: new Size(5, 25));
			{
				BeginVertical(spacing: new Size(5, 5));
				{
					BeginHorizontal();
					{
						foreach (var column in Enum.GetValues(typeof(MistlockInstability)).Cast<MistlockInstability>().Chunk(5))
						{
							BeginVertical(spacing: new Size(2, 2));
							{
								foreach (var instability in column)
								{
									BeginHorizontal();
									{
										var image = new ImageView
										{
											Image = imageProvider.GetMistlockInstabilityIcon(instability),
											ToolTip = GameNames.GetInstabilityName(instability),
											Size = new Size(20, 20),
										};
										Add(image);
										var checkbox = new CheckBox
										{
											Text = GameNames.GetInstabilityName(instability)
										};
										checkbox.CheckedBinding.Bind(
											() => filters.InstabilityFilters[instability],
											value => filters.InstabilityFilters[instability] = value ?? false
										);
										Add(checkbox);
									}
									EndHorizontal();
								}
							}
							EndVertical();
						}
					}
					EndHorizontal();
				}
				EndVertical();
				BeginGroup("提示", new Padding(10, 5));
				{
					Add(new Label
					{
						Text = "您可以透過右鍵日誌清單標頭並勾選 霧鎖異變 來啟用日誌清單中的 霧鎖異變 欄位。",
						Wrap = WrapMode.Word,
						// This prevents the label to expanding the entire window just to fit everything on one line.
						// It will still fully expand to the fill the group.
						Width = 300
					});
				}
				EndGroup();
				Add(null);
			}
			EndVertical();
		}
	}
}