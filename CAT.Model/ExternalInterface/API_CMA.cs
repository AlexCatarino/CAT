namespace CAT.Model
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using DdeNet.Client;
    using ROBOTRADERLib;
    using System.Threading.Tasks;

    class API_CMA : IExternalComm, IDisposable
    {
        #region Fields

        private DateTime BvspTime 
        { 
            get 
            {
                return TimeZoneInfo.ConvertTime(DateTime.Now, 
                    TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"));
            } 
        }

        // Any DDE server
        private ConcurrentDictionary<string, DateTime> _lasttime;

        // CMA DDE Server Fields
        private bool _keealivecma = true;
        private DdeClient _cmaClient;   // DDE Client
        private ConcurrentDictionary<string, DateTime> _cmaSymbols;

        // Nelógica ProfitChart Fields
        private bool _keealivenpc = true;
        private DdeClient _npcClient;   // DDE Client
        private ConcurrentDictionary<string, DateTime> _npcSymbols;

        // RoboTrader Fields
        private bool _keealiveoms = true;
        private RoboTrading _omsclient;
        private ConcurrentDictionary<int, string> _track;
        private ConcurrentDictionary<string, NewOrderData> _orders;
        
        // Azure Fields
        private TableBatchOperation _batchOperation;
        private CloudTable Table
        {
            get
            {
                return CloudStorageAccount.Parse(
                    "DefaultEndpointsProtocol=https;" +
                    "AccountName=catarino;" +
                    "AccountKey=pQFN//7LQ5L0ki0AyUKcwTBUz9i9F07zoX5iFezs9Q4Fm4mm/WsF8MmKmOwZVmNdOpOGr7NZnJs5Jy4ZX6RmsQ==")
                    .CreateCloudTableClient().GetTableReference("Ticks");
            }
        }
        private static bool IsDeveloper
        {
            get { return System.Security.Principal.WindowsIdentity.GetCurrent().Name == @"nirvana\alex"; }
        }

        private bool[] CheckProcesses
        {
            get
            {
                return new bool[]
                {
                    Process.GetProcessesByName("TCA_DDESVR").Length > 0,
                    Process.GetProcessesByName("ProfitChart").Length > 0,
                    Process.GetProcessesByName("cmas4").Length > 0
                };
            }
        }

        private bool NoneProcessAvailable
        {
            get
            {
                return !CheckProcesses.Contains(true);
            }
        }

        private bool IsLogged 
        {
            get
            {
                var strMarkets = "";
                try { _omsclient.GetEnabledMarkets(out strMarkets); }
                catch (Exception) { };
                return !string.IsNullOrEmpty(strMarkets);
            }
        }

        #endregion

        #region Event Handler

        public event Action<Tick> TickArrived;
        
        public event Action<string> InfoChanged;
        private void NotifyOMS(string status)
        {
            if (InfoChanged == null) return;
            Task.Factory.StartNew(() => InfoChanged(status));
        }

        public event Action<string> FeedChanged;
        private void NotifyDDE(string status)
        {
            if (FeedChanged == null) return;
            Task.Factory.StartNew(() => FeedChanged(status));
        }

        #endregion

        #region Constructor / Destructor

        public API_CMA()
        {
            this._batchOperation = new TableBatchOperation();
            this._track = new ConcurrentDictionary<int, string>();
            this._orders = new ConcurrentDictionary<string, NewOrderData>();
            this._lasttime = new ConcurrentDictionary<string, DateTime>();

            this._keealiveoms = true;
            this._omsclient = new RoboTrading();

            this._omsclient.OnStatusChanged += _omsclient_OnStatusChanged;
            this._omsclient.OnNewOrderReply += _omsclient_OnNewOrderReply;
            this._omsclient.OnCancelOrderReply += _omsclient_OnCancelOrderReply;
            this._omsclient.OnOrderListReply += _omsclient_OnOrderListReply;
            this._omsclient.OnOrderListUpdate += _omsclient_OnOrderListUpdate;

            this._lasttime.TryAdd("NPC", BvspTime);
            this._keealivenpc = true;
            this._npcClient = new DdeClient("profitchart", "cot");
            this._npcClient.Advise += OnTickArrived;
            this._npcClient.Disconnected += (s, e) => { IsDDEAvailable(_npcClient); };
            this._npcSymbols = new ConcurrentDictionary<string, DateTime>();

            this._lasttime.TryAdd("CMA", BvspTime);
            this._keealivecma = true;
            this._cmaClient = new DdeClient("TWSVR", "CMA");
            this._cmaClient.Advise += OnTickArrived;
            this._cmaClient.Disconnected += (s, e) => { IsDDEAvailable(_cmaClient); };
            this._cmaSymbols = new ConcurrentDictionary<string, DateTime>();
        }

        ~API_CMA()
        {
            this.Dispose();
        }

        #region Dispose
        public void Dispose()
        {
            NotifyDDE("OFF Conexão ao DDE server parada");

            _keealiveoms = false;
            _keealivecma = false;
            _keealivenpc = false;

            if (_cmaClient != null) _cmaClient.Dispose();
            if (_npcClient != null) _npcClient.Dispose();

            _omsclient = null;
            _cmaClient = null;
            _npcClient = null;
        }

        #endregion
        #endregion

        #region Interface Functions : Subscribe, Start and SendTrade

        /// <summary>
        /// Start the connections to CMA OMS and DDE server 
        /// </summary>
        public void Start()
        {
            if (!IsOMSAvailable()) NotifyOMS("OFF CMA não está loggado");
            if (IsDDEAvailable(_cmaClient)) NotifyDDE("CMA DDE Server");
            if (IsDDEAvailable(_npcClient)) NotifyDDE("ProfitChart DDE");
            
            Task.Factory.StartNew(KeepAliveOMS);
            Task.Factory.StartNew(KeepAliveCMA);
            Task.Factory.StartNew(KeepAliveNPC);
        }

        /// <summary>
        /// Subscribe symbol in DDE server 
        /// </summary>
        /// <param name="symbol">Symbol to subscribe</param>
        public void Subscribe(string symbol)
        {
            var market = !symbol.Contains("FUT") ? "0012" : "0057";
            
            #region If OPTION, define series
            if (symbol.Contains("#"))
            {
                var today = DateTime.Today;
                var reff = new DateTime(today.Year, today.Month, 15);
                while (reff.DayOfWeek != DayOfWeek.Monday) reff = reff.AddDays(1);

                if (reff <= today)
                {
                    reff = reff.AddMonths(1).AddDays(15 - reff.Day);
                    while (reff.DayOfWeek != DayOfWeek.Monday) reff = reff.AddDays(1);
                }

                symbol = symbol.Replace('#', (char)(64 + reff.Month));
            }
            #endregion
            
            symbol = Rename(symbol);
            var npcSymbol = symbol + ".ult";
            var cmaSymbol = market + symbol + ";1";

            if (_npcSymbols.TryAdd(npcSymbol, BvspTime) && IsDDEAvailable(_npcClient))
            {
                try { _npcClient.StartAdvise(npcSymbol, 1, true, 60000); }
                catch (Exception x) { NotifyDDE("OFF PFT: " + x.Message); }
            }

            if (_cmaSymbols.TryAdd(cmaSymbol, BvspTime) && IsDDEAvailable(_cmaClient))
            {
                try { _cmaClient.StartAdvise(cmaSymbol, 1, true, 60000); }
                catch (Exception x) { NotifyDDE("OFF CMA: " + x.Message); }
            }

        }

        /// <summary>
        /// Send the trade to OMS 
        /// </summary>
        /// <param name="setup">Setup</param>
        /// <param name="trade">Trade to send</param>
        /// <param name="command">How to process the trade</param>
        public void SendTrade(Setup setup, Trade trade, int command)
        {
            if (setup.How2Trade * command == 0) return;

            var order = new NewOrderData { BranchID = "Standard" };
            order.OrderExpireDate = DateTime.Today.ToString("yyyy-MM-dd");
            order.OrderExpireType = 0;
            order.OrderType = command > 0 ? 0 : 1;
            order.Symbol = Rename(trade.Symbol);
            order.MarketID = trade.AssetClass;
            order.Identificator = Math.Abs(command) == 2
                ? trade.SetupId.ToString() : BvspTime.TimeOfDay.ToString();
            order.Price = setup.How2Trade == 6 ? 0
                : Math.Abs(command) == 2 ? (double)trade.StopGain.Value
                : Math.Sign(command) * (double)setup.Slippage + (double)trade.ExitValue.Value;
            order.PriceType = order.Price > 0 ? 0 : 6;
            
            #region Send orders

            _orders.TryAdd(order.Identificator, order);
            
            setup.Basket.Shuffle();
            var index = setup.Basket.FindIndex(t => t.ClientID == "30516");
            if (index > 0)
            {
                var first = setup.Basket[0];
                setup.Basket[0] = setup.Basket[index];
                setup.Basket[index] = first;
            }
            var basket = new List<Client>(setup.Basket);
            var isLogged = this.IsLogged;

            Task.Factory.StartNew(() =>
            {
                foreach (var item in basket)
                {
                    order.ClientID = item.ClientID;
                    order.Quantity = (double)item.SetQnty(trade.EntryValue, "BMF.FUT");
                    if (item.IsMini) order.Symbol = order.Symbol.Replace("IND", "WIN");
                    var type = order.OrderType == 0 ? " C " : " V ";
                    var info = order.Symbol + type + order.Price + " " + order.ClientID + " " + order.Quantity + " ";

                    var iReqID = 0;
                    var status = "OK";
                    if (isLogged) _omsclient.NewOrder(order, out iReqID, out status);

                    if (status == "OK") NotifyOMS(info);
                    else NotifyOMS("OFF " + status + " " + info);
                    _track.TryAdd(iReqID, order.Identificator + " " + info);
                }
            });
            #endregion

            //if (Math.Abs(command) != 2) this.Cancel(trade.Id.ToString());
        }

        #endregion

        #region Private Functions

        /// <summary>
        /// Check if OMS is available
        /// </summary>
        private bool IsOMSAvailable()
        {
            string status;
            if (!CheckProcesses[2]) return false;

            _omsclient.Start("CMA_TESTE", "CMA_USER", "123456", out status);
            return _omsclient.StartOrderList(new OrderListFilter { DisplayOnlyMyOrders = 1 }, out status) == 1;
        }
       
        /// <summary>
        /// Check if external programs are available
        /// </summary>
        private bool IsDDEAvailable(DdeClient client)
        {
            var index = client.Topic == "CMA" ? 0 : 1;
            var server = index == 0 ? "CMA TWSRV" : "ProfitChart RT";
            
            if (client.IsConnected) { NotifyDDE(server); return true; }
            if (NoneProcessAvailable) NotifyDDE("OFF Sem servidor DDE!");
            if (!CheckProcesses[index]) return false;
            
            try { client.Connect(); }
            catch { return false; }

            return true;
        }

        /// <summary>
        /// Keep alive connection to CMA OMS 
        /// </summary>
        private void KeepAliveOMS()
        {
            while (_keealiveoms)
            {
                System.Threading.Thread.Sleep(60000);
                
                if (IsLogged) continue;
                if (!IsOMSAvailable()) NotifyOMS("OFF CMA não está loggado");

                #region old
                //continue;

                //if (NotLogged) continue;

                //int ireply;
                //while (ireply == 90130)
                //{
                //    _omsclient = null;
                //    Thread.Sleep(10000);
                //    ireply = this.RoboStart();
                //}

                //if (ireply > 1 && ireply != 90140)
                //{
                //    this.RoboStop();
                //    this.RoboStart();
                //    this.OrderList();
                //}
                #endregion
            }
        }

        /// <summary>
        /// Keep alive connection to CMA DDE Server 
        /// </summary>
        private void KeepAliveCMA()
        {
            while (_keealivecma)
            {
                var timeout = 10000;
                System.Threading.Thread.Sleep(timeout);

                var span = (BvspTime - _lasttime["CMA"]).TotalMilliseconds;
                if (timeout > span || !IsDDEAvailable(_cmaClient)) continue;

                NotifyDDE("OFF CMA: " + (int)span / 1e3 + " segundos sem novos dados.");

                foreach (var item in _cmaSymbols)
                {
                    try { _cmaClient.StopAdvise(item.Key, 1); }
                    catch (Exception x) { NotifyDDE("OFF CMA: " + x.Message); }
                }

                foreach (var item in _cmaSymbols)
                {
                    try { _cmaClient.StartAdvise(item.Key, 1, true, 60000); NotifyDDE("CMA DDE Server"); }
                    catch (Exception x) { NotifyDDE("OFF CMA: " + x.Message); }
                }
            }
        }

        /// <summary>
        /// Keep alive connection to ProfitChart DDE 
        /// </summary>
        private void KeepAliveNPC()
        {
            while (_keealivenpc)
            {
                var timeout = 10000;
                System.Threading.Thread.Sleep(timeout);

                var span = (BvspTime - _lasttime["NPC"]).TotalMilliseconds;
                if (timeout > span || !IsDDEAvailable(_npcClient)) continue;

                NotifyDDE("OFF NPC: " + (int)span / 1e3 + " segundos sem novos dados.");
                
                foreach (var item in _npcSymbols)
                {
                    try { _npcClient.StopAdvise(item.Key, 1); }
                    catch (Exception x) { NotifyDDE("OFF NPC: " + x.Message); }
                }

                foreach (var item in _npcSymbols)
                {
                    try { _npcClient.StartAdvise(item.Key, 1, true, 60000); NotifyDDE("ProfitChart RT"); }
                    catch (Exception x) { NotifyDDE("OFF NPC: " + x.Message); }
                }
            }
        }

        private void OnTickArrived(object sender, DdeAdviseEventArgs e)
        {
            var value = 0m;
            if (TickArrived == null || !decimal.TryParse(e.Text, out value)) return;
            if (BvspTime.Hour < 9 || value == 0) return;
            
            Task.Factory.StartNew(() =>
            {
                var symbol = e.Item.Split('.')[0].Split(';')[0];
                var dde = e.Item.Contains(";") ? "CMA" : "NPC";
                if (dde == "CMA") symbol = symbol.Substring(4);
                
                var tick = new Tick(symbol, BvspTime, value);

                if (BvspTime.Hour == 9) if (!tick.GetClass().Contains("BMF")) return;

                TickArrived(tick); //NotifyOMS(tick.ToString() + " from " + dde);
                _lasttime[dde] = tick.Time;
                
                //if (!IsDeveloper) return;
                //Table.Execute(TableOperation.InsertOrReplace(tick));
            });
        }

        private void Cancel(string Id)
        {
            if (!IsLogged) return;
            Task.Factory.StartNew(() =>
            {
                int iNumOrders;
                var data = new string[3];
            
                var ireply = _omsclient.GetOrderIDsFromList(new IDFilter { Identificator = Id },
                    out data[0], out iNumOrders, out data[1], out data[2]);

                foreach (var id in data[0].Trim().Split('\t'))
                    ireply *= _omsclient.CancelOrder(id.Trim(), "F", out data[2]);
            });
        }
        
        private int RoboStart()
        {
            var status = "OK";
            var ireply = _omsclient == null ? 0
                : _omsclient.Start("CMA_TESTE", "CMA_USER", "123456", out status);

            if (ireply != 0) _omsclient.GetEnabledMarkets(out status);

            status = string.IsNullOrEmpty(status) ? "OFF CMA não está loggado"
                : ireply == 0 ? "OFF CMA indisponível" : "OK";

            NotifyOMS(status);
                
            return ireply;
        }

        private int OrderList()
        {
            string status;
            return _omsclient.StartOrderList(new OrderListFilter { DisplayOnlyMyOrders = 1 }, out status);
        }

        private void RoboStop()
        {
            if (IsLogged) _omsclient.Stop();
            NotifyOMS("OFF Conexão ao OMS parada");
        }

        private string Rename(string symbol)
        {
            if (!symbol.Contains("FUT")) return symbol;

            var reff = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 12);
            if (reff.Month % 2 != 0) reff = reff.AddMonths(1);
            while (reff.DayOfWeek != DayOfWeek.Wednesday) reff = reff.AddDays(1);

            if (reff <= DateTime.Today)
            {
                reff = reff.AddMonths(1).AddDays(12 - reff.Day);
                if (reff.Month % 2 != 0) reff = reff.AddMonths(1);
                while (reff.DayOfWeek != DayOfWeek.Wednesday) reff = reff.AddDays(1);
            }
            
            var Series = "FGHJKMNQUVXZ";
            return symbol.Replace("FUT", Series[reff.Month - 1] + reff.ToString("yy"));
        }

        #endregion

        #region Internal EventHandlers

        /// <summary>
        /// Check if external programs are available
        /// </summary>
        internal void _omsclient_OnStatusChanged(int lStatus, string strDescription)
        {
            var ok = lStatus == 120 ? "OK " : "OFF ";
            NotifyOMS(ok + strDescription.Replace("OFF", ""));
            if (lStatus == 110) this.RoboStop();
        }

        /// <summary>
        /// Check if external programs are available
        /// </summary>
        internal void _omsclient_OnNewOrderReply(int lRequestID, string strOrders, string strDescription, int lStatus)
        {
            var info = "";
            if (lStatus == 1) return;

            _track.TryRemove(lRequestID, out info);

            if (strDescription.Contains("Erro de acesso a base de dados"))
            {
                //    var detail = info.Split(' ');
                //    var order = new NewOrderData();
                //    _orders.TryGetValue(detail[0], out order);
                //    order.Symbol = detail[1];
                //    order.ClientID = detail[4];
                //    order.Quantity = double.Parse(detail[5]);

                //    var iReqID = 0;
                //    var status = "OK";
                //    if (_rt != null) _rt.NewOrder(order, out iReqID, out status);

                //    if (status != "OK") OnStatusChanged("Nova tentativa para: " + info);
                //    else OnStatusChanged("OFF " + status + " " + info);
                //    _track.TryAdd(iReqID, info);

                //    return;
            }

            NotifyOMS(info + strDescription.Trim());
        }

        /// <summary>
        /// Check if external programs are available
        /// </summary>
        internal void _omsclient_OnCancelOrderReply(string strOrder, string strDescription, int lStatus)
        {
            if (lStatus > 1) { NotifyOMS("Falha ao cancelar ordem #" + strOrder.Trim() + " " + strDescription); return; }

            //var info = new OrderInfo();
            //var output = "Ordem " + key.Trim() + " cancelada com sucesso";
            ////if (iStat == 1) ShadowOrders.TryRemove(key.Trim(), out info);
            //if (iStat != 1) output = output
            //    .Replace("cancelada com sucesso", " nao foi cancelada. Motivo: " + sDesc);
        }

        /// <summary>
        /// Check if external programs are available
        /// </summary>
        internal void _omsclient_OnOrderListReply(int lTotalListCount, string strDescription, int lStatus)
        {
            int iNumOrders;
            var data = new string[3];
            //var iReply = _rt.GetOrderIDsFromList(new IDFilter(),
            //    out data[0], out iNumOrders, out data[1], out data[2]);

            //foreach (var id in data[0].Trim().Split('\t'))
            //{
            //    var info = new OrderInfo();
            //    iReply = _rt.GetOrderInfo(id.Trim(), "F", info, out data[2]);
            //    if (info.Identificator.Length > 0)
            //    {
            //        if (info.OrderStatus == "E")


            //            ReferencePrice.TryAdd(info.Identificator, info.Price);
            //        Debug.WriteLine(info.Identificator);
            //    }
            //}
        }

        /// <summary>
        /// Check if external programs are available
        /// </summary>
        internal void _omsclient_OnOrderListUpdate(string strOrdersChanged, string strOrdersRemoved, int lTotalListCount)
        {
            var info = new OrderInfo();
            //iStat = _omsclient.GetOrderInfo(key.Trim(), "F", info, out sDesc);
            //sDesc = iStat + ": " + sDesc + " > " + info.OrderID + " " + info.OrderStatus + " " + info.Quantity;
            //Debug.WriteLine(sDesc);

            if (!info.Identificator.Contains(":")) return;

            if (info.OrderStatus == "X")
            {

            }
            //if (info.OrderStatus == "E")
            //    _porders.TryAdd(info.OrderID, info);
        }

        #endregion

    }

    #region Shuffle list

    static class Ext
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var p = new System.Security.Cryptography.RNGCryptoServiceProvider();
            var n = list.Count;
            while (n > 1)
            {
                var box = new byte[1];
                do p.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                var k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    #endregion
}