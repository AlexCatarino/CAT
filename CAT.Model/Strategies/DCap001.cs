using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class DCap001 : Strategy
    {
        # region Fields

        private KeyValuePair<int, DateTime> _shift;
        private DateTime _shifttime;

        [NonSerialized]
        private List<Candle> _candles;
        private List<Candle> _eodcandles;
        
        #endregion

        #region Constructor

        public DCap001()
            : base()
        {
            this.hour = 10;
            this.candles = new List<Candle>();
            this._candles = new List<Candle>();
            this._eodcandles = new List<Candle>();
        }

        #endregion

        #region Public override functions
                
        public override void Run(Tick tick)
        {
            if (Filter(tick)) return;

            currentTime = tick.Time;
            StartOrReset();

            var eod = currentTime >= eodTime;
            var last = Indicadors(MakeCandle(tick));

            if (type != 0)
            {
                TrailingStop(tick);
                TrailingGain(tick, last);

                var action = trade.Update(tick) ? 0 : -trade.Type;
                Send(trade, action);

                if (trade.IsTrading) return;

                type = 0;
                trades.Add(trade);
            }

            // Return while ref candle has not oppened
            if (type != 0 || last == null || eod) return;

            type = int.Parse(last.Indicators.Split(';')[0]);
            trade = new Trade(type, tick, setup);
            trade.CloseTime = eodTime;

            var sl = type > 0 ? last.MinValue : last.MaxValue;

            trade.ChangeLoss(sl);
            //trade.ChangeGain(last.MaxValue-last.MinValue);

            trade.IsTrading = true;

            trade.Update(tick);

            Send(trade, 1);

            // Rule: Can only exit after 2 candles
            _candles.RemoveAll(c => c.OpenTime < currentTime);
        }

        public override void StartOrReset()
        {
            if (eodTime.Date == currentTime.Date) return;
            eodTime = GetBell(currentTime, 6 + 55.0 / 60);
            if (trade != null) trade.CloseTime = eodTime;
            return;
        }

        public override void WarmUp(List<Tick> ticks)
        {
            base.WarmUp(ticks);
            #region Get EOD data to calculate VolRank

            if (ticks.Count == 0) ticks.Add(new Tick(setup.Symbol, DateTime.Now, 0));
            var year = ticks.First().Time.AddYears(-1).Year;
            if (ticks.First().Time.Month == 1) year--;

            if (_eodcandles == null) 
                _eodcandles = new List<Candle>();
            else 
                _eodcandles.Clear();

            var files = Directory.GetFiles(DataDir, "COTAHIST" + "*xml").ToList();            
            files.Clear(); // Assim não vai ler nada

            foreach (var file in files)
            {                
                if (year > int.Parse(new FileInfo(file).Name.Substring(10, 4))) continue;

                try
                {
                    using (var tr = new StreamReader(file))
                    {
                        var allcandles = (List<Candle>)(new System.Xml.Serialization.XmlSerializer(typeof(List<Candle>))).Deserialize(tr);
                        _eodcandles.AddRange(allcandles.FindAll(c => Symbols.Contains(c.Symbol)));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
            candles = candles.OrderBy(c => c.OpenTime).ToList();
            if (ticks.Count == 1) ticks.Clear();
            #endregion
            var last = Indicadors(true);
        }
        #endregion

        #region Specific functions
        private Candle Indicadors(bool closedcandle)
        {
            var index = 0;

            if (!closedcandle && _candles != null)
            {
                index = _candles.FindIndex(c =>
                    {
                        var ctime = c.OpenTime.AddMinutes(setup.TimeFrame);
                        var s = currentTime >= ctime && currentTime.Date == ctime.Date;
                        return s;
                    });
                if (index < 0) return null;

                _candles.RemoveRange(0, index);

                return _candles[0];
            }

            var optInTimePeriod = 10;
            var maxrangeallowed = 20m;
            var breakoutcandlesize = 1m;
            var par = setup.Parameters.Split(' ');
            
            if (par.Length > 0) int.TryParse(par[0], out optInTimePeriod);
            if (par.Length > 1) decimal.TryParse(par[1], out maxrangeallowed);
            if (par.Length > 2) decimal.TryParse(par[2], out breakoutcandlesize);
            if (breakoutcandlesize <= 0) return null;

            for (var i = optInTimePeriod; i < candles.Count; i++)
            {
                var cur = candles[i];
                var subset = candles.GetRange(i - optInTimePeriod, optInTimePeriod);
                if (cur.OpenTime.Date != subset.First().OpenTime.Date) continue;

                var min = subset.Min(r => r.MinValue);
                var max = subset.Max(r => r.MaxValue);
                if (max > cur.MaxValue && min < cur.MinValue) continue;

                var diff = max - min;
                if (diff >= maxrangeallowed) continue;

                var cdiff = (cur.MaxValue - cur.MinValue) / breakoutcandlesize;                
                if (cdiff < diff) continue;

                var sign = cur.MaxValue > max ? 1 : 0;
                if (cur.MinValue < min) sign -= 1;

                cur.Indicators = sign.ToString() + ";" +
                    max.ToString("0.00") + ";" + min.ToString("0.00") + ";" + diff.ToString("0.00") + ";" + cdiff.ToString("0.00");
            }

            _candles = candles.FindAll(c => c.OpenTime >= setup.OfflineStartTime &&
                !string.IsNullOrWhiteSpace(c.Indicators));// && c.Indicators != "0");

            if (!true)
            {
                var file = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\candles.csv");
                if (file.Exists) file.Delete();
                foreach (var c in _candles) File.AppendAllText(file.FullName, c.ToString() + Environment.NewLine);
            }
            
            candles.RemoveRange(0, Math.Max(0, candles.Count - optInTimePeriod));

            var lastcandle = _candles.Count < 2 || _candles[_candles.Count - 2].Indicators == _candles.Last().Indicators
                ? null : _candles.Last();

            if (candles.Count > 0 && candles.Last().OpenTime > DateTime.Today) OnStatusChanged(candles.Last().ToString());

            return lastcandle;
        }
        private void TrailingStop(Tick tick)
        {
            if (!setup.DynamicLoss.HasValue || tick.Symbol != trade.Symbol) return;

            var tsl = trade.Type*(tick.Value - (decimal?) setup.DynamicLoss*trade.Type);

            if (tsl > trade.Type*trade.StopLoss || !trade.StopLoss.HasValue) trade.StopLoss = tsl*trade.Type;
        }
        private void TrailingGain(Tick tick, Candle last)
        {
            if (last == null || last.Indicators == type.ToString()) return;
            trade.StopGain = tick.Value;
        }
        #endregion

        #region Public override functions ONLINE TRADING
        public override DateTime GetStartTime()
        {
            var optInTimePeriod = .0;
            var str = setup.Parameters.Split(' ')[0];
            if (!double.TryParse(str, out optInTimePeriod))
                return setup.OfflineStartTime;

            var time = DateTime.Today.AddDays(-optInTimePeriod);
            if (time < setup.OfflineStartTime) time = setup.OfflineStartTime;

            return candles != null && candles.Count > 0 ?
                candles.Last().OpenTime.AddMinutes(setup.TimeFrame) : time;
        }
        #endregion

        #region Public override functions OPTIMIZATION

        public override int GetNumberOfParameters()
        {
            return 5;
        }

        public override Setup Gene2Setup(Setup setup, double[] genes)
        {
            var mySetup = new Setup(setup.SetupId, setup.Name);
            mySetup.Symbol = setup.Symbol;
            mySetup.Allow = setup.Allow;
            mySetup.Discount = setup.Discount;
            mySetup.Slippage = setup.Slippage;
            mySetup.Capital = 1m;
            mySetup.DayTradeDuration = setup.DayTradeDuration;
            mySetup.OfflineStartTime = setup.OfflineStartTime;
            mySetup.StaticLoss = null;
            mySetup.DynamicLoss = null;
            mySetup.StaticGain = null;

            mySetup.TimeFrame = Math.Max(2, Math.Min(7, Math.Ceiling(genes[0] * 9)));

            var optInTimePeriod = Math.Max(2, Math.Min(30, Math.Ceiling(genes[1] * 32)));
            var maxrangeallowed = Math.Max(2, Math.Min(20, Math.Ceiling(genes[2] * 22)));
            var breakoutcandlesize = Math.Max(.05, Math.Min(2, Math.Round(genes[3] * 2.1, 4)));

            mySetup.Parameters = optInTimePeriod + " " + maxrangeallowed + " " + breakoutcandlesize;

            mySetup.DynamicLoss = Math.Max(1, Math.Min(20, Math.Ceiling(genes[4] * 22))) / 2;

            return mySetup;
        }
        public override double[] Setup2Gene(Setup setup)
        {
            var lenght = GetNumberOfParameters();
            var myGenes = new double[lenght];

            myGenes[0] = Math.Max(0, Math.Min(1, setup.TimeFrame / 9));

            var strGenes = setup.Parameters.Split(' ');
            
            if (lenght <= 1) return myGenes;
            myGenes[1] = Math.Max(0, Math.Min(1, double.Parse(strGenes[0]) / 32));
            
            if (lenght <= 2) return myGenes;
            myGenes[2] = Math.Max(0, Math.Min(1, double.Parse(strGenes[1]) / 22));
            
            if (lenght <= 3) return myGenes;
            myGenes[3] = Math.Max(0, Math.Min(1, double.Parse(strGenes[2]) / 2.1));

            if (lenght <= 4) return myGenes;
            myGenes[4] = Math.Max(0, Math.Min(1, (double)setup.DynamicLoss / 11));
            return myGenes;
        }
        #endregion
    }
}