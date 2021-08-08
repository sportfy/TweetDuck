using System.Collections.Generic;
using TweetLib.Api.Data;
using TweetLib.Api.Data.Notification;
using TweetLib.Api.Service;

namespace TweetDuck.Application.Service {
	class NotificationService : INotificationService {
		private readonly List<NamespacedProvider> desktopNotificationScreenProviders = new List<NamespacedProvider>();

		public void RegisterDesktopNotificationScreenProvider(IDesktopNotificationScreenProvider provider) {
			desktopNotificationScreenProviders.Add(new NamespacedProvider(ApiServices.Namespace(provider.Id), provider));
		}

		public List<NamespacedProvider> GetDesktopNotificationScreenProviders() {
			return desktopNotificationScreenProviders;
		}

		public class NamespacedProvider : IDesktopNotificationScreenProvider {
			public NamespacedResource NamespacedId { get; }

			private readonly IDesktopNotificationScreenProvider provider;

			public NamespacedProvider(NamespacedResource id, IDesktopNotificationScreenProvider provider) {
				this.NamespacedId = id;
				this.provider = provider;
			}

			public Resource Id => provider.Id;
			public string DisplayName => provider.DisplayName;
			public IScreen PickScreen(IScreenLayout layout) => provider.PickScreen(layout);
		}
	}
}
