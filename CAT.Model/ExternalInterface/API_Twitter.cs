namespace CAT.Model
{
    using System;
    using TweetSharp;
    using System.Threading;
    using System.Diagnostics;
    using System.Collections.Generic;

    public static class API_Twitter
    {
        public static void Send(Trade trade)
        {
            var svc = new TwitterService
                (
                    "6buKNSrHVPE35LQOwq7w",
                    "6WmsQekutUdkJDc4AjhZiLO9WmZsYKxJ12GcsEqjc",
                    "319732064-1ssEYYhkIMXuIiWQTnDSZO2BL5eTMBmF6MEgqSPz",
                    "EAP0txPZFX9Ehcxge8RE44YaGIIa8jqY78krRnPqNY"
                );

            ThreadPool.QueueUserWorkItem(o =>
            {
                var msg = "#" + trade.Symbol;
                msg += trade.Type > 0 ? " C:" : " V:";
                msg += trade.EntryValue;
                msg += trade.StopLoss.HasValue ? " L:" + trade.StopLoss : " L: Fech.";
                msg += trade.StopGain.HasValue ? " G:" + trade.StopGain : " G: Fech.";
                msg += DateTime.Today.ToString(" #DT #AUTO <yyyyMMdd>", System.Globalization.CultureInfo.CurrentCulture);

                try { svc.SendTweet(new SendTweetOptions { Status = msg }); }
                catch (Exception err) { System.Diagnostics.Debug.WriteLine(err.Message); };
            });
        }
    }
}
