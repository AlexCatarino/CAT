using System;
using System.Collections.Generic;
using System.Linq;

namespace CAT.Model
{
    [Serializable]
    class AIMA : Strategy
    {
        # region Fields

        #endregion

        #region Constructor / Destructor

        public AIMA()
            : base()
        {
            this.hour = 10;
        }
        #endregion

        #region Public override functions

        public override void WarmUp(List<Tick> ticks)
        {
            // Empty, because OCandle does not need it
        }

        public override void Run(Tick tick)
        {
            currentTime = tick.Time;
            if (Filter(tick)) return;

            var ima = candles.Find(x => x.OpenTime == tick.Time).Indicators.Split(' ');

            if (type != 0)
            {
                trade.ExitTime = tick.Time;
                trade.ExitValue = tick.Value;

                var eod = false; // currentTime >= eodTime;
                var loss = false;//type * tick.Value <= trade.StopLoss * type;
                var gain = type * tick.Value >= trade.StopGain * type;
                action = gain || loss || eod ? -1 : 0;
                Send(trade, action);

                if (double.Parse(ima[1]) >= .9 || double.Parse(ima[0]) == 0)
                    Console.WriteLine("Exit");

                if (action == 0) return;

                // Is EOD?
                if (eod || gain) eodTime = tick.Time;

                // Try StopLoss Exit
                if (loss) trade.ExitValue = trade.StopLoss;

                // Try StopGain Exit
                if (gain) trade.ExitValue = trade.StopGain;

                type = 0;
                action = 1;
                trades.Add(trade);
            }

            // 
            StartOrReset();

            if (type != 0 || allow == string.Empty) return;

            //type = type == 0 && double.Parse(ima[1]) >= .8 ? 1 : type;
            type = type == 0 && double.Parse(ima[0]) >= .9 ? -1 : type;

            if (type != 0)
            {
                setup.StaticLoss = .01;
                setup.StaticGain = .10;

                trade = new Trade(Guid.NewGuid(), setup.SetupId, type, tick.Time, setup.Capital.Value);
                trade.Symbol = tick.Symbol;
                trade.EntryValue = tick.Value - .01m * type;
                if (setup.StaticLoss.HasValue) trade.StopLoss = Math.Round(trade.EntryValue * (1 - (decimal)setup.StaticLoss.Value * type), 2);
                if (setup.StaticGain.HasValue) trade.StopGain = Math.Round(trade.EntryValue * (1 + (decimal)setup.StaticGain.Value * type), 2);
                trade.ExitValue = trade.EntryValue;
                trade.ExitTime = trade.EntryTime;
            }

        }

        public override void StartOrReset()
        {
            if (eodTime.Date == currentTime.Date) return;

            if (eodTime.Year > 1) eodTime.AddHours(-setup.DayTradeDuration);
            var hour = eodTime > dbvsp ? 10 : currentTime.Hour > 11 ? eodTime.Hour : currentTime.Hour;
            eodTime = FixEODTime(currentTime.Date.AddHours(hour + setup.DayTradeDuration));

            
            //type = 0;
            
            this.slippage = 0.000m;
            allow = setup.Allow;   // Allow both long and sell again.

            if (eodTime.Date == DateTime.Today)
                OnStatusChanged("Id: " + setup.SetupId + " Bell: " + eodTime.ToShortTimeString());
        }

        #endregion

        #region Specific functions

        public override IEnumerable<Tick> GetData(DateTime starttime)
        {
            candles = new List<Candle>();
            var ticks = new List<Tick>();
            var files = System.IO.Directory.GetFiles(DataDir, this.setup.Symbol + "*csv");

            foreach (var file in files)
            {
                var info = new System.IO.FileInfo(file);
                if (!info.Exists)
                {
                    OnStatusChanged(file + " não existe.");
                    continue;
                }
                var buffer = new byte[info.Length];
                if (buffer.Length < 10000) continue;

                using (var asciifile = System.IO.File.OpenRead(file))
                {
                    asciifile.Read(buffer, 0, buffer.Length);
                    var lines = System.Text.Encoding.UTF8.GetString(buffer).Split('\n').ToList();
                    lines.RemoveAll(l => l.Length == 0);
                           
                    var last = lines.Last();
                    var eod = last.Contains(":") ? 3 : 2;

                    foreach (var line in lines)
                    {
                        try
                        {
                            var columns = line.Trim().Split(';');

                            var tick = new Tick(columns[1].Trim(), DateTime.Parse(columns[0]), decimal.Parse(columns[2]));
                      
                            var candle = new Candle(tick);
                            candle.Indicators = columns[3] + " " + columns[4];
    
                            ticks.Add(tick);
                            candles.Add(candle);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        };
                    }
                }
            }

            foreach (var tick in ticks) yield return tick;
        }

        private void Indicadors(bool closedcandle)
        {
            if (!closedcandle) return;
        }

        #endregion
    }
}


