using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Sections.Statistics.Tabs;

namespace GW2Scratch.ArcdpsLogManager.Sections
{
	public class StatisticsSection : Panel
	{
		private readonly EncounterStatistics encounters;
		private readonly SpecializationStatistics specializations;

		public StatisticsSection(ImageProvider imageProvider, ILogNameProvider nameProvider)
		{
			specializations = new SpecializationStatistics(imageProvider);
			encounters = new EncounterStatistics(nameProvider);

			var tabs = new TabControl();
			tabs.Pages.Add(new TabPage {Text = "�D�J��", Content = encounters, Padding = new Padding(10)});
			tabs.Pages.Add(new TabPage {Text = "�S��", Content = specializations, Padding = new Padding(10)});
			Content = tabs;
		}

		public void UpdateDataFromLogs(IReadOnlyList<LogData> logs)
		{
			encounters.UpdateDataFromLogs(logs);
			specializations.UpdateDataFromLogs(logs);
		}
	}
}