﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ZeroKLobby
{
    public class BrowserTab : WebBrowser, INavigatable
    {
        int navigatedIndex = 0;
        int historyCount = 0;
        int currrentHistoryPosition = 0;
        readonly List<string> navigatedPlaces = new List<string>();
        readonly List<string> historyList = new List<string>();
        string navigatingTo = null;
        readonly string pathHead;

        public BrowserTab(string head)
        {
            pathHead = head;
            if (Program.TasClient != null) Program.TasClient.LoginAccepted += (sender, args) =>
            {
                navigatingTo = head;
                Navigate(head);
            };
            base.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(UpdateURL); //This will call "UpdateURL()" when page finish loading
        }


        //protected override void OnNewWindow(System.ComponentModel.CancelEventArgs e) //This block "Open In New Window" button.
        //{
        //    e.Cancel = true;
        //    Navigate(StatusText);
        //}

         //protected override void OnNavigated(WebBrowserNavigatedEventArgs e) //This intercept which URL finish loading (including Advertisement)
        //{
        //    base.OnNavigated(e);
        //}
        
        protected override void OnNavigating(WebBrowserNavigatingEventArgs e) //this intercept URL navigation induced when user click on link or during page loading
        {
            var url = e.Url.ToString();
            if (string.IsNullOrEmpty(e.TargetFrameName) && url.Contains("zero-k") && !url.StartsWith("javascript:")) //if navigation is within Zero-K
            {
                var nav = Program.MainWindow.navigationControl.GetInavigatableByPath(url); //check which TAB this URL represent
                if (nav == null || nav == this) {
                    navigatingTo = url;
                }
                else
                {
                    // navigate to another tab actually
                    e.Cancel = true;
                    Program.MainWindow.navigationControl.Path = url;
                }
            }
            base.OnNavigating(e);
        }

        public string PathHead { get { return pathHead; } }

        public bool TryNavigate(params string[] path) //navigation induced by call from "NavigationControl.cs"
        {
            var pathString = String.Join("/", path);
            if (navigatingTo == pathString) { return true; }  //already navigating there, just return TRUE
            if (pathString.StartsWith(PathHead)) 
            {
                bool canNavigate = TryToGoBackForward(pathString);
                if (canNavigate) { return true; }
                navigatingTo = pathString;
                Navigate(pathString);
                return true; //the URL contain intended header, return TRUE and Navigate() to URL
            }
            String currentURL = String.Empty;
            if (Url != null && !string.IsNullOrEmpty(Url.ToString()))
            {
                currentURL = Url.ToString();
                if (pathString == currentURL) { return true; } //already there, just return TRUE
            }
            for (int i = 0; i < navigatedIndex; i++)
            {
                if (navigatedPlaces[i] == pathString) 
                {
                    bool canNavigate = TryToGoBackForward(pathString);
                    if (canNavigate) {return true;}
                    navigatingTo = pathString;
                    Navigate(pathString);
                    return true; //the URL is from history, return TRUE and Navigate() to URL
                }
            }
            return false; //URL has nothing to do with this WebBrowser instance,  just return FALSE
        }

        public bool Hilite(HiliteLevel level, params string[] path)
        {
            return false;
        }

        public string GetTooltip(params string[] path)
        {
            throw new NotImplementedException();
        }

        public void Reload() {
            Refresh(WebBrowserRefreshOption.IfExpired);
        }

        public bool CanReload { get { return true; }}

        private void UpdateURL(object sender, WebBrowserDocumentCompletedEventArgs e) //is called when webpage finish loading
        {   //Reference: http://msdn.microsoft.com/en-us/library/system.windows.forms.webbrowser.aspx
            
            //This update URL textbox & add the page to NavigationBar's history (so that Back&Forward button can be used):
            navigatingTo = ((WebBrowser)sender).Url.ToString();
            Program.MainWindow.navigationControl.Path = ((WebBrowser)sender).Url.ToString();
            //NOTE on operation: 
            //setting the path to this URL will cause "GoToPage(path)" to be called, 
            //"GoToPage(path)" then will loop over all tab control (including this instance of "BrowserTab"), which will then call "TryToNavigate(path)".
            //SINCE the "navigatingTo" is already set to this URL here, so when "TryToNavigate(path)" is called it matches to the current URL and return TRUE.
            //when "TryToNavigate(path)" return TRUE it will cause the page to be pushed to stack & the "Back" button will work.
            
            //This store previously visited URL for checking in TryNavigate() later. If checking found a match it meant that the URL belong to this WebBrowser.
            //This list is unordered (it is not related at all to sequence in "Forward"/"Backward" button)
            var final = navigatingTo;
            bool inList = false;
            for (int i = 0; i < navigatedIndex; i++)
            {
                if (navigatedPlaces[i] == final)
                {
                    inList = true;
                    break;
                }
            }
            if (!inList)
            {
                navigatedPlaces.Add(final);//if at end of table then use "Add"
                navigatedIndex++;
            }

            AddToHistory(final);
        }

        //this function keep track of new pages opened by WebBrowser and translate it into a stack that emulate the WebBrowser's actual history.
        //We can't access WebBrowser's history directly, that's why we do this.
        private void AddToHistory(String pathString) 
        {
            if (currrentHistoryPosition <= historyCount)
            {
                if (currrentHistoryPosition == 0 && historyCount == 0)
                {
                    historyList.Add(pathString);
                    historyCount++;
                }
                else
                {
                    bool isBack = false;
                    bool isFront = false;
                    bool isHere = false;
                    if (currrentHistoryPosition > 0 && historyList[currrentHistoryPosition - 1] == pathString)
                    {
                        isBack = true;
                        currrentHistoryPosition = currrentHistoryPosition - 1;
                    }
                    else if (currrentHistoryPosition < historyCount - 1 && historyList[currrentHistoryPosition + 1] == pathString)
                    {
                        isFront = true;
                        currrentHistoryPosition = currrentHistoryPosition + 1;
                    }
                    else if (historyList[currrentHistoryPosition] == pathString)
                    {
                        isHere = true;
                    }
                    if (!isHere && !isBack && !isFront)
                    {
                        if (currrentHistoryPosition == historyCount - 1)
                        {
                            historyList.Add(pathString);
                            currrentHistoryPosition = historyCount;
                            historyCount++;
                        }
                        else if (currrentHistoryPosition < historyCount - 1)
                        {
                            historyList[currrentHistoryPosition + 1] = pathString;
                            currrentHistoryPosition = currrentHistoryPosition + 1;
                        }
                    }
                }
            }
        }

        //this function compare the pathString with one in history, and determine whether WebBrowser should GoBack() or GoForward() to navigate to that pathString.
        //The pathString come from TryNavigate() which actually triggered/come from NavigationControl.cs. The NavigationControl.cs controls the "Forward" and "Back" button and send the appropriate pathString.
        private bool TryToGoBackForward(String pathString)
        {
            if (currrentHistoryPosition <= historyCount)
            {
                if (currrentHistoryPosition != 0 || historyCount != 0)
                {
                    bool isBack = false;
                    bool isFront = false;
                    if (currrentHistoryPosition > 0 && historyList[currrentHistoryPosition - 1] == pathString)
                    {
                        isBack = true;
                    }
                    else if (currrentHistoryPosition < historyCount - 1 && historyList[currrentHistoryPosition + 1] == pathString)
                    {
                        isFront = true;
                    }
                    if (isBack)
                    {
                        currrentHistoryPosition = currrentHistoryPosition - 1;
                        base.GoBack();
                        //System.Diagnostics.Trace.TraceInformation("GoBack {0}", pathString);
                        navigatingTo = pathString;
                        return true;
                    }
                    else if (isFront)
                    {
                        currrentHistoryPosition = currrentHistoryPosition + 1;
                        base.GoForward();
                        //System.Diagnostics.Trace.TraceInformation("GoForward {0}", pathString);
                        navigatingTo = pathString;
                        return true;
                    }
                    else
                    {
                        //System.Diagnostics.Trace.TraceInformation("currrentHistoryPosition {0}", currrentHistoryPosition);
                        //System.Diagnostics.Trace.TraceInformation("historyCount {0}", historyCount);
                        //System.Diagnostics.Trace.TraceInformation("Newpage {0}", pathString);
                    }
                }
            }
            return false;
        }
         
    }
}