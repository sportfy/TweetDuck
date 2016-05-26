﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using TweetDck.Core.Utils;
using TweetDck.Core.Controls;

namespace TweetDck.Core.Handling{
    class TweetDeckBridge{
        public static string LastRightClickedLink = string.Empty;
        public static string LastHighlightedTweet = string.Empty;
        public static string ClipboardImagePath = string.Empty;

        private readonly FormBrowser form;

        public string BrandName{
            get{
                return Program.BrandName;
            }
        }

        public string VersionTag{
            get{
                return Program.VersionTag;
            }
        }

        public bool MuteNotifications{
            get{
                return Program.UserConfig.MuteNotifications;
            }
        }

        public bool ExpandLinksOnHover{
            get{
                return Program.UserConfig.ExpandLinksOnHover;
            }
        }

        public TweetDeckBridge(FormBrowser form){
            this.form = form;
        }

        public void LoadFontSizeClass(string fsClass){
            form.InvokeSafe(() => {
               TweetNotification.SetFontSizeClass(fsClass);
            });
        }

        public void LoadNotificationHeadContents(string headContents){
            form.InvokeSafe(() => {
               TweetNotification.SetHeadTag(headContents); 
            });
        }

        public void SetLastRightClickedLink(string link){
            form.InvokeSafe(() => LastRightClickedLink = link);
        }

        public void SetLastHighlightedTweet(string link){
            form.InvokeSafe(() => LastHighlightedTweet = link);
        }

        public void OpenSettingsMenu(){
            form.InvokeSafe(form.OpenSettings);
        }

        public void OnTweetPopup(string tweetHtml, string tweetUrl, int tweetCharacters){
            form.InvokeSafe(() => {
                form.OnTweetPopup(new TweetNotification(tweetHtml,tweetUrl,tweetCharacters));
            });
        }

        public void OnTweetSound(){
            form.InvokeSafe(form.OnTweetSound);
        }

        public void DisplayTooltip(string text, bool showInNotification){
            form.InvokeSafe(() => {
                form.DisplayTooltip(text,showInNotification);
            });
        }

        public void TryPasteImage(){
            form.InvokeSafe(() => {
                if (Clipboard.ContainsImage()){
                    Image img = Clipboard.GetImage();
                    if (img == null)return;

                    try{
                        Directory.CreateDirectory(Program.TemporaryPath);

                        ClipboardImagePath = Path.Combine(Program.TemporaryPath,"TD-Img-"+DateTime.Now.Ticks+".png");
                        img.Save(ClipboardImagePath,ImageFormat.Png);

                        form.OnImagePasted();
                    }catch(Exception e){
                        Program.HandleException("Could not paste image from clipboard.",e);
                    }
                }
            });
        }

        public void ClickUploadImage(int offsetX, int offsetY){
            form.InvokeSafe(() => {
                Point prevPos = Cursor.Position;

                Cursor.Position = form.PointToScreen(new Point(offsetX,offsetY));
                NativeMethods.SimulateMouseClick(NativeMethods.MouseButton.Left);
                Cursor.Position = prevPos;

                form.OnImagePastedFinish();
            });
        }

        public void OpenBrowser(string url){
            BrowserUtils.OpenExternalBrowser(url);
        }

        public void Log(string data){
            System.Diagnostics.Debug.WriteLine(data);
        }
    }
}
