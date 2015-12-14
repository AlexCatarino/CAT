using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Hubs;

namespace CAT.Model
{
    public class API_Azure
    {
        private static bool IsDeveloper 
        {
            get { return WindowsIdentity.GetCurrent().Name == @"nirvana\alex"; }
        }
        private static readonly IHubProxy hub;
        private static readonly HubConnection connection;

        static API_Azure()
        {
            try
            {
                connection = new HubConnection("http://samurais.azurewebsites.net");
                hub = connection.CreateHubProxy("TradeHub");
                connection.StateChanged += (obj) =>
                {
                    if (obj.OldState == ConnectionState.Connected) Connect();
                    Debug.WriteLine("From " + obj.OldState + " to " + obj.NewState);
                };

                if (!IsDeveloper) connection.Stop();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public static void Connect()
        {
            if (!IsDeveloper) return; // Only connects with my user "nirvana\alex"

            if (connection.State == ConnectionState.Connected) return;

            connection.Start();
        }

        public static void Send(Trade trade)
        {
            // Only sends trades with my user "nirvana\alex"
            if (!IsDeveloper || trade == null) return;
            if (trade.SetupId == 4) return;

            ThreadPool.QueueUserWorkItem(obj =>
            {
                if (connection.State != ConnectionState.Connected) return;
                
                hub.Invoke("SendMessage", trade);
            });
        }

        public void Send(string a, string b, string c, string d, string e, string f)
        {
            if (connection.State != ConnectionState.Connected) return;

            var trade = new Trade { Symbol = a, EntryTime = DateTime.Now };

            ThreadPool.QueueUserWorkItem(obj => { hub.Invoke("SendMessage", trade); });
        }
    }
}
