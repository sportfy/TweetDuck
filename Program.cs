using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CefSharp;
using CefSharp.WinForms;
using TweetDuck.Application;
using TweetDuck.Browser;
using TweetDuck.Browser.Handling;
using TweetDuck.Browser.Handling.General;
using TweetDuck.Configuration;
using TweetDuck.Dialogs;
using TweetDuck.Management;
using TweetDuck.Plugins;
using TweetDuck.Resources;
using TweetDuck.Utils;
using TweetLib.Core;
using TweetLib.Core.Collections;
using TweetLib.Core.Features.Extensions;
using TweetLib.Core.Utils;
using Win = System.Windows.Forms;

namespace TweetDuck {
	static class Program {
		public const string BrandName = Lib.BrandName;
		public const string VersionTag = Version.Tag;

		public const string Website = "https://tweetduck.chylex.com";

		public static readonly string ProgramPath = AppDomain.CurrentDomain.BaseDirectory;
		public static readonly string ExecutablePath = Win.Application.ExecutablePath;

		public static readonly bool IsPortable = File.Exists(Path.Combine(ProgramPath, "makeportable"));

		public static readonly string ScriptPath = Path.Combine(ProgramPath, "scripts");
		public static readonly string PluginPath = Path.Combine(ProgramPath, "plugins");
		public static readonly string ExtensionPath = Path.Combine(ProgramPath, "extensions");

		public static readonly string StoragePath = IsPortable ? Path.Combine(ProgramPath, "portable", "storage") : GetDataStoragePath();

		public static readonly string PluginDataPath = Path.Combine(StoragePath, "TD_Plugins");
		public static readonly string InstallerPath = Path.Combine(StoragePath, "TD_Updates");
		private static readonly string CefDataPath = Path.Combine(StoragePath, "TD_Chromium");

		public static string UserConfigFilePath => Path.Combine(StoragePath, "TD_UserConfig.cfg");
		public static string SystemConfigFilePath => Path.Combine(StoragePath, "TD_SystemConfig.cfg");
		public static string PluginConfigFilePath => Path.Combine(StoragePath, "TD_PluginConfig.cfg");
		public static string AnalyticsFilePath => Path.Combine(StoragePath, "TD_Analytics.cfg");

		private static string ErrorLogFilePath => Path.Combine(StoragePath, "TD_Log.txt");
		private static string ConsoleLogFilePath => Path.Combine(StoragePath, "TD_Console.txt");

		public static uint WindowRestoreMessage;

		private static readonly LockManager LockManager = new LockManager(Path.Combine(StoragePath, ".lock"));
		private static bool hasCleanedUp;

		public static Reporter Reporter { get; }
		public static ConfigManager Config { get; }
		public static ScriptLoader Resources { get; }

		static Program() {
			Reporter = new Reporter(ErrorLogFilePath);
			Reporter.SetupUnhandledExceptionHandler("TweetDuck Has Failed :(");

			Config = new ConfigManager();

			#if DEBUG
			Resources = new ScriptLoaderDebug();
			#else
			Resources = new ScriptLoader();
			#endif

			Lib.Initialize(new App.Builder {
				ErrorHandler = Reporter,
				SystemHandler = new SystemHandler(),
				ResourceHandler = Resources
			});
		}

		internal static void SetupWinForms() {
			Win.Application.EnableVisualStyles();
			Win.Application.SetCompatibleTextRenderingDefault(false);
		}

		[STAThread]
		private static void Main() {
			SetupWinForms();
			Cef.EnableHighDPISupport();

			WindowRestoreMessage = NativeMethods.RegisterWindowMessage("TweetDuckRestore");

			if (!FileUtils.CheckFolderWritePermission(StoragePath)) {
				FormMessage.Warning("Permission Error", "TweetDuck does not have write permissions to the storage folder: " + StoragePath, FormMessage.OK);
				return;
			}

			if (!LockManager.Lock(Arguments.HasFlag(Arguments.ArgRestart))) {
				return;
			}

			Config.LoadAll();

			if (Arguments.HasFlag(Arguments.ArgImportCookies)) {
				ProfileManager.ImportCookies();
			}
			else if (Arguments.HasFlag(Arguments.ArgDeleteCookies)) {
				ProfileManager.DeleteCookies();
			}

			if (Arguments.HasFlag(Arguments.ArgUpdated)) {
				WindowsUtils.TryDeleteFolderWhenAble(InstallerPath, 8000);
				WindowsUtils.TryDeleteFolderWhenAble(Path.Combine(StoragePath, "Service Worker"), 4000);
				BrowserCache.TryClearNow();
			}

			try {
				ResourceRequestHandlerBase.LoadResourceRewriteRules(Arguments.GetValue(Arguments.ArgFreeze));
			} catch (Exception e) {
				FormMessage.Error("Resource Freeze", "Error parsing resource rewrite rules: " + e.Message, FormMessage.OK);
				return;
			}

			BrowserCache.RefreshTimer();

			CefSharpSettings.WcfEnabled = false;
			CefSharpSettings.LegacyJavascriptBindingEnabled = true;

			CefSettings settings = new CefSettings {
				UserAgent = BrowserUtils.UserAgentChrome,
				BrowserSubprocessPath = Path.Combine(ProgramPath, BrandName + ".Browser.exe"),
				CachePath = StoragePath,
				UserDataPath = CefDataPath,
				LogFile = ConsoleLogFilePath,
				#if !DEBUG
				LogSeverity = Arguments.HasFlag(Arguments.ArgLogging) ? LogSeverity.Info : LogSeverity.Disable
				#endif
			};

			var pluginScheme = new PluginSchemeFactory();

			settings.RegisterScheme(new CefCustomScheme {
				SchemeName = PluginSchemeFactory.Name,
				IsStandard = false,
				IsSecure = true,
				IsCorsEnabled = true,
				IsCSPBypassing = true,
				SchemeHandlerFactory = pluginScheme
			});

			CommandLineArgs.ReadCefArguments(Config.User.CustomCefArgs).ToDictionary(settings.CefCommandLineArgs);
			BrowserUtils.SetupCefArgs(settings.CefCommandLineArgs);

			Cef.Initialize(settings, false, new BrowserProcessHandler());

			Win.Application.ApplicationExit += (sender, args) => ExitCleanup();

			ApiServices.Register();
			ExtensionLoader.LoadAllInFolder(ExtensionPath);

			FormBrowser mainForm = new FormBrowser(pluginScheme);
			Resources.Initialize(mainForm);
			Win.Application.Run(mainForm);

			if (mainForm.UpdateInstaller != null) {
				ExitCleanup();

				if (mainForm.UpdateInstaller.Launch()) {
					Win.Application.Exit();
				}
				else {
					RestartWithArgsInternal(Arguments.GetCurrentClean());
				}
			}
		}

		private static string GetDataStoragePath() {
			string custom = Arguments.GetValue(Arguments.ArgDataFolder);

			if (custom != null && (custom.Contains(Path.DirectorySeparatorChar) || custom.Contains(Path.AltDirectorySeparatorChar))) {
				if (Path.GetInvalidPathChars().Any(custom.Contains)) {
					Reporter.HandleEarlyFailure("Data Folder Invalid", "The data folder contains invalid characters:\n" + custom);
				}
				else if (!Path.IsPathRooted(custom)) {
					Reporter.HandleEarlyFailure("Data Folder Invalid", "The data folder has to be either a simple folder name, or a full path:\n" + custom);
				}

				return Environment.ExpandEnvironmentVariables(custom);
			}
			else {
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), custom ?? BrandName);
			}
		}

		public static void Restart(params string[] extraArgs) {
			CommandLineArgs args = Arguments.GetCurrentClean();
			CommandLineArgs.ReadStringArray('-', extraArgs, args);
			RestartWithArgs(args);
		}

		public static void RestartWithArgs(CommandLineArgs args) {
			FormBrowser browserForm = FormManager.TryFind<FormBrowser>();

			if (browserForm != null) {
				browserForm.ForceClose();

				ExitCleanup();
				RestartWithArgsInternal(args);
			}
		}

		private static void RestartWithArgsInternal(CommandLineArgs args) {
			args.AddFlag(Arguments.ArgRestart);
			Process.Start(ExecutablePath, args.ToString());
			Win.Application.Exit();
		}

		private static void ExitCleanup() {
			if (hasCleanedUp) {
				return;
			}

			Config.SaveAll();

			Cef.Shutdown();
			BrowserCache.Exit();

			LockManager.Unlock();
			hasCleanedUp = true;
		}
	}
}
