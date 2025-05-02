using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;

namespace MedRecPro.Helpers
{

    public class ErrorMsg
    {
        public ConcurrentBag<string> Val { get; protected internal set; } = new ConcurrentBag<string>();
    }

    public sealed class ErrorHelper
    {

        #region initialize
        private static readonly Lazy<ErrorHelper> lazy = new Lazy<ErrorHelper>(() => new ErrorHelper());

        public ErrorHelper Instance { get { return lazy.Value; } }

        private ErrorHelper() { }

        private static ErrorMsg? errorMsg; 
        #endregion

        /******************************************************/
        public static void AddErrorMsg(string msg)
        {
            #region implementation
            string key;

            try
            {
                key = string.Concat("Error",Util.GetUserName() ??  System.Environment.UserName).GetHashString();

                try
                {
                    errorMsg = (ErrorMsg)PerformanceHelper.GetCache(key) ?? new ErrorMsg();
                }
                catch
                {
                    errorMsg = new ErrorMsg();
                }

                if (errorMsg != null)
                {

                    if (errorMsg.Val == null)
                    {
                        errorMsg.Val = new ConcurrentBag<string>();
                    }

                    if (errorMsg != null)
                    {
                        errorMsg.Val.Add(HttpUtility.HtmlEncode((errorMsg.Val.Count() + 1).ToString("D4")
                            + ": "
                            + DateTime.Now.ToLongTimeString()
                            + " "
                            + msg));
                    }
                    else
                    {
                        throw new Exception("Null value passed.");
                    }

                    //save for 10 minutes
                    PerformanceHelper.SetCache(key, errorMsg, 0.16);
                }
            }
            catch (Exception e)
            {
                Debug.Write("AddErrorMsg: " + e.Message);
            }

            #endregion
        }

        /******************************************************/
        public static bool IsErrorLogged(string txt)
        {
            #region implementation
            try
            {
                if (!string.IsNullOrEmpty(txt))
                {
                    var messages = GetErrorMsg();

                    if (messages != null && messages.Count > 0)
                    {
                        return messages.Any<string>(x => x.ToLower().Contains(txt.ToLower()));
                    }
                }
            }
            catch
            {
                return false;
            }

            return false; 
            #endregion
        }

        /******************************************************/
        public static int GetLineNumber(Exception exception)
        {
            int lineNumber = -1;
            var stackTrace = new StackTrace(exception, true);
            var frame = stackTrace.GetFrame(0);

            if (frame != null)
                lineNumber = frame.GetFileLineNumber();

            return lineNumber;
        }

        /******************************************************/
        public static List<string>? GetErrorMsg(string userName)
        {
            #region implementation
            string key;

            try
            {
                if (string.IsNullOrEmpty(userName))
                {
                    return null;
                }

                key = string.Concat("Error", userName).GetHashString();

                errorMsg = (ErrorMsg)PerformanceHelper.GetCache(key);
            }
            catch
            {
                return null;
            }
            /*
             * 03/13/2023 Barnes: added order by b/c the
             * concurrent bag wasn't in sequential order.
             */

            return errorMsg?.Val?.OrderBy(x => x)?.ToList() ?? new ErrorMsg().Val.ToList();
            #endregion
        }

        /******************************************************/
        public static List<string>? GetErrorMsg()
        {
            #region implementation
            string key;

            try
            {
                key = string.Concat("Error",Util.GetUserName() ??  System.Environment.UserName).GetHashString();

                errorMsg = (ErrorMsg)PerformanceHelper.GetCache(key);
            }
            catch
            {
                return null;
            }
            /*
             * 03/13/2023 Barnes: added order by b/c the
             * concurrent bag wasn't in sequential order.
             */

            return errorMsg?.Val?.OrderBy(x => x)?.ToList() ?? new ErrorMsg().Val.ToList(); 
            #endregion
        }
    }
}