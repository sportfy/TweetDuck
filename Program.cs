﻿using CefSharp;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TweetDick.Configuration;
using TweetDick.Core;
using TweetDick.Migration;

[assembly: CLSCompliant(true)]
namespace TweetDick{
    static class Program{
        #if DUCK
        public const string BrandName = "TweetDuck";
        public const string Website = "http://tweetduck.chylex.com";
        #else
        public const string BrandName = "TweetDick";
        public const string Website = "http://tweetdick.chylex.com";
        #endif

        public static readonly string StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),BrandName);
        private static readonly LockManager LockManager = new LockManager(Path.Combine(StoragePath,".lock"));
        
        public static UserConfig UserConfig { get; private set; }

        private static string HeaderAcceptLanguage{
            get{
                string culture = CultureInfo.CurrentCulture.Name;

                if (culture == "en"){
                    return "en-us,en";
                }
                else{
                    return culture.ToLowerInvariant()+",en;q=0.9";
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LoadLibrary(string name);

        [DllImport("Shell32.dll")]
        public static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        [STAThread]
        private static void Main(){
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            if (!LockManager.Lock()){
                if (MessageBox.Show("Another instance of "+BrandName+" is already running.\r\nDo you want to close it?",BrandName+" is Already Running",MessageBoxButtons.YesNo,MessageBoxIcon.Error,MessageBoxDefaultButton.Button2) == DialogResult.Yes){
                    if (!LockManager.CloseLockingProcess(10000)){
                        MessageBox.Show("Could not close the other process.",BrandName+" Has Failed :(",MessageBoxButtons.OK,MessageBoxIcon.Error);
                        return;
                    }
                }
                else return;
            }

            UserConfig = UserConfig.Load(Path.Combine(StoragePath,"TD_UserConfig.cfg"));

            MigrationManager.Run();

            Cef.OnContextInitialized = () => {
                using(IRequestContext ctx = Cef.GetGlobalRequestContext()){
                    string err;
                    ctx.SetPreference("browser.enable_spellchecking",false,out err);
                }
            };

            Cef.Initialize(new CefSettings{
                AcceptLanguageList = HeaderAcceptLanguage,
                UserAgent = BrandName+" "+Application.ProductVersion,
                Locale = CultureInfo.CurrentCulture.TwoLetterISOLanguageName,
                CachePath = StoragePath,
                #if !DEBUG
                LogSeverity = LogSeverity.Disable
                #endif
            });

            Application.ApplicationExit += (sender, args) => {
                UserConfig.Save();
                LockManager.Unlock();
                Cef.Shutdown();
            };

            Application.Run(new FormBrowser());
        }
    }
}
