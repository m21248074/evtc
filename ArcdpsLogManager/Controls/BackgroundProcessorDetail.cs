using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Processing;

namespace GW2Scratch.ArcdpsLogManager.Controls
{
	public sealed class BackgroundProcessorDetail : DynamicLayout, INotifyPropertyChanged
	{
		private readonly Label statusLabel = new Label();
		private readonly Label queuedLabel = new Label();
		private readonly Label processedLabel = new Label();
		private readonly Label totalQueuedLabel = new Label();
		private readonly Label concurrencyLabel = new Label();

		private IBackgroundProcessor backgroundProcessor;
		
		public IBackgroundProcessor BackgroundProcessor
		{
			get => backgroundProcessor;
			set
			{
				if (backgroundProcessor != null)
				{
					backgroundProcessor.Starting -= OnProcessorStatusChange;
					backgroundProcessor.Stopping -= OnProcessorStatusChange;
					backgroundProcessor.StoppingWithError -= OnProcessorStatusChange;
					backgroundProcessor.Scheduled -= OnProcessorStatusChange;
					backgroundProcessor.Unscheduled -= OnProcessorStatusChange;
					backgroundProcessor.Processed -= OnProcessorStatusChange;
				}

				if (Equals(value, backgroundProcessor)) return;
				backgroundProcessor = value;

				if (backgroundProcessor != null)
				{
					backgroundProcessor.Starting += OnProcessorStatusChange;
					backgroundProcessor.Stopping += OnProcessorStatusChange;
					backgroundProcessor.StoppingWithError += OnProcessorStatusChange;
					backgroundProcessor.Scheduled += OnProcessorStatusChange;
					backgroundProcessor.Unscheduled += OnProcessorStatusChange;
					backgroundProcessor.Processed += OnProcessorStatusChange;
				}

				OnPropertyChanged();
			}
		}

		public BackgroundProcessorDetail()
		{
			Padding = new Padding(10);
			Width = 300;

			BeginVertical(spacing: new Size(10, 10));
			{
				AddRow("���A:", statusLabel);
				AddRow("�B�z��:", queuedLabel);
				AddRow("�w�B�z:", processedLabel);
				AddRow("�@�B�z:", totalQueuedLabel);
				AddRow("�̤j�õo��{��:", concurrencyLabel);
			}

			PropertyChanged += (sender, args) =>
			{
				if (args.PropertyName == nameof(BackgroundProcessor))
				{
					UpdateLabels();
				}
			};
		}

		private void OnProcessorStatusChange(object sender, EventArgs args)
		{
			Application.Instance.AsyncInvoke(UpdateLabels);
		}

		private void UpdateLabels()
		{
			if (backgroundProcessor == null)
			{
				return;
			}

			statusLabel.Text = backgroundProcessor.BackgroundTaskRunning ? "���椤" : "�w����";
			queuedLabel.Text = backgroundProcessor.GetScheduledItemCount().ToString();
			processedLabel.Text = backgroundProcessor.ProcessedItemCount.ToString();
			totalQueuedLabel.Text = backgroundProcessor.TotalScheduledCount.ToString();
			concurrencyLabel.Text = backgroundProcessor.MaxConcurrentProcesses.ToString();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}