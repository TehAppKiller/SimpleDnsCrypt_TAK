using Caliburn.Micro;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Helper;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

namespace SimpleDnsCrypt.ViewModels
{
	[Export(typeof(BootstrapResolversViewModel))]
	public class BootstrapResolversViewModel : Screen
	{
		private string _windowTitle;
		private ObservableCollection<string> _bootstrapResolvers;
		private string _selectedBootstrapResolver;
		private string _addressInput;


		[ImportingConstructor]
		public BootstrapResolversViewModel()
		{
			_bootstrapResolvers = new ObservableCollection<string>();
		}

		/// <summary>
		///     The title of the window.
		/// </summary>
		public string WindowTitle
		{
			get => _windowTitle;
			set
			{
				_windowTitle = value;
				NotifyOfPropertyChange(() => WindowTitle);
			}
		}

		public ObservableCollection<string> BootstrapResolvers
		{
			get => _bootstrapResolvers;
			set
			{
				_bootstrapResolvers = value;
				NotifyOfPropertyChange(() => BootstrapResolvers);
			}
		}

		public string SelectedFallbackResolver
		{
			get => _selectedBootstrapResolver;
			set
			{
				_selectedBootstrapResolver = value;
				NotifyOfPropertyChange(() => SelectedFallbackResolver);
			}
		}

		public string AddressInput
		{
			get => _addressInput;
			set
			{
				_addressInput = value;
				NotifyOfPropertyChange(() => AddressInput);
			}
		}

		public void AddAddress()
		{
			if (string.IsNullOrEmpty(_addressInput)) return;
			var validatedAddress = ValidationHelper.ValidateIpEndpoint(_addressInput);
			if (string.IsNullOrEmpty(validatedAddress)) return;
			if (BootstrapResolvers.Contains(validatedAddress)) return;
			BootstrapResolvers.Add(validatedAddress);
			AddressInput = string.Empty;
		}

		public void RemoveAddress()
		{
			if (string.IsNullOrEmpty(_selectedBootstrapResolver)) return;
			if (_bootstrapResolvers.Count == 1) return;
			_bootstrapResolvers.Remove(_selectedBootstrapResolver);
		}

		public void RestoreDefault()
		{
			BootstrapResolvers.Clear();
			BootstrapResolvers = new ObservableCollection<string>(Global.DefaultBootstrapResolvers);
		}
	}
}
