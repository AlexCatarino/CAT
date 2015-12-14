using System.Collections.Concurrent;
using System.Threading.Tasks;
using CAT.Model;
using Microsoft.AspNet.SignalR;
using System.Linq;
using System;
using Microsoft.AspNet.SignalR.Hubs;
using System.Collections.Generic;

namespace CAT.Azure.Hubs
{
    [HubName("TradeHub")]
    public class TradeHub : Hub
    {
        public void GetTradeList()
        {
            this.Clients.All.storedTrades(Global.Trades.Values.OrderBy(x => x.EntryTime).ToList());
        }

        public Task SendMessage(Trade trade)
        {
            if(Global.Trades.TryAdd(trade.Id, trade)) return this.Clients.All.addTrade2Page(trade);

            Global.Trades[trade.Id] = trade;
            return this.Clients.All.updateTrade(trade);
        }
    }
}