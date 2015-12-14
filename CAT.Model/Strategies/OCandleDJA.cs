using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CAT.Model
{
    [Serializable]
    public class OCandleDJI : Strategy
    {
        # region Fields

        private DateTime _open;
        private DateTime _clse;
        private Decimal _target;
        private Decimal _bvalue;
        private Object _object;
        private Dictionary<int, Decimal> _zero;
        private ConcurrentDictionary<string, decimal> _max;
        private ConcurrentDictionary<string, decimal> _min;
        private ConcurrentDictionary<string, decimal> _val;
        
        #endregion

        #region Constructor / Destructor
        
        public OCandleDJI() : base()
        { 
            this.hour = 10;
            _object = new Object();
            _zero = new Dictionary<int, Decimal>();
            _max = new ConcurrentDictionary<string, decimal>();
            _min = new ConcurrentDictionary<string, decimal>();
            _val = new ConcurrentDictionary<string, decimal>();
        }
        ~OCandleDJI()
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

        public override Setup Gene2Setup(Setup setup, double[] genes)
        {
            var mySetup = new Setup(setup.SetupId, setup.Name);
            mySetup.Symbol = setup.Symbol;
            mySetup.Allow = setup.Allow;
            mySetup.Capital = setup.Capital;
            mySetup.DayTradeDuration = setup.DayTradeDuration;
            mySetup.OfflineStartTime = setup.OfflineStartTime;
            mySetup.Parameters = setup.Parameters;

            var multi = setup.Allow == "V" ? .975 : 3;
            var add = 0.5 * 0;
            multi = multi - add;

            var sl = genes.Length > 3 ? genes[3] / 3 + .05 : .2;

            mySetup.TimeFrame = Math.Ceiling(genes[0] * 100);
            mySetup.DynamicLoss = Math.Round(genes[1] + .05, 4);
            mySetup.StaticLoss = Math.Round(sl, 4);
            mySetup.StaticGain = genes[2] < multi ? (double?)Math.Round(genes[2] * multi + add, 4) : null;

            return mySetup;
        }

        public override double[] Setup2Gene(Setup setup)
        {
            var length = GetNumberOfParameters();
            var myGenes = new double[length];

            var multi = setup.Allow == "V" ? .975 : 3;
            var add = 0.5 * 0;
            multi = multi - add;

            myGenes[0] = setup.TimeFrame / 100;
            myGenes[1] = setup.DynamicLoss.HasValue ? setup.DynamicLoss.Value - .05 : 1;
            myGenes[2] = setup.StaticGain.HasValue ? setup.StaticGain.Value / multi - add : multi;
            if (length > 3) myGenes[3] = setup.StaticLoss.HasValue ? setup.StaticLoss.Value * 3 - .05 : 1; 
            return myGenes;
        }

        public override void Run(Tick tick)
        {
            if (Filter(tick)) return;
            
            currentTime = tick.Time;

            _val.AddOrUpdate(tick.Symbol, tick.Value, (k, v) => v = tick.Value);
            
            if (type != 0)
            {
                trade.ExitTime = currentTime;
                trade.ExitValue = _val[trade.Symbol];

                var eod = currentTime >= eodTime;
                if (!eod && trade.Symbol != tick.Symbol) return;
                
                TrailingStop();

                var gain = type * trade.ExitValue >= trade.StopGain * type;
                var loss = type * trade.ExitValue <= trade.StopLoss * type;
                if (loss || gain || eod) type = 0;
                action = type == 0 ? -trade.Type : 0;

                Send(trade, action);

                if (string.IsNullOrEmpty(trade.Obs) && trade.Result < 0 && trade.EntryTime.Date != DateTime.Today)
                    trade.Obs = (trade.ExitTime.Value - trade.EntryTime).TotalMinutes.ToString();

                if (action == 0) return;

                if (gain) eodTime = trade.ExitTime.Value;
                
                trade.ExitValue = 
                    gain ? trade.StopGain :                           // Try StopLoss Exit
                    loss ? trade.StopLoss - slippage * trade.Type :   // Try StopGain Exit
                    trade.ExitValue;                                  // Is EOD

                trades.Add(trade);
            }

            //
            StartOrReset();

            // Return while ref candle has not oppened
            if (type != 0 || currentTime < _open || currentTime >= eodTime || allow == string.Empty) return;

            // Get ref candle extremes
            if (currentTime < _clse)
            {
                _max.AddOrUpdate(tick.Symbol, tick.Value, (k, v) => v = Math.Max(_max[tick.Symbol], tick.Value));
                _min.AddOrUpdate(tick.Symbol, tick.Value, (k, v) => v = Math.Min(_min[tick.Symbol], tick.Value));
                action = 0; return;
            }

            // Return if no candle was added
            if (_max.IsEmpty) return;

            if (string.IsNullOrEmpty(tradlng.Symbol) && allow != "V")
            { 
                var key = SearchNearThreshold(ref _max);

                if (string.IsNullOrEmpty(key)) allow = allow == "A" ? "V" : string.Empty;
                else
                {
                    tradlng.Symbol = key;
                    tradlng.EntryValue = _max[key] + .01m;
                    tradlng.ExitValue = _min[key] - .01m;
                    var fixComm = 100 * tradlng.BkrFixComm / tradlng.Qnty;

                    if (setup.StaticLoss.HasValue)
                        while (tradlng.NetResult - fixComm <= -100 * (decimal?)setup.StaticLoss) tradlng.ExitValue = tradlng.ExitValue + .01m;
                    tradlng.StopLoss = tradlng.ExitValue;

                    while (tradlng.NetResult - fixComm < 0) tradlng.ExitValue = tradlng.ExitValue + .01m;
                    _zero.Add(tradlng.Type, tradlng.ExitValue.Value);

                    if (setup.StaticGain.HasValue)
                    {
                        while (tradlng.NetResult - fixComm < 100 * (decimal?)setup.StaticGain)
                            tradlng.ExitValue = tradlng.ExitValue + .01m;
                        tradlng.StopGain = tradlng.ExitValue;
                    }

                    tradlng.ExitValue = null;
                    Send(tradlng, 0);
                }
            }

            if (string.IsNullOrEmpty(tradsht.Symbol) && allow != "C")
            {
                var key = SearchNearThreshold(ref _min);

                if (string.IsNullOrEmpty(key)) allow = allow == "A" ? "C" : string.Empty;
                else
                {
                    tradsht.Symbol = key;
                    tradsht.EntryValue = _min[key] - .01m;
                    tradsht.ExitValue = _max[key] + .01m;
                    tradsht.StopGain = .05m;
                    var fixComm = 100 * tradsht.BkrFixComm / tradsht.Qnty;

                    if (setup.StaticLoss.HasValue)
                        while (tradsht.NetResult - fixComm <= -100 * (decimal?)setup.StaticLoss) tradsht.ExitValue = tradsht.ExitValue - .01m;
                    tradsht.StopLoss = tradsht.ExitValue;

                    while (tradsht.NetResult - fixComm < 0) tradsht.ExitValue = tradsht.ExitValue - .01m;
                    _zero.Add(tradsht.Type, tradsht.ExitValue.Value);

                    if (setup.StaticGain.HasValue)
                    {
                        while (tradsht.NetResult - fixComm < 100 * (decimal?)setup.StaticGain && tradsht.ExitValue.Value >= .05m)
                            tradsht.ExitValue = tradsht.ExitValue - .01m;
                        tradsht.StopGain = Math.Round(tradsht.ExitValue.Value, 2);
                    }

                    tradsht.ExitValue = null;
                    Send(tradsht, 0);
                }
            }

            if ((allow == "A" || allow == "C") && !string.IsNullOrEmpty(tradlng.Symbol) && _val[tradlng.Symbol] >= tradlng.EntryValue) type = 1;
            if ((allow == "A" || allow == "V") && !string.IsNullOrEmpty(tradsht.Symbol) && _val[tradsht.Symbol] <= tradsht.EntryValue) type = -1;

            if (type == 0) return;
            trade = type > 0 ? tradlng : tradsht;
            trade.Obs = _target.ToString("R$ 0.00");
            trade.ExitValue = trade.EntryValue;
            Send(trade, trade.Type);
            
            trade.EntryValue = trade.EntryValue + slippage * type;
            trade.ExitTime = currentTime;
            trade.EntryTime = currentTime;
            allow = allow == "A" ? type > 0 ? "V" : "C" : string.Empty;
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
            
            type = 0;
            _val.Clear();
            _max.Clear();
            _min.Clear();
            _zero.Clear();
            _target = Decimal.Parse(setup.Parameters.Split(' ')[0]);;
            _bvalue = _target < 1 ? .16m : .51m;
            
            this.slippage = 0m;
            this.tradlng = new Trade(Guid.NewGuid(), setup.SetupId, 1, _clse, setup.Capital.Value);
            this.tradsht = new Trade(Guid.NewGuid(), setup.SetupId, -1, _clse, setup.Capital.Value);
            allow = setup.Allow;   // Allow both long and sell again.

            if (eodTime.Date == DateTime.Today)
                OnStatusChanged("Id: " + setup.SetupId + " Bell: " + eodTime.ToShortTimeString() +
                    " Abe: " + _open.ToShortTimeString() +
                    " Fech: " + _clse.ToShortTimeString());
        }

        public override bool Filter(Tick tick)
        {
            var i = tick.Symbol.Length == 6 ? 1 : 2;
            if (IsOnline && !Symbols.Contains(setup.Symbol + "#" + tick.Symbol.Substring(5, i))) return true;
            
            //if (!tick.Symbol.Contains(setup.Symbol) || (int)tick.Symbol[4] < 64) return true;

            return _val.ContainsKey(tick.Symbol) && _val[tick.Symbol] == tick.Value;
        }
        
        #endregion

        #region Private functions
        private string SearchNearThreshold(ref ConcurrentDictionary<string, decimal> dict)
        {
            if (dict.IsEmpty) return string.Empty;

            var ordered = dict.Where(d => d.Value >= _bvalue && d.Value < _target + 1).ToList();
                
            if (ordered.Count <= 0) return string.Empty;

            var mathabs = ordered.Select(o => Math.Abs(o.Value - _target)).ToList();
            var index = mathabs.IndexOf(mathabs.Min());

            return ordered[index].Key;
        }

        private void TrailingStop()
        {
            if (!setup.DynamicLoss.HasValue) return;

            var threshold = trade.Type > 0
                ? Math.Min(_target, trade.EntryValue)
                : Math.Max(_target, trade.EntryValue);

            threshold = trade.EntryValue;

            var tsl = trade.Type * (trade.ExitValue - threshold * (decimal?)setup.DynamicLoss * trade.Type);
            if (tsl > trade.Type * trade.StopLoss)
                trade.StopLoss = trade.Type > 0
                    ? trade.Type * Math.Ceiling(tsl.Value * 100) / 100
                    : trade.Type * Math.Floor(tsl.Value * 100) / 100;
        }
        #endregion
    }
}