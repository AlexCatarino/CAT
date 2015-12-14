using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using CAT.Model;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace CAT.Azure.Hubs
{
    [HubName("TradeHub")]
    public class TradeHub : Hub
    {
        public void GetTradeList()
        {
            Clients.All.tradeListRequested();
        }

        public Task SendMessage(Trade trade)
        {
            //String formatedMessage = String.Format("{0}: {1}", Global.Users[Context.ConnectionId], message);

            return Clients.All.tradeMessage(trade);
        }
    }
}