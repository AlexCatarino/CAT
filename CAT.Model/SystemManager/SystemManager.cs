using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Optimera;
using Optimera.GA;

namespace CAT.Model
{
    public class SystemManager : IOptimisable, IDisposable
    {
        #region Strategies Dictionary

        private Dictionary<int, Strategy> DicStrategies
        {
            get
            {
                return new Dictionary<int, Strategy>
                {
                    {1, new ACap003()},
                    {2, new OCandle()},
                    {3, new OCandle()},
                    {4, new OCandle()},
                    {5, new OCandle()},
                    {6, new OCandle_v2()},
                    {7, new OCandle_v2()},
                    {8, new OCandle_v2()},
                    {9, new OCandle_v2()},
                    {10, new ACap002()},
                    {12, new ACap002()},
                    {13, new ACap002()},
                    {14, new ACap004()},
                    {15, new OCandle()},
                    {16, new OCandle()},
                    {17, new OCandle()},
                    {99, new DCap003()},
                };
            }
        }

        #endregion

        #region Fields

        private Object _object;

        private IExternalComm _externalComm;

        public Setup OptimizationSetup { get; set; }
        
        public ConcurrentBag<Task> _bag { get; set; }
        public ConcurrentDictionary<int, Strategy> Strategies { get; set; }
        public ConcurrentDictionary<int, List<Tick>> DicTicks { get; set; }
        private CancellationTokenSource tokenSource;

        public ObservableCollection<Trade> TradeList { get; set; }
        
        #endregion

        #region Constructor

        public SystemManager()
        {
            this._object = new Object();
            this.tokenSource = new CancellationTokenSource();
            this.TradeList = new ObservableCollection<Trade>();
            this.Strategies = new ConcurrentDictionary<int, Strategy>();
            this.DicTicks = new ConcurrentDictionary<int, List<Tick>>();
        }

        ~SystemManager()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            if (Strategies.Count() == 0) return;
            foreach (var item in Strategies) item.Value.Dispose();
        }

        #endregion
        
        #region Event Handlers

        public event Action<Tick> TickArrived;
        public event Action<string> CommChanged;
        public event Action<string> InfoChanged;
        private void OnInfoChanged(string status)
        {
            if (InfoChanged != null) InfoChanged(status);
        }

        public event Action<Trade, int> TradeArrived;
        protected void OnTradeArrived(Trade trade, int act)
        { 
            if (TradeArrived == null) return;
            TradeArrived(trade, act);
        }

        #endregion

        #region Private Methods

        private Strategy Initialize(Setup setup)
        {
            var strategy = DicStrategies[setup.SetupId];
            
            var file = String.Format("{0:ID_000}.dat", setup.SetupId);

            if (File.Exists(file))
            {
                try
                {
                    using (var input = File.OpenRead(file))
                        strategy = (Strategy)(new BinaryFormatter()).Deserialize(input);
                    strategy.CheckMrktHourChange(strategy.setup.DayTradeDuration, setup.DayTradeDuration);
                }
                catch (Exception e)
                {
                    OnInfoChanged(e.Message + Environment.NewLine + e.StackTrace);
                }
            }

            strategy.setup = setup;
            strategy.trades = new List<Trade>();
            strategy.StatusChanged += (status) => OnInfoChanged(status);
            
            return strategy;
        }

        public void CreateTasks(Setup newsetup)
        {
            var task = Task.Factory.StartNew(() =>
            {
                OnlineTrading(newsetup);
            }, tokenSource.Token);

            _bag.Add(task); 
        }

        public void PreOnlineTrading()
        {
            if (_bag != null) return;

            this._bag = new ConcurrentBag<Task>();

            if (Process.GetProcessesByName("ConnectionBox").Length > 0 && false)
                _externalComm = new API_QCB();
            else
                _externalComm = new API_CMA();

            _externalComm.TickArrived += (tick) => { if (TickArrived != null) TickArrived(tick); };
            _externalComm.FeedChanged += (state) => { if (CommChanged != null) CommChanged(state); };
            _externalComm.InfoChanged += (state) => OnInfoChanged(state);
            _externalComm.Start();
            
            //API_Azure.Connect();
        }

        public void OnlineTrading(Setup setup)
        {
            var strategy = Initialize(setup);

            strategy.TradableAssets(true);
            
            //_externalComm.SendTrade(setup, strategy.trade, 1);
            // OnTradeArrived(strategy.trade, 1);
            
            // Update Strategy State
            var starttime = strategy.GetStartTime();
            var ticks = strategy.GetData(starttime);
            
            strategy.WarmUp(ticks.ToList());
                        
            #region Run "mini-backtest"
            var lasttime = DateTime.Today; 
            
            if (strategy.trade != null)
            {
                lasttime = strategy.trade.ExitTime.HasValue 
                    ? strategy.trade.ExitTime.Value 
                    : strategy.trade.EntryTime;
                ticks.ToList().RemoveAll(t => t.Time <= lasttime);
            }

            foreach (var tick in ticks) strategy.Run(tick);

            ticks = null;
            #endregion

            strategy.currentTime = DateTime.Today.AddHours(strategy.hour);
            strategy.StartOrReset();
            strategy.IsOnline = true;
            
            strategy.TradeArrived += (Trade trade, int act) =>
            {
                _externalComm.SendTrade(setup, trade, act);
                
                OnTradeArrived(trade, act);
                //API_Azure.Send(trade);
            };

            foreach (var symbol in strategy.Symbols) _externalComm.Subscribe(symbol);

            VerifyExistance(strategy.trade);
            VerifyExistance(strategy.tradlng);
            VerifyExistance(strategy.tradsht);

            _externalComm.TickArrived += (tick) =>
            {
                lock(_object) strategy.Run(tick);

                if (tokenSource.IsCancellationRequested)
                {
                    //strategy.TryClose(-1);
                }
            };

            // Save strategies for dispose
            Strategies.TryAdd(setup.SetupId, strategy);
        }

        public void PrintPortfolio()
        {
            var trades = new List<Trade>(TradeList.OrderBy(t => t.ExitTime));
            var tradesGroup = trades.GroupBy(t => t.SetupId + "_" + t.Symbol.Substring(0, 4))
                .ToDictionary(x => x.Key, y => y.ToList());

            if (tradesGroup.Count > 1)
            {
                var totalcapital = tradesGroup.Sum(kvp => kvp.Value.Average(x => x.Capital));

                var strategy = new Strategy();

                tradesGroup.ToList().ForEach(t =>
                {
                    strategy.trades = t.Value;

                    if (strategy.trades.Count > 0)
                    {
                        var share = t.Value.Average(x => x.Capital) / totalcapital;

                        strategy.trade = strategy.trades.Last();
                        strategy.setup = new Setup(strategy.trade.SetupId, t.Key);
                        strategy.Statistics(true, share);
                    }
                });

                strategy.trades = new List<Trade>(trades);

                if (strategy.trades.Count > 0)
                {
                    strategy.trade = strategy.trades.Last();
                    strategy.setup = new Setup(0, "Portfolio");
                    strategy.Statistics(true);
                }
            }

            TradeList.Clear();
        }

        private void VerifyExistance(Trade trade)
        {
            if (trade == null || trade.Symbol == null) return;
            
            _externalComm.Subscribe(trade.Symbol);
            if (trade.IsTrading || trade.EntryTime > DateTime.Today) OnTradeArrived(trade, 0);
            //API_Azure.Send(trade);
        }

        public async Task OfflineTrading(Setup setup)
        {
            setup.OfflineStartTime = setup.OfflineStartTime.Date;
            
            using (var strategy = Initialize(setup))
            {
                strategy.type = 0;
                strategy.IsOnline = false;
                strategy.candles = new List<Candle>();
                //OnTradeArrived(strategy.trade, 0);
                
                try
                {
                    var ticks = GetData(setup);
                    if (ticks.Count() == 0)
                    { 
                        OnInfoChanged("Não há ficheiros de dados. Contate o desenvolvedor.");
                        return;
                    }

                    //strategy.trades = ticks.GroupBy(t => t.Time.Date).ToList().Select(t =>
                    //{
                    //    var trade = new Trade(1, t.First(), setup);
                    //    trade.ExitTime = t.Last().Time;
                    //    trade.ExitValue = t.Last().Value;
                    //    return trade;
                    //}).ToList();

                    //var monthlyperformance = strategy.Statistics(true);

                    strategy.TradableAssets();
                    strategy.WarmUp(ticks.ToList());

                    var time = DateTime.Now;
                    foreach (var tick in ticks) strategy.Run(tick);

                    strategy.trades.ForEach(t => TradeList.Add(t));

                    var result = strategy.Statistics(true);   // Save results

                    if (result == null) { OnInfoChanged("null"); return; }
                    result.Description = (DateTime.Now - time).TotalSeconds.ToString("#.000s.\r\n");
                    OnInfoChanged(result.ToShortString().Replace(";", " "));
                }
                catch (Exception e) { OnInfoChanged(e.StackTrace); }
            }
        }

        public IEnumerable<Tick> GetData(Setup setup)
        {
            var ticks = new List<Tick>();
            
            if (!DicTicks.TryGetValue(setup.SetupId, out ticks))
            {
                var s = DicStrategies[setup.SetupId];
                s.setup = setup;
                ticks = s.GetData(setup.OfflineStartTime).ToList();     
                if (ticks.Count > 0) DicTicks.TryAdd(setup.SetupId, ticks);
            }

            return ticks;
        }       
        
        public void EditTrade(Trade trade) 
        {
            tokenSource.Cancel();
            //foreach (var t in OnlineTradingTasks)
            //    RouteringChanged(t.Value.Id + " " + t.Value.Status);
        }

        public void PrintSavedTrades()
        {
            var count = 0;
            var files = Directory.GetFiles(Environment.CurrentDirectory, "ID_" + "*.dat").OrderByDescending(x => x);
            
            foreach (var file in files)
            {
                using (var input = File.OpenRead(file))
                {
                    var trades = new List<Trade>();
                    var strategy = (Strategy)(new BinaryFormatter()).Deserialize(input);
                    
                    if (strategy.trade != null) trades.Add(strategy.trade);
                    if (strategy.tradlng != null) trades.Add(strategy.tradlng);
                    if (strategy.tradsht != null) trades.Add(strategy.tradsht);
                    if (trades.Count == 0) continue;
                    var setupid = strategy.setup.SetupId.ToString("ID 000 > ");
                    
                    foreach (var trade in trades)
                    {
                        if (trade.IsDayTrade && DateTime.Today > trade.EntryTime) continue;
                        
                        if (trade.IsTrading || !trade.ExitTime.HasValue)
                        {
                            count++;
                            OnInfoChanged(setupid + trade.ToString().Replace(";", " "));
                        }
                    }  
                }
            }
            if (count == 1) OnInfoChanged("Trade em aberto:");
            if (count > 1) OnInfoChanged("Trades em aberto:");
        }

        #endregion

        #region Optimisation Functions
        public int NumberOfParameters()
        {
            return DicStrategies[OptimizationSetup.SetupId].GetNumberOfParameters();
        }
        public double[] GetGenes()
        {
            return DicStrategies[OptimizationSetup.SetupId].Setup2Gene(OptimizationSetup);
        }
        public Setup SetGenes(double[] genes)
        {
            return DicStrategies[OptimizationSetup.SetupId].Gene2Setup(OptimizationSetup, genes);
        }
        public double Fitness(double[] genes)
        {
            try
            {
                var id = OptimizationSetup.SetupId;

                var strategy = DicStrategies[id];
                var ticks = new List<Tick>();
                DicTicks.TryGetValue(id, out ticks);

                var monthly = ticks.GroupBy(t => t.Time.ToString("mm-yy")).ToList().Select(t => 
                { 
                    var trade = new Trade(1,t.First(),new Setup(id,"B&H"));
                    return trade;
                });

                strategy.type = 0;
                strategy.trades = new List<Trade>();
                strategy.setup = strategy.Gene2Setup(OptimizationSetup, genes);
                strategy.StatusChanged += (status) => OnInfoChanged(status);
                strategy.WarmUp(ticks);

                foreach (var tick in ticks) strategy.Run(tick);
                var result = strategy.Statistics(false);

                ticks = null;
                strategy = null;
                if (result != null && result.TotalNetProfit > result.MaxDrawndown) OnInfoChanged(result.ToShortString());

                return result == null ? -1e10 : id == 1 || id == 14
                    ? (double)result.TotalNetProfit.Value
                    : (double)result.SharpeRatio.Value;
            }
            catch (Exception e)
            {
                OnInfoChanged(e.Message + Environment.NewLine + e.StackTrace);
                return double.MinValue;
            }
        }
        #endregion
    }
}