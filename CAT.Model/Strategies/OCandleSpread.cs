using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CAT.Model
{
    [Serializable]
    class OCandleSpread : Strategy
    {
        # region Fields

        private DateTime _open;
        private DateTime _clse;
        private Decimal _target;
        private Decimal _uvalue;
        private Decimal _bvalue;
        private Object _object;
        private Dictionary<int, Double> _zero;
        private ConcurrentDictionary<string, decimal> _max;
        private ConcurrentDictionary<string, decimal> _min;
        private ConcurrentDictionary<string, decimal> _val;
        
        #endregion

        #region Constructor / Destructor
        
        public OCandleSpread() : base()
        { 
            this.hour = 10;
            _object = new Object();
            _zero = new Dictionary<int, double>();
            _max = new ConcurrentDictionary<string, decimal>();
            _min = new ConcurrentDictionary<string, decimal>();
            _val = new ConcurrentDictionary<string, decimal>();
            tradeDic = new Dictionary<string, Trade>();
        }
        ~OCandleSpread()
        {
            if (setup == null || trade == null) return;
        }

        #endregion

        #region Public override functions

        public override void WarmUp(List<Tick> ticks)
        {
           
        }

        public override IEnumerable<Tick> Datafilter(IEnumerable<Tick> ticks)
        {
            var exp = new List<DateTime>();
            for (var dt = this.setup.OfflineStartTime; dt <= DateTime.Today; dt = dt.AddDays(1))
                if (dt.DayOfWeek == DayOfWeek.Monday && dt.Day > 14 && dt.Day < 22) exp.Add(dt);

            return ticks.Where(t =>
                {
                    // Keep quotes for today's trades
                    if (t.Time > DateTime.Today) return true;

                    exp.RemoveAll(d => d <= t.Time);
                    if (exp.Count == 0) return false;

                    var tmp = exp.First();
                    if (tmp.Month != (int)t.Symbol[4] - 64) return false;

                    return setup.Name.Length == 7 ? t.Time > tmp.AddDays(1 - tmp.Day) : t.Time < tmp.AddDays(1 - tmp.Day);
                });
        }

        public override void Run(Tick tick)
        {
            if (Filter(tick)) return;

            currentTime = tick.Time;

            _val.AddOrUpdate(tick.Symbol, tick.Value, (k, v) => v = tick.Value);

            if (type != 0)
            {
                TrailingStop(tick, trade);
                
                Send(trade, trade.Update(tick) ? 0 : -trade.Type);
                
                if (trade.IsTrading) return;

                type = 0;
                trades.Add(trade);
            }

            //
            StartOrReset();

            // Return while ref candle has not oppened
            if (type != 0 || currentTime < _open || currentTime >= eodTime || allow == string.Empty) return;

            string symbol;

            // Get ref candle extremes
            if (SearchNearThreshold(out symbol))
            {
                _max.AddOrUpdate(tick.Symbol, tick.Value, (k, v) => v = Math.Max(_max[tick.Symbol], tick.Value));
                _min.AddOrUpdate(tick.Symbol, tick.Value, (k, v) => v = Math.Min(_min[tick.Symbol], tick.Value));
                return;
            }

            if (tradeDic.Count == 0)
            {
                var minvar = tick.GetMinVar();
                tradeDic.Add("C", new Trade(+1, new Tick(symbol, currentTime, _max[symbol] + minvar), setup));
                tradeDic.Add("V", new Trade(-1, new Tick(symbol, currentTime, _min[symbol] - minvar), setup));
            }

            RemoveUnnecessaryInfo(symbol);
            if (symbol != tick.Symbol) return;
            if (tradeDic.ContainsKey("C")) tradeDic["C"].IsTrading = _val[symbol] >= tradeDic["C"].EntryValue;
            if (tradeDic.ContainsKey("V")) tradeDic["V"].IsTrading = _val[symbol] <= tradeDic["V"].EntryValue;
            if (tradeDic.All(t => !t.Value.IsTrading)) return;

            trade = tradeDic.FirstOrDefault(t => t.Value.IsTrading).Value;
            trade.Obs = _target.ToString("R$ 0.00");
            trade.CloseTime = eodTime;
            trade.AddEntrySlippage(tick);
            trade.Update(tick);
            Send(trade, type = trade.Type);

            tradeDic.Remove(trade.Type > 0 ? "C" : "V");
            allow = tradeDic.Count == 0 ? string.Empty : tradeDic.First().Key;
        }

        public override void StartOrReset()
        {
            if (_open.Date == currentTime.Date) return;

            if (eodTime.Year > 1) eodTime.AddHours(-setup.DayTradeDuration);
            type = eodTime > dbvsp ? 10 : currentTime.Hour > 11 ? eodTime.Hour : currentTime.Hour;
            eodTime = FixEODTime(currentTime.Date.AddHours(type + setup.DayTradeDuration));

            _open = eodTime.Date.AddMinutes(570).Add(
                TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time").GetUtcOffset(eodTime) -
                TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time").GetUtcOffset(eodTime));

            _clse = _open.AddMinutes(setup.TimeFrame);

            if (eodTime.Date == DateTime.Today)
                OnStatusChanged("Id: " + setup.SetupId + " Bell: " + eodTime.ToShortTimeString() +
                    " Abe: " + _open.ToShortTimeString() +
                    " Fech: " + _clse.ToShortTimeString());

            tradeDic = new Dictionary<string, Trade>();
            _val.Clear();
            _max.Clear();
            _min.Clear();
            _zero.Clear();
            _target = Decimal.Parse(setup.Parameters.Split(' ')[0]); ;
            _bvalue = _target < 1 ? .16m : 0.30m;// .31m;
            _uvalue = _target < 1 ? .71m : 1.35m;// .91m;

            this.type = 0;
            this.slippage = 0m;
            this.tradlng = null;
            this.tradsht = null;
            allow = setup.Allow;   // Allow both long and sell again.
        }

        public override void TradableAssets()
        {
            var strikes = this.setup.Parameters.Split(' ');
            
            base.TradableAssets();
            this.Symbols.Clear();
          
            for (int i = 1; i < strikes.Length; i++)
                this.Symbols.Add(this.setup.Symbol + "#" + strikes[i]);
        }

        public override bool Filter(Tick tick)
        {
            if (!this.IsOnline) return false;
            if (Symbols.Count == 0) return false;

            var symbol = tick.Symbol.Substring(0, 4) + "#" + tick.Symbol.Substring(5);
            return !Symbols.Contains(symbol);
        }
        
        #endregion

        #region Private functions

        private bool SearchNearThreshold(out string pivotString)
        {
            var mid = _max.ToDictionary(x => x.Key, y => (y.Value + _min[y.Key]) / 2).ToList();
            mid.Sort((x, y) => x.Value.CompareTo(y.Value));

            var pivotIndex = mid.FindIndex(x => x.Value >= _target) - 1;
            pivotString = pivotIndex >= 0 ? mid[pivotIndex].Key : string.Empty;

            if (string.IsNullOrWhiteSpace(pivotString)) return true;

            var stopLoss = 0.01m * Math.Ceiling((_max[pivotString] + 0.01m) * (1 - (decimal)setup.StaticLoss) / 0.01m);

            return _bvalue > _min[pivotString] || stopLoss < _min[pivotString] - 0.01m;
        }

        private void RemoveUnnecessaryInfo(string symbol)
        {
            decimal value;
            if (_max.Count <= 2) return;

            var mid = _max.ToDictionary(x => x.Key, y => (y.Value + _min[y.Key]) / 2).ToList();
            mid.Sort((x, y) => x.Value.CompareTo(y.Value));

            var symbols = new string[] { symbol, mid.Find(x => x.Value >= _target).Key };
            
            foreach (var s in _max.Keys.Except(symbols))
            {
                _max.TryRemove(s, out value);
                _min.TryRemove(s, out value);
            }
        }

        private void TrailingStop(Tick tick, Trade trade)
        {
            if (!setup.DynamicLoss.HasValue) return;
            if (tick.Symbol != trade.Symbol) return;

            // 
            if (tick.Value * trade.Type <= trade.EntryValue * (1 + (decimal?)setup.DynamicLoss * trade.Type)) return;

            var tsl = trade.Type * (tick.Value - trade.EntryValue * //(decimal?)setup.DynamicLoss * trade.Type);
                                                                      (decimal?)setup.StaticLoss * trade.Type);
            
            if (tsl > trade.Type * trade.StopLoss) trade.StopLoss = tsl * trade.Type;
        }
        #endregion

        #region Public override functions OPTIMIZATION

        public override int GetNumberOfParameters()
        {
            return 3;
        }

        public override Setup Gene2Setup(Setup setup, double[] genes)
        {
            var mySetup = new Setup(setup.SetupId, setup.Name);
            mySetup.Symbol = setup.Symbol;
            mySetup.Allow = setup.Allow;
            mySetup.Discount = setup.Discount;
            mySetup.Slippage = setup.Slippage;
            mySetup.Capital = setup.Capital;
            mySetup.DayTradeDuration = setup.DayTradeDuration;
            mySetup.OfflineStartTime = setup.OfflineStartTime;
            mySetup.Parameters = setup.Parameters;

            mySetup.TimeFrame = Math.Min(60, Math.Ceiling(3 + genes[0] * 60));
            mySetup.DynamicLoss = Math.Round(.05 + genes[1], 4);
            mySetup.StaticGain = Math.Round(.1 + genes[2] * 2, 4);
            mySetup.StaticLoss = genes.Length > 3 ? Math.Round(0.05 + genes[3] / 3, 4) : .2;

            if (mySetup.StaticGain >= 1)
            {
                mySetup.StaticGain = null;
                genes[2] = 1;
            }

            if (mySetup.DynamicLoss > mySetup.StaticGain + mySetup.StaticLoss)
            {
                mySetup.DynamicLoss = mySetup.StaticGain + mySetup.StaticLoss;
                if (mySetup.DynamicLoss.HasValue) genes[1] = mySetup.DynamicLoss.Value - .05;
            }
            
            return mySetup;
        }

        public override double[] Setup2Gene(Setup setup)
        {
            var length = GetNumberOfParameters();

            var myGenes = new double[length];

            myGenes[0] = (setup.TimeFrame - 3) / 57;
            myGenes[1] = setup.DynamicLoss.HasValue ? setup.DynamicLoss.Value - .05 : 1;
            myGenes[2] = setup.StaticGain.HasValue ? setup.StaticGain.Value - .2 : 1;
            if (length > 3) myGenes[3] = setup.StaticLoss.HasValue ? setup.StaticLoss.Value * 3 - .05 : 1;
            return myGenes;
        }

        #endregion
    }
}