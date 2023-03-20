using Caliburn.Micro;
using DnsCrypt.Blacklist;
using MahApps.Metro.Controls;
using MahApps.Metro.SimpleChildWindow;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Helper;
using SimpleDnsCrypt.Models;
using SimpleDnsCrypt.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Application = System.Windows.Application;
using Screen = Caliburn.Micro.Screen;

namespace SimpleDnsCrypt.ViewModels
{
	[Export(typeof(ForwardViewModel))]
	public class ForwardViewModel : Screen
	{
		private static readonly ILog Log = LogManagerHelper.Factory();
		private readonly IWindowManager _windowManager;
		private readonly IEventAggregator _events;
		private BindableCollection<Rule> _forwardingRules;

		private bool _isForwardingEnabled;
		private Rule _selectedForwardingEntry;

		private string _forwardingRulesFile;

		/// <summary>
		/// Initializes a new instance of the <see cref="ForwardViewModel"/> class
		/// </summary>
		/// <param name="windowManager">The window manager</param>
		/// <param name="events">The events</param>
		[ImportingConstructor]
		public ForwardViewModel(IWindowManager windowManager, IEventAggregator events)
		{
			_windowManager = windowManager;
			_events = events;
			_events.Subscribe(this);
			_forwardingRules = new BindableCollection<Rule>();

			if (!string.IsNullOrEmpty(Properties.Settings.Default.ForwardingRulesFile))
			{
				_forwardingRulesFile = Properties.Settings.Default.ForwardingRulesFile;
				Task.Run(async () => { await ReadForwardingRulesFromFile(); });
			}
			else
			{
				//set default
				_forwardingRulesFile = Path.Combine(Directory.GetCurrentDirectory(), Global.DnsCryptProxyFolder,
					Global.DnsCryptProxyFileCloakingRules);
				Properties.Settings.Default.ForwardingRulesFile = _forwardingRulesFile;
				Properties.Settings.Default.Save();
			}
		}

		#region Forwarding

		private async Task ReadForwardingRulesFromFile(string readFromPath = "")
		{
			try
			{
				var file = _forwardingRulesFile;
				if (!string.IsNullOrEmpty(readFromPath))
				{
					file = readFromPath;
				}

				if (string.IsNullOrEmpty(file)) return;

				if (!File.Exists(file)) return;
				var lines = await DomainBlacklist.ReadAllLinesAsync(file);
				if (lines.Length > 0)
				{
					var tmpRules = new List<Rule>();
					foreach (var line in lines)
					{
						if (line.StartsWith("#")) continue;
						var tmp = line.ToLower().Trim();
						if (string.IsNullOrEmpty(tmp)) continue;
						var lineParts = tmp.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
						if (lineParts.Length != 2) continue;
						var rule = new Rule
						{
							Key = lineParts[0].Trim(),
							Value = lineParts[1].Trim()
						};

						tmpRules.Add(rule);
					}

					ForwardingRules.Clear();
					var orderedTmpRules = tmpRules.OrderBy(r => r.Key);
					ForwardingRules = new BindableCollection<Rule>(orderedTmpRules);
				}
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}

		public BindableCollection<Rule> ForwardingRules
		{
			get => _forwardingRules;
			set
			{
				if (value.Equals(_forwardingRules)) return;
				_forwardingRules = value;
				NotifyOfPropertyChange(() => ForwardingRules);
			}
		}

		public Rule SelectedForwardingEntry
		{
			get => _selectedForwardingEntry;
			set
			{
				_selectedForwardingEntry = value;
				NotifyOfPropertyChange(() => SelectedForwardingEntry);
			}
		}

		public string ForwardingRulesFile
		{
			get => _forwardingRulesFile;
			set
			{
				if (value.Equals(_forwardingRulesFile)) return;
				_forwardingRulesFile = value;
				Properties.Settings.Default.ForwardingRulesFile = _forwardingRulesFile;
				Properties.Settings.Default.Save();
				NotifyOfPropertyChange(() => ForwardingRulesFile);
			}
		}

		public bool IsForwardingEnabled
		{
			get => _isForwardingEnabled;
			set
			{
				_isForwardingEnabled = value;
				ManageDnsCryptForwarding(DnscryptProxyConfigurationManager.DnscryptProxyConfiguration);
				NotifyOfPropertyChange(() => IsForwardingEnabled);
			}
		}

		private async void ManageDnsCryptForwarding(DnscryptProxyConfiguration dnscryptProxyConfiguration)
		{
			try
			{
				if (_isForwardingEnabled)
				{
					if (dnscryptProxyConfiguration == null) return;

					var saveAndRestartService = false;

					if (dnscryptProxyConfiguration.forwarding_rules == null)
					{
						dnscryptProxyConfiguration.forwarding_rules = _forwardingRulesFile;
						saveAndRestartService = true;
					}

					if (!File.Exists(_forwardingRulesFile))
					{
						File.Create(_forwardingRulesFile).Dispose();
						await Task.Delay(50);
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
				}
				else
				{
					//disable forwarding again
					_isForwardingEnabled = false;
					dnscryptProxyConfiguration.forwarding_rules = null;
					DnscryptProxyConfigurationManager.DnscryptProxyConfiguration = dnscryptProxyConfiguration;
					DnscryptProxyConfigurationManager.SaveConfiguration();
					if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
					{
						DnsCryptProxyManager.Restart();
						await Task.Delay(Global.ServiceRestartTime).ConfigureAwait(false);
					}
				}
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}

		public void RemoveForwardingRule()
		{
			try
			{
				if (_selectedForwardingEntry == null) return;
				ForwardingRules.Remove(_selectedForwardingEntry);
				SaveForwardingRulesToFile();
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}

		public async void AddForwardingRule()
		{
			try
			{
				var metroWindow = Application.Current.Windows.OfType<MetroWindow>().FirstOrDefault();
				var addRuleWindow = new AddRuleWindow(RuleWindowType.Forwarding);
				var addRuleWindowResult = await metroWindow.ShowChildWindowAsync<AddRuleWindowResult>(addRuleWindow);

				if (!addRuleWindowResult.Result) return;
				if (string.IsNullOrEmpty(addRuleWindowResult.RuleKey) ||
					string.IsNullOrEmpty(addRuleWindowResult.RuleValue)) return;
				var tmp = new Rule
				{
					Key = addRuleWindowResult.RuleKey,
					Value = addRuleWindowResult.RuleValue
				};
				_forwardingRules.Add(tmp);
				var orderedTmpRules = _forwardingRules.OrderBy(r => r.Key);
				ForwardingRules = new BindableCollection<Rule>(orderedTmpRules);
				SaveForwardingRulesToFile();
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}

		public void ExportForwardingRules()
		{
			try
			{
				var saveForwardingFileDialog = new SaveFileDialog
				{
					RestoreDirectory = true,
					AddExtension = true,
					DefaultExt = ".txt"
				};
				var result = saveForwardingFileDialog.ShowDialog();
				if (result != DialogResult.OK) return;
				SaveForwardingRulesToFile(saveForwardingFileDialog.FileName);
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}

		public async void ImportForwardingRules()
		{
			try
			{
				var openForwardingFileDialog = new OpenFileDialog
				{
					Multiselect = false,
					RestoreDirectory = true
				};
				var result = openForwardingFileDialog.ShowDialog();
				if (result != DialogResult.OK) return;
				await ReadForwardingRulesFromFile(openForwardingFileDialog.FileName);
				SaveForwardingRulesToFile();
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}

		public void ChangeForwardingRulesFile()
		{
			try
			{
				var forwardingFolderDialog = new FolderBrowserDialog
				{
					ShowNewFolderButton = true
				};
				if (!string.IsNullOrEmpty(_forwardingRulesFile))
				{
					forwardingFolderDialog.SelectedPath = Path.GetDirectoryName(_forwardingRulesFile);
				}

				var result = forwardingFolderDialog.ShowDialog();
				if (result == DialogResult.OK)
				{
					ForwardingRulesFile = Path.Combine(forwardingFolderDialog.SelectedPath, Global.DnsCryptProxyFileForwardingRules);
					SaveForwardingRulesToFile();
				}
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}

		private int LongestForwardingKey => this._forwardingRules.Max(z => z.Key.Length);

		public void SaveForwardingRulesToFile(string saveToPath = "")
		{
			try
			{
				var file = _forwardingRulesFile;
				if (!string.IsNullOrEmpty(saveToPath))
				{
					file = saveToPath;
				}

				if (string.IsNullOrEmpty(file)) return;
				const int extraSpace = 1;
				var lines = new List<string>();
				foreach (var rule in _forwardingRules)
				{
					var spaceCount = LongestForwardingKey - rule.Key.Length;
					var sb = new StringBuilder();
					sb.Append(rule.Key);
					sb.Append(' ', spaceCount + extraSpace);
					sb.Append(rule.Value);
					lines.Add(sb.ToString());
				}

				var orderedTmpRules = lines.OrderBy(r => r);
				File.WriteAllLines(file, orderedTmpRules, Encoding.UTF8);
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}
		}

		#endregion

	}
}