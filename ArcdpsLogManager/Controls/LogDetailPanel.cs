using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;
using GW2Scratch.ArcdpsLogManager.Configuration;
using GW2Scratch.ArcdpsLogManager.Gw2Api;
using GW2Scratch.ArcdpsLogManager.Logs;
using GW2Scratch.ArcdpsLogManager.Logs.Naming;
using GW2Scratch.ArcdpsLogManager.Logs.Tagging;
using GW2Scratch.ArcdpsLogManager.Processing;
using GW2Scratch.ArcdpsLogManager.Sections;
using GW2Scratch.EVTCAnalytics.Processing.Encounters.Modes;
using GW2Scratch.EVTCAnalytics.Processing.Encounters.Results;

namespace GW2Scratch.ArcdpsLogManager.Controls
{
	public sealed class LogDetailPanel : DynamicLayout
	{
		private UploadProcessor UploadProcessor { get; }
		private ImageProvider ImageProvider { get; }
		private ILogNameProvider NameProvider { get; }

		private LogData logData;

		private readonly Label nameLabel = new Label {Font = Fonts.Sans(16, FontStyle.Bold), Wrap = WrapMode.Word};
		private readonly Label modeLabel = new Label {Font = Fonts.Sans(12, FontStyle.Italic), Wrap = WrapMode.Word};
		private readonly Label resultLabel = new Label {Font = Fonts.Sans(11)};
		private readonly LinkButton fileNameButton = new LinkButton();
		private readonly GroupCompositionControl groupComposition;
		private readonly Label parseTimeLabel = new Label();
		private readonly Label parseStatusLabel = new Label();
		private readonly Button dpsReportUploadButton;
		private readonly TextBox dpsReportTextBox;
		private readonly Button dpsReportOpenButton;
		private readonly Button copyButton;
		private readonly TagControl tagControl;
		private readonly DynamicTable groupCompositionSection;
		private readonly DynamicTable failedProcessingSection;
		private readonly TextArea exceptionTextArea = new TextArea {ReadOnly = true};
		private readonly MistlockInstabilityList instabilityList;

		public LogData LogData
		{
			get => logData;
			set
			{
				SuspendLayout();
				logData = value;

				if (logData == null)
				{
					Visible = false;
					return;
				}

				Visible = true;

				nameLabel.Text = NameProvider.GetName(logData);

				modeLabel.Text = logData.EncounterMode switch
				{
					EncounterMode.Unknown => "",
					EncounterMode.Normal => "",
					EncounterMode.Challenge => "挑戰模式",
					EncounterMode.LegendaryChallenge => "傳奇挑戰模式",
					EncounterMode.Emboldened1 => "膽量模式 1層",
					EncounterMode.Emboldened2 => "膽量模式 2層",
					EncounterMode.Emboldened3 => "膽量模式 3層",
					EncounterMode.Emboldened4 => "膽量模式 4層",
					EncounterMode.Emboldened5 => "膽量模式 5層",
					_ => throw new ArgumentOutOfRangeException()
				};

				if (logData.LogExtras?.FractalExtras?.FractalScale != null)
				{
					if (modeLabel.Text != "")
					{
						modeLabel.Text += ", ";
					}
					modeLabel.Text += $"碎層難度係數 {logData.LogExtras.FractalExtras.FractalScale}";
				}
				
				modeLabel.Visible = !String.IsNullOrWhiteSpace(modeLabel.Text);

				string result;
				switch (logData.EncounterResult)
				{
					case EncounterResult.Success:
						result = "成功";
						break;
					case EncounterResult.Failure:
						result = logData.HealthPercentage.HasValue
							? $"失敗 ({logData.HealthPercentage * 100:0.00}% 生命值)"
							: "失敗";
						break;
					case EncounterResult.Unknown:
						result = "未知";
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				double seconds = logData.EncounterDuration.TotalSeconds;
				string duration = logData.ShortDurationString;

				fileNameButton.Text = System.IO.Path.GetFileName(logData.FileName);

				resultLabel.Text = $"{duration} {result}";

				parseTimeLabel.Text = $"{logData.ParseMilliseconds} 毫秒";
				parseStatusLabel.Text = logData.ParsingStatus.ToString();

				instabilityList.MistlockInstabilities = logData.LogExtras?.FractalExtras?.MistlockInstabilities;

				groupComposition.Players = logData.Players;

				UpdateUploadStatus();

				tagControl.Tags = logData.Tags;

				if (logData.ParsingStatus == ParsingStatus.Failed)
				{
					if (logData.ParsingException != null)
					{
						string exceptionText = $"{logData.ParsingException.Message}\n\n\n" +
						                       $"{logData.ParsingException.ExceptionName}: {logData.ParsingException.Message}\n" +
						                       $"{logData.ParsingException.StackTrace}";
						exceptionTextArea.Text = exceptionText;
					}

					if (logData.Players == null)
					{
						groupCompositionSection.Visible = false;
					}

					failedProcessingSection.Visible = true;
				}
				else
				{
					groupCompositionSection.Visible = true;
					failedProcessingSection.Visible = false;
				}

				ResumeLayout();
			}
		}

		public LogDetailPanel(LogCache logCache, ApiData apiData, LogDataProcessor logProcessor, UploadProcessor uploadProcessor,
			ImageProvider imageProvider, ILogNameProvider nameProvider, bool readOnly = false)
		{
			UploadProcessor = uploadProcessor;
			ImageProvider = imageProvider;
			NameProvider = nameProvider;

			Padding = new Padding(10, 10, 10, 2);
			Width = 350;
			Visible = false;

			instabilityList = new MistlockInstabilityList(imageProvider);
			groupComposition = new GroupCompositionControl(apiData, imageProvider);
			tagControl = new TagControl {ReadOnly = readOnly};

			DynamicGroup debugSection;
			var debugButton = new Button {Text = "除錯資料"};
			var reparseButton = new Button {Text = "重新處理"};

			tagControl.TagAdded += (sender, args) =>
			{
				if (logData.Tags.Add(new TagInfo(args.Name)))
				{
					logCache.CacheLogData(logData);
				}
			};

			tagControl.TagRemoved += (sender, args) =>
			{
				if (logData.Tags.Remove(new TagInfo(args.Name)))
				{
					logCache.CacheLogData(logData);
				}
			};

			BeginVertical(spacing: new Size(0, 15), yscale: true);
			{
				BeginVertical(spacing: new Size(0, 8));
				{
					BeginVertical();
					{
						Add(nameLabel);
						Add(modeLabel);
					}
					EndVertical();
					BeginVertical();
					{
						Add(resultLabel);
					}
					EndVertical();
					BeginVertical();
					{
						Add(instabilityList);
					}
					EndVertical();
				}
				EndVertical();
				BeginVertical(spacing: new Size(0, 5));
				{
					groupCompositionSection = BeginVertical(yscale: true);
					{
						AddRow(new Scrollable {Content = groupComposition, Border = BorderType.None});
					}
					EndVertical();

					// The WPF platform also considers invisible sections in layout when their yscale is set to true.
					// To work around this, we disable it for WPF. As the same issue also affects the group composition
					// section above, the layout does not fully collapse, and the failed processing section appears in
					// the lower half of the panel, which is good enough and doesn't even look that much out of place.
					bool yscaleFailedSection = !Application.Instance.Platform.IsWpf;

					failedProcessingSection = BeginVertical(spacing: new Size(10, 10), yscale: yscaleFailedSection);
					{
						AddRow("Processing of this log failed. This may be a malformed log, " +
						       "often caused by versions of arcdps incompatible with a specific Guild Wars 2 release.");
						AddRow("Reason for failed processing:");
						AddRow(exceptionTextArea);
					}
					EndVertical();

					debugSection = BeginGroup("除錯資料", new Padding(5));
					{
						BeginHorizontal();
						{
							BeginVertical(xscale: true, spacing: new Size(5, 5));
							{
								AddRow("處理所花費的時間", parseTimeLabel);
								AddRow("處理狀態", parseStatusLabel);
							}
							EndVertical();
							BeginVertical(spacing: new Size(5, 5));
							{
								AddRow(debugButton);
								AddRow(reparseButton);
							}
							EndVertical();
						}
						EndHorizontal();
					}
					EndGroup();
					BeginHorizontal();
					{
						Add(new Scrollable {Content = tagControl, Border = BorderType.None});
					}
					EndHorizontal();

					if (Application.Instance.Platform.IsWpf)
					{
						copyButton = new Button { Image = imageProvider.GetCopyButtonEnabledImage(), Height = 25, Width = 25 };
					}
					else
					{
						// The height is not working correctly on Gtk, and the icon may have clashing colors depending on the Gtk theme.
						copyButton = new Button { Text = "複製" };
					}
					dpsReportUploadButton = new Button();
					dpsReportTextBox = new TextBox {ReadOnly = true};
					dpsReportOpenButton = new Button {Text = "打開"};

					BeginGroup("上傳到 dps.report (Elite Insights)", new Padding(5), new Size(0, 5));
					{
						BeginVertical(spacing: new Size(5, 5));
						{
							BeginHorizontal();
							{
								// On the Gtk platform, there is not enough space for the button with its implicit padding.
								if (!Application.Instance.Platform.IsGtk)
								{
									Add(copyButton);
								}
								Add(dpsReportTextBox, true);
								Add(dpsReportOpenButton);
								if (!readOnly)
								{
									Add(dpsReportUploadButton);
								}
							}
							EndHorizontal();
						}
						EndVertical();
					}
					EndGroup();
				}
				EndVertical();
			}
			EndVertical();
			BeginVertical(spacing: new Size(10, 0));
			{
				Add(null, true);
				BeginHorizontal();
				{
					Add(null, true);
					Add(fileNameButton);
				}
			}
			EndVertical();

			dpsReportUploadButton.Click += (sender, args) => { UploadProcessor.ScheduleDpsReportEIUpload(logData); };
			dpsReportOpenButton.Click += (sender, args) =>
			{
				try
				{
					var processInfo = new ProcessStartInfo() { FileName = logData.DpsReportEIUpload.Url, UseShellExecute = true };
					Process.Start(processInfo);
				}
				catch (Exception e)
				{
					MessageBox.Show(this, $"Failed to open the URL: {e.Message}. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxType.Error);
				}
			};
			copyButton.Click += (sender, args) =>
			{
				var copyClipboard = new Clipboard()
				{
					Text = logData.DpsReportEIUpload.Url,
				};
			};

			debugButton.Click += (sender, args) =>
			{
				var debugData = new DebugData {LogData = LogData};
				var dialog = new Form {Content = debugData, Width = 500, Title = "除錯資料" };
				dialog.Show();
				debugData.InspectorOpened += (_, _) => dialog.Close();
			};

			reparseButton.Click += (sender, args) => logProcessor.Schedule(logData);

			fileNameButton.Click += (sender, args) =>
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start("explorer.exe", $"/select,\"{logData.FileName}\"");
				}
				else
				{
					// On linux, we try to use dbus and if that fails, we fallback to just opening the directory.
					// The dbus invocation can fail for a variety of reasons:
					// - dbus is not available
					// - no programs implement the service,
					// - ...
					
					var dbusArgs = "--session --dest=org.freedesktop.FileManager1 " +
						"--type=method_call /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems " +
						$"array:string:\"file://{logData.FileName}\" string:\"\"";
					var dbusProcessInfo = new ProcessStartInfo()
					{
						FileName = "dbus-send",
						Arguments = dbusArgs,
						UseShellExecute = true
					};
					var dbusProcess = Process.Start(dbusProcessInfo);
					dbusProcess?.WaitForExit();
					bool success = (dbusProcess?.ExitCode ?? 1) == 0;
					
					if (!success)
					{
						// Just open the directory instead.
						var processInfo = new ProcessStartInfo()
						{
							FileName = System.IO.Path.GetDirectoryName(logData.FileName),
							UseShellExecute = true
						};
						Process.Start(processInfo);
					}
				}
			};

			Settings.ShowDebugDataChanged += (sender, args) =>
			{
				debugSection.Visible = Settings.ShowDebugData;
				debugSection.GroupBox.Visible = Settings.ShowDebugData;
			};
			Shown += (sender, args) =>
			{
				// Assigning visibility in the constructor does not work
				debugSection.Visible = Settings.ShowDebugData;
				debugSection.GroupBox.Visible = Settings.ShowDebugData;
			};

			uploadProcessor.Processed += OnUploadProcessorUpdate;
			uploadProcessor.Unscheduled += OnUploadProcessorUpdate;
			uploadProcessor.Scheduled += OnUploadProcessorUpdate;
		}

		private void UpdateUploadStatus()
		{
			if (logData == null)
			{
				return;
			}

			const string reuploadButtonText = "重新上傳";

			bool uploadEnabled = false;
			bool openEnabled = false;
			bool copyEnabled = false;
			string text = "";
			string uploadButtonText;
			var upload = logData.DpsReportEIUpload;
			switch (upload.UploadState)
			{
				case UploadState.NotUploaded:
					uploadButtonText = "上傳";
					uploadEnabled = true;
					break;
				case UploadState.Queued:
				case UploadState.Uploading:
					uploadButtonText = "上傳中...";
					break;
				case UploadState.UploadError:
					uploadButtonText = reuploadButtonText;
					uploadEnabled = true;
					text = $"上傳失敗: {upload.UploadError ?? "沒有錯誤"}";
					break;
				case UploadState.ProcessingError:
					uploadButtonText = reuploadButtonText;
					uploadEnabled = true;
					text = $"dps.report 錯誤: {upload.ProcessingError ?? "沒有錯誤"}";
					break;
				case UploadState.Uploaded:
					uploadButtonText = reuploadButtonText;
					uploadEnabled = true;
					openEnabled = true;
					copyEnabled = true;
					text = upload.Url;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			if (Application.Instance.Platform.IsWpf)
			{
				copyButton.Image = copyEnabled ? ImageProvider.GetCopyButtonEnabledImage() : ImageProvider.GetCopyButtonDisabledImage();
			}

			copyButton.Enabled = copyEnabled;
			dpsReportUploadButton.Text = uploadButtonText;
			dpsReportUploadButton.Enabled = uploadEnabled;
			dpsReportOpenButton.Enabled = openEnabled;
			dpsReportTextBox.Text = text;
			dpsReportTextBox.Enabled = text != "";
		}

		private void OnUploadProcessorUpdate(object sender, EventArgs e)
		{
			Application.Instance.Invoke(UpdateUploadStatus);
		}
	}
}