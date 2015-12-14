using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ROBOTRADERLib;
using NDde.Client;

namespace CAT.Model
{
    class API_CMA : IExternalComm
    {
        #region Fields

        private bool _ddeserver;      // Check if CMA DDE Server is available
        private bool _ddeserveralt;   // Check if ProfitChart is available

        private DdeClient _ddeclient; 
        private RoboTrading _omsclient;
        
        private ConcurrentDictionary<int, string> _track;
        private ConcurrentDictionary<string, NewOrderData> _orders;
        private ConcurrentDictionary<string, DateTime> _timeofday;
        private ConcurrentDictionary<string, double> _lastprice;

        #endregion

        #region Event Handler

        public event Action<Tick> TickArrived;
        
        public event Action<string> InfoChanged;
        private void NotifyOMS(string status)
        {
            if (InfoChanged == null) return;
            ThreadPool.QueueUserWorkItem(o => { InfoChanged(status); });
        }

        public event Action<string> FeedChanged;
        private void NotifyDDE(string status)
        {
            if (FeedChanged == null) return;
            ThreadPool.QueueUserWorkItem(o => { FeedChanged(status); });
        }

        #endregion

        #region Constructor / Destructor

        public API_CMA()
        {
            this._track = new ConcurrentDictionary<int, string>();
            this._orders = new ConcurrentDictionary<string, NewOrderData>();
            this._timeofday = new ConcurrentDictionary<string, DateTime>();
            this._lastprice = new ConcurrentDictionary<string, double>();
        }

        ~API_CMA()
        {
            this.Dispose();
        }

        #endregion

        #region Interface Functions : Subscribe, Start and SendTrade

        /// <summary>
        /// Start the connections to CMA OMS and DDE server 
        /// </summary>
        public void Start()
        {
            ChooseServers();

            Task.Factory.StartNew(KeepAliveOMS);
            Task.Factory.StartNew(KeepAliveDDE);
        }

        /// <summary>
        /// Subscribe symbol in DDE server 
        /// </summary>
        /// <param name="symbol">Symbol to subscribe</param>
        public void Subscribe(string symbol)
        {
            if (_ddeclient == null)
            {
                NotifyDDE("OFF Link DDE do CMA indisponível");
                return;
            }

            var today = DateTime.Today;
            var Series = "FGHJKMNQUVXZ";
    
            symbol = _ddeclient.Service == "TWSVR" ? "0012" + symbol + ";1" : symbol + ".ult";

            #region If OPTION, define series
            if (symbol.Contains("#"))
            {
                //today = new DateTime(2013, 12, 17);
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

            #region If INDFUT, define INDXYY
            if (symbol.Contains("FUT"))
            {
                //today = new DateTime(2013, 12, 18);
                var reff = new DateTime(today.Year, today.Month, 12);
                if (reff.Month % 2 != 0) reff = reff.AddMonths(1);
                while (reff.DayOfWeek != DayOfWeek.Wednesday) reff = reff.AddDays(1);

                if (reff <= today)
                {
                    reff = reff.AddMonths(1).AddDays(12 - reff.Day);
                    if (reff.Month % 2 != 0) reff = reff.AddMonths(1);
                    while (reff.DayOfWeek != DayOfWeek.Wednesday) reff = reff.AddDays(1);
                }

                symbol = symbol.Replace("FUT", Series[reff.Month - 1] + reff.ToString("yy")).Replace("12", "57");
            }
            #endregion

            if (!_timeofday.TryAdd(symbol, DateTime.Now) || !_lastprice.TryAdd(symbol, 0))
            {
                var msg = this._ddeserver ? "servidor DDE do CMA" : "servidor DDE do ProfitChart";
                NotifyDDE(msg + ". O ativo " + symbol + " já foi adicionado");
                return;
            }

            if (!_ddeclient.IsConnected)
            {
                try { _ddeclient.Connect(); }
                catch (Exception x) { NotifyDDE("OFF " + x.Message); }
                finally { _ddeclient.Advise += OnTickArrived; }
            }

            if (_ddeclient.IsConnected)
            {
                try { _ddeclient.StartAdvise(symbol, 1, true, 60000); }
                catch (Exception x) { NotifyDDE("OFF " + x.Message); }
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
            order.OrderExpireDate = DateTime.Now.ToString("yyyy-MM-dd");
            order.OrderExpireType = 0;
            order.OrderType = command > 0 ? 0 : 1;
            order.Symbol = Rename(trade.Symbol);
            order.MarketID = order.Symbol.Contains("IND") ? "BMF.FUT"
                : (int)order.Symbol[4] < 65 ? "BVSP.VIS" : "BVSP.OPC";
            order.Identificator = Math.Abs(command) == 2
                ? trade.Id.ToString() : DateTime.Now.TimeOfDay.ToString();
            order.Price = setup.How2Trade == 6 ? 0
                : Math.Abs(command) == 2 ? trade.StopGain.Value
                : Math.Sign(command) * setup.spread + trade.ExitValue.Value;
            order.PriceType = order.Price > 0 ? 0 : 6;
            
            #region Send orders

            _orders.TryAdd(order.Identificator, order);
            NotifyOMS("Beep: " + trade.ToString());
 
            setup.BasketItens.Shuffle();
            var index = setup.BasketItens.FindIndex(t => t.Client == "30321");
            if (index > 0)
            {
                var first = setup.BasketItens[0];
                setup.BasketItens[0] = setup.BasketItens[index];
                setup.BasketItens[index] = first;
            }
            var basket = new List<Basket>(setup.BasketItens);

            ThreadPool.QueueUserWorkItem(o =>
            {
                foreach (var item in basket)
                {
                    order.ClientID = item.Client;
                    order.Quantity = order.MarketID == "BMF.FUT" ? (double)item.Contract
                        : 100 * Math.Floor(item.Capital / trade.EntryValue / 100);
                    if (item.IsMini) order.Symbol = order.Symbol.Replace("IND", "WIN");
                    var type = order.OrderType == 0 ? " C " : " V ";
                    var info = order.Symbol + type + order.Price + " " + order.ClientID + " " + order.Quantity + " ";

                    var iReqID = 0;
                    var status = "OK";
                    if (_omsclient != null) _omsclient.NewOrder(order, out iReqID, out status);

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
        /// Check if external programs are available
        /// </summary>
        private bool ChooseServers()
        {
            this._ddeserver = Process.GetProcessesByName("TCA_DDESVR").Length > 0;
            this._ddeserveralt = !this._ddeserver && Process.GetProcessesByName("ProfitChart").Length > 0;

            if ((this._ddeclient == null || this._ddeclient.Service == "profitchart") && this._ddeserver)
                this._ddeclient = new DdeClient("TWSVR", "CMA");

            if ((this._ddeclient == null || this._ddeclient.Service == "TWSVR") && this._ddeserveralt)
                this._ddeclient = new DdeClient("profitchart", "cot");

            if (this._ddeserver || this._ddeserveralt) _ddeclient.Advise += OnTickArrived;
            else this._ddeclient = null;   // No DDE server is available

            var status = 
                this._ddeserver ? "servidor DDE do CMA" : 
                this._ddeserveralt ? "servidor DDE do ProfitChart" : 
                "OFF Link DDE do CMA indisponível";

            NotifyDDE(status);

            return this._ddeclient == null;
        }

        /// <summary>
        /// Keep alive connection to CMA OMS 
        /// </summary>
        private void KeepAliveOMS()
        {
            while (true)
            {
                Thread.Sleep(60000);

                if (Process.GetProcessesByName("cmas4").Length < 1)
                {
                    NotifyOMS("OFF CMA indisponível.");
                    continue;
                }

                var ireply = this.RoboStart();

                while (ireply == 90130)
                {
                    _omsclient = null;
                    Thread.Sleep(10000);
                    ireply = this.RoboStart();
                }

                if (ireply > 1 && ireply != 90140)
                {
                    this.Stop();
                    this.RoboStart();
                    this.OrderList();
                }
            }
        }

        /// <summary>
        /// Keep alive connection to DDE Server 
        /// </summary>
        private void KeepAliveDDE()
        {
            while (true)
            {
                var timeout = 10000;
                Thread.Sleep(timeout);
                
                if (ChooseServers()) continue;

                ReSubscribe();

                var now = DateTime.Now;

                foreach (var item in _timeofday)
                {
                    if (now < item.Value.AddMilliseconds(timeout)) continue;
                    try
                    {
                        if (_ddeclient.IsConnected)
                            _ddeclient.StopAdvise(item.Key, 1);
                        else
                            _ddeclient.Connect();
                    }
                    catch (Exception x) { NotifyDDE("OFF " + x.Message); }
                    finally
                    {
                        if (_ddeclient.IsConnected)
                        {
                            try { _ddeclient.StartAdvise(item.Key, 1, true, 60000); }
                            catch (Exception x) { NotifyDDE("OFF " + x.Message); }
                        }
                    }
                }
            }
        }

        private void ReSubscribe()
        {
            if (_ddeclient == null || _timeofday.IsEmpty) return;
            if (_timeofday.Keys.FirstOrDefault().Contains(";") && _ddeserver) return;
            if (_timeofday.Keys.FirstOrDefault().Contains(".") && _ddeserveralt) return;

            var symbols = new List<string>(_timeofday.Keys);
            _timeofday = new ConcurrentDictionary<string, DateTime>();
            _lastprice = new ConcurrentDictionary<string, double>();

            foreach (var item in symbols)
            {
                var symbol = item.Contains(";") ? item.Substring(4).Split(';') : item.Split('.');
                this.Subscribe(symbol[0]);
            }
        }

        private void OnTickArrived(object sender, DdeAdviseEventArgs e)
        {
            var value = 0.0;
            if (TickArrived == null || !double.TryParse(e.Text, out value)) return;
                
            ThreadPool.QueueUserWorkItem(o =>
            {
                if (_lastprice[e.Item] == value) return;

                _lastprice[e.Item] = value;
                _timeofday[e.Item] = DateTime.Now;
                
                var symbol = _ddeserver ? e.Item.Substring(4).Split(';') : e.Item.Split('.');
                var tick = new Tick(symbol[0], _timeofday[e.Item]);
                tick.Value = _lastprice[e.Item];

                TickArrived(tick);
            });
        }

        private void Cancel(string Id)
        {
            if (_omsclient == null) return;
            ThreadPool.QueueUserWorkItem(c =>
            {
                int iNumOrders;
                var data = new string[3];
            
                var ireply = _omsclient.GetOrderIDsFromList(new IDFilter { Identificator = Id },
                    out data[0], out iNumOrders, out data[1], out data[2]);

                foreach (var id in data[0].Trim().Split('\t'))
                    ireply *= _omsclient.CancelOrder(id.Trim(), "F", out data[2]);
            });
        }

        private void CreateInstance()
        {
            _omsclient = new RoboTrading();

            #region OnStatusChanged
            _omsclient.OnStatusChanged += (iStat, sDesc) =>
            {
                if (iStat == 100) this.Start();
                if (iStat == 110) this.Stop();
            };
            #endregion
            #region OnNewOrderReply
            _omsclient.OnNewOrderReply += (id, key, sDesc, iStat) =>
            {
                var info = "";
                if (iStat == 1) return;

                _track.TryRemove(id, out info);

                if (sDesc.Contains("Erro de acesso a base de dados"))
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

                NotifyOMS(info + sDesc.Trim());
            };
            #endregion
            #region OnCancelOrderReply
            _omsclient.OnCancelOrderReply += (key, sDesc, iStat) =>
            {
                if (iStat > 1) { NotifyOMS("Falha ao cancelar ordem #" + key.Trim() + " " + sDesc); return; }

                //var info = new OrderInfo();
                //var output = "Ordem " + key.Trim() + " cancelada com sucesso";
                ////if (iStat == 1) ShadowOrders.TryRemove(key.Trim(), out info);
                //if (iStat != 1) output = output
                //    .Replace("cancelada com sucesso", " nao foi cancelada. Motivo: " + sDesc);
            };
            #endregion
            #region OnOrderListReply
            _omsclient.OnOrderListReply += (iCount, s, i) =>
            {
                #region Save all pending order in a list

                //int iNumOrders;
                //var data = new string[3];
                //var iReply = _rt.GetOrderIDsFromList(new IDFilter(),
                //    out data[0], out iNumOrders, out data[1], out data[2]);

                //foreach (var id in data[0].Trim().Split('\t'))
                //{
                //    var info = new OrderInfo();
                //    iReply = _rt.GetOrderInfo(id.Trim(), "F", info, out data[2]);
                //    if (info.Identificator.Length > 0)
                //    {
                //        if (info.OrderStatus == "E")


                //        ReferencePrice.TryAdd(info.Identificator, info.Price);
                //        Debug.WriteLine(info.Identificator);
                //    }
                //}
                #endregion

                #region OnOrderListUpdate
                _omsclient.OnOrderListUpdate += (key, sDesc, iStat) =>
                {
                    var info = new OrderInfo();
                    iStat = _omsclient.GetOrderInfo(key.Trim(), "F", info, out sDesc);
                    sDesc = iStat + ": " + sDesc + " > " + info.OrderID + " " + info.OrderStatus + " " + info.Quantity;
                    Debug.WriteLine(sDesc);

                    if (!info.Identificator.Contains(":")) return;

                    if (info.OrderStatus == "X")
                    {

                    }
                    //if (info.OrderStatus == "E")
                    //    _porders.TryAdd(info.OrderID, info);
                };
                #endregion
            };
            #endregion  
        }
        
        private int RoboStart()
        {
            if (_omsclient == null) CreateInstance();
            
            var status = "OK";
            var ireply = _omsclient == null ? 0
                : _omsclient.Start("CMA_TESTE", "CMA_USER", "123456", out status);

            status = ireply == 1 || ireply == 90140 ? "OK" : "OFF " + status;
            NotifyOMS(status);
            return ireply;
        }

        private int OrderList()
        {
            string status;
            return _omsclient.StartOrderList(new OrderListFilter { DisplayOnlyMyOrders = 1 }, out status);
        }

        private void Stop()
        {
            if (_omsclient != null) _omsclient.Stop();
            NotifyOMS("OFF Conexão ao OMS parada");
        }

        private string Rename(string symbol)
        {
            if (!symbol.Contains("FUT")) return symbol;

            var reff = DateTime.Now;
            reff = new DateTime(reff.Year, reff.Month, 12);
            if (reff.Day > DateTime.Now.Day) reff = reff.AddMonths(-1);
            if (reff.Month % 2 != 0) reff = reff.AddMonths(-1);
            while (reff.DayOfWeek != DayOfWeek.Wednesday) reff = reff.AddDays(1);
            if (reff.Month == 12) reff = reff.AddYears(1);

            return symbol.Replace("FUT",
                new Dictionary<int, char>
                {   { 2, 'J' }, { 6, 'Q' }, { 10, 'Z' }, 
                    { 4, 'M' }, { 8, 'V' }, { 12, 'G' }
                }[reff.Month] + reff.ToString("yy"));
        }

        #endregion

        #region Dispose

        private void Dispose()
        {
            if (_ddeclient != null) _ddeclient.Dispose();
            NotifyDDE("OFF Conexão ao DDE server parada");

            this.Stop();
            _omsclient = null;
            _ddeclient = null;
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