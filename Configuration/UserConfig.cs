﻿using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using TweetDck.Core;
using TweetDck.Core.Handling;
using TweetDck.Core.Utils;
using TweetDck.Plugins;

namespace TweetDck.Configuration{
    [Serializable]
    sealed class UserConfig{
        private static readonly IFormatter Formatter = new BinaryFormatter{
            Binder = new SerializationCompatibilityHandler()
        };

        private const int CurrentFileVersion = 5;

        // START OF CONFIGURATION

        public bool IgnoreMigration { get; set; }
        public bool IgnoreUninstallCheck { get; set; }

        public WindowState BrowserWindow { get; set; }
        public bool DisplayNotificationTimer { get; set; }
        public bool NotificationTimerCountDown { get; set; }

        public TweetNotification.Duration NotificationDuration { get; set; }
        public TweetNotification.Position NotificationPosition { get; set; }
        public Point CustomNotificationPosition { get; set; }
        public int NotificationEdgeDistance { get; set; }
        public int NotificationDisplay { get; set; }
        public int NotificationDurationValue { get; set; }
        public bool NotificationLegacyLoad { get; set; }

        public bool ExpandLinksOnHover { get; set; }
        public bool EnableTrayHighlight { get; set; }

        public bool EnableUpdateCheck { get; set; }
        public string DismissedUpdate { get; set; }

        public PluginConfig Plugins { get; private set; }
        public WindowState PluginsWindow { get; set; }

        public string CustomCefArgs { get; set; }
        public string CustomBrowserCSS { get; set; }
        public string CustomNotificationCSS { get; set; }

        public bool IsCustomNotificationPositionSet{
            get{
                return CustomNotificationPosition.X != -32000 && CustomNotificationPosition.X != 32000;
            }
        }
        
        public bool MuteNotifications{
            get{
                return muteNotifications;
            }

            set{
                if (muteNotifications == value)return;

                muteNotifications = value;

                if (MuteToggled != null){
                    MuteToggled(this, new EventArgs());
                }
            }
        }

        public TrayIcon.Behavior TrayBehavior{
            get{
                return trayBehavior;
            }

            set{
                if (trayBehavior == value)return;

                trayBehavior = value;

                if (TrayBehaviorChanged != null){
                    TrayBehaviorChanged(this, new EventArgs());
                }
            }
        }

        // END OF CONFIGURATION
        
        [field:NonSerialized]
        public event EventHandler MuteToggled;
        
        [field:NonSerialized]
        public event EventHandler TrayBehaviorChanged;

        [NonSerialized]
        private string file;

        private int fileVersion;
        private bool muteNotifications;
        private TrayIcon.Behavior trayBehavior;

        private UserConfig(string file){
            this.file = file;

            BrowserWindow = new WindowState();
            DisplayNotificationTimer = true;
            NotificationDuration = TweetNotification.Duration.Medium;
            NotificationPosition = TweetNotification.Position.TopRight;
            CustomNotificationPosition = new Point(-32000, -32000);
            NotificationEdgeDistance = 8;
            NotificationDurationValue = 25;
            EnableUpdateCheck = true;
            ExpandLinksOnHover = true;
            EnableTrayHighlight = true;
            Plugins = new PluginConfig();
            PluginsWindow = new WindowState();

            Plugins.DisableOfficialFromConfig("clear-columns");
            Plugins.DisableOfficialFromConfig("reply-account");
        }

        private void UpgradeFile(){
            if (fileVersion == CurrentFileVersion){
                return;
            }

            // if outdated, cycle through all versions
            if (fileVersion == 0){
                DisplayNotificationTimer = true;
                EnableUpdateCheck = true;
                ++fileVersion;
            }

            if (fileVersion == 1){
                ExpandLinksOnHover = true;
                ++fileVersion;
            }

            if (fileVersion == 2){
                BrowserWindow = new WindowState();
                Plugins = new PluginConfig();
                PluginsWindow = new WindowState();
                ++fileVersion;
            }

            if (fileVersion == 3){
                EnableTrayHighlight = true;

                switch(NotificationDuration){
                    case TweetNotification.Duration.Short: NotificationDurationValue = 15; break;
                    case TweetNotification.Duration.Medium: NotificationDurationValue = 25; break;
                    case TweetNotification.Duration.Long: NotificationDurationValue = 35; break;
                    case TweetNotification.Duration.VeryLong: NotificationDurationValue = 45; break;
                }

                ++fileVersion;
            }

            if (fileVersion == 4){
                Plugins.DisableOfficialFromConfig("clear-columns");
                Plugins.DisableOfficialFromConfig("reply-account");
                ++fileVersion;
            }

            // update the version
            fileVersion = CurrentFileVersion;
            Save();
        }

        public bool Save(){
            try{
                string directory = Path.GetDirectoryName(file);
                if (directory == null)return false;

                Directory.CreateDirectory(directory);

                if (File.Exists(file)){
                    string backupFile = GetBackupFile(file);
                    File.Delete(backupFile);
                    File.Move(file, backupFile);
                }

                using(Stream stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None)){
                    Formatter.Serialize(stream, this);
                }

                return true;
            }catch(Exception e){
                Program.HandleException("Could not save the configuration file.", e);
                return false;
            }
        }
        
        public static UserConfig Load(string file){
            UserConfig config = null;

            for(int attempt = 0; attempt < 2; attempt++){
                try{
                    using(Stream stream = new FileStream(attempt == 0 ? file : GetBackupFile(file), FileMode.Open, FileAccess.Read, FileShare.Read)){
                        if ((config = Formatter.Deserialize(stream) as UserConfig) != null){
                            config.file = file;
                        }
                    }
                    
                    if (config != null){
                        config.UpgradeFile();
                    }

                    break;
                }catch(FileNotFoundException){
                }catch(DirectoryNotFoundException){
                    break;
                }catch(Exception e){
                    Program.HandleException("Could not open the configuration file.", e);
                }
            }

            return config ?? new UserConfig(file);
        }

        public static string GetBackupFile(string file){
            return file+".bak";
        }

        private class SerializationCompatibilityHandler : SerializationBinder{
            public override Type BindToType(string assemblyName, string typeName){
                assemblyName = assemblyName.Replace("TweetDick", "TweetDuck");
                typeName = typeName.Replace("TweetDick", "TweetDck");
                return Type.GetType(string.Format(CultureInfo.CurrentCulture, "{0},  {1}", typeName, assemblyName));
            }
        }
    }
}
