using Caliburn.Micro;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Helper;
using SimpleDnsCrypt.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DnsCrypt.Blacklist;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace SimpleDnsCrypt.ViewModels
{
	[Export(typeof(BlockedDomainsLogViewModel))]
	public class BlockedDomainsLogViewModel : Screen
	{
		private static readonly ILog Log = LogManagerHelper.Factory();

		private ObservableCollection<LogLine> _logLines;
		private string _logFile;
		private bool _isLogging;
		private LogLine _selectedLogLine;

		[ImportingConstructor]
		public BlockedDomainsLogViewModel()
		{
			_isLogging = false;
			_logLines = new ObservableCollection<LogLine>();
		}

		private void AddLogLine(LogLine logLine)
		{
			Execute.OnUIThread(() =>
			{
				LogLines.Add(logLine);
			});
		}

		public void ClearLog()
		{
			Execute.OnUIThread(() => { LogLines.Clear(); });
		}

		public ObservableCollection<LogLine> LogLines
		{
			get => _logLines;
			set
			{
				if (value.Equals(_logLines)) return;
				_logLines = value;
				NotifyOfPropertyChange(() => LogLines);
			}
		}

		public string LogFile
		{
			get => _logFile;
			set
			{
				if (value.Equals(_logFile)) return;
				_logFile = value;
				NotifyOfPropertyChange(() => LogFile);
			}
		}

		public LogLine SelectedLogLine
		{
			get => _selectedLogLine;
			set
			{
				_selectedLogLine = value;
				NotifyOfPropertyChange(() => SelectedLogLine);
			}
		}

		public bool IsLogging
		{
			get => _isLogging;
			set
			{
				_isLogging = value;
				BlockedDomainsLog(DnscryptProxyConfigurationManager.DnscryptProxyConfiguration);
				NotifyOfPropertyChange(() => IsLogging);
			}
		}

		private async void BlockedDomainsLog(DnscryptProxyConfiguration dnscryptProxyConfiguration)
		{
			const string defaultLogFormat = "ltsv";
			try
			{
				if (_isLogging)
				{
					if (dnscryptProxyConfiguration == null) return;

					var saveAndRestartService = false;
					if (dnscryptProxyConfiguration.Blocked_domains == null)
					{
						dnscryptProxyConfiguration.Blocked_domains = new FilterList
						{
							log_file = Global.DnsCryptProxyLogBlockedNames,
							log_format = defaultLogFormat
						};
						saveAndRestartService = true;
					}

					if (string.IsNullOrEmpty(dnscryptProxyConfiguration.Blocked_domains.log_format) ||
						!dnscryptProxyConfiguration.Blocked_domains.log_format.Equals(defaultLogFormat))
					{
						dnscryptProxyConfiguration.Blocked_domains.log_format = defaultLogFormat;
						saveAndRestartService = true;
					}

					if (string.IsNullOrEmpty(dnscryptProxyConfiguration.Blocked_domains.log_file))
					{
						dnscryptProxyConfiguration.Blocked_domains.log_file = Global.DnsCryptProxyLogBlockedNames;
						saveAndRestartService = true;
					}

					if (saveAndRestartService)
					{
						DnscryptProxyConfigurationManager.DnscryptProxyConfiguration = dnscryptProxyConfiguration;
						if (DnscryptProxyConfigurationManager.SaveConfiguration())
						{
							if (DnsCryptProxyManager.IsDnsCryptProxyInstalled())
							{
								if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
								{
									DnsCryptProxyManager.Restart();
									await Task.Delay(Global.ServiceRestartTime).ConfigureAwait(false);
								}
								else
								{
									DnsCryptProxyManager.Start();
									await Task.Delay(Global.ServiceStartTime).ConfigureAwait(false);
								}
							}
							else
							{
								await Task.Run(() => DnsCryptProxyManager.Install()).ConfigureAwait(false);
								await Task.Delay(Global.ServiceInstallTime).ConfigureAwait(false);
								if (DnsCryptProxyManager.IsDnsCryptProxyInstalled())
								{
									DnsCryptProxyManager.Start();
									await Task.Delay(Global.ServiceStartTime).ConfigureAwait(false);
								}
							}
						}
					}

					LogFile = Path.Combine(Directory.GetCurrentDirectory(), Global.DnsCryptProxyFolder,
						dnscryptProxyConfiguration.Blocked_domains.log_file);

					if (!string.IsNullOrEmpty(_logFile))
					{
						if (!File.Exists(_logFile))
						{
							File.Create(_logFile).Dispose();
							await Task.Delay(50);
						}

						await Task.Run((System.Action)(() =>
						{
							using (var reader = new StreamReader(new FileStream(_logFile,
								FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
							{
								//start at the end of the file
								var lastMaxOffset = reader.BaseStream.Length;

								while (_isLogging)
								{
									Thread.Sleep(500);
									//if the file size has not changed, idle
									if (reader.BaseStream.Length == lastMaxOffset)
										continue;

									//seek to the last max offset
									reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

									//read out of the file until the EOF
									string line;
									while ((line = reader.ReadLine()) != null)
									{
										var blockLogLine = new Models.DomainLogLine(line);
										AddLogLine(blockLogLine);
									}

									//update the last max offset
									lastMaxOffset = reader.BaseStream.Position;
								}
							}
						})).ConfigureAwait(false);
					}
					else
					{
						IsLogging = false;
					}
				}
				else
				{
					//disable block log again
					_isLogging = false;
					dnscryptProxyConfiguration.Blocked_domains.log_file = null;
					DnscryptProxyConfigurationManager.DnscryptProxyConfiguration = dnscryptProxyConfiguration;
					DnscryptProxyConfigurationManager.SaveConfiguration();
					if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
					{
						DnsCryptProxyManager.Restart();
						await Task.Delay(Global.ServiceRestartTime).ConfigureAwait(false);
					}
					Execute.OnUIThread(() => { LogLines.Clear(); });
				}
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}
	}
}
