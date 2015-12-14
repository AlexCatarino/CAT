using System;

namespace CAT.Model
{
    interface IExternalComm
    {
        void Start();
        void SendTrade(Setup setup, Trade trade, int command);
        void Subscribe(string symbol);
        
        event Action<Tick> TickArrived;
        event Action<string> InfoChanged;
        event Action<string> FeedChanged;
    }
}
