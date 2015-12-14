using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace CAT.Model
{
    [Serializable] 
    public class Trade : ObservableObject
    {
        #region Fields
        private decimal _bspvarcomm = 0;
        private decimal _bkrvarcomm = 0;

        private decimal? _stoploss;
        private decimal? _stopgain;
        private DateTime? _exittime;
        private decimal? _exitvalue;
        private string _obs;
        #endregion

        #region Constructor
        public Trade()
        {

        }

        public Trade(Guid id, int setupid, int type, DateTime entrytime, decimal capital)
        {
            this.Id = id;
            this.SetupId = setupid;
            this.Type = type;
            this.EntryTime = entrytime;
            this.Capital = capital;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="setupid">Identifies the strategy</param>
        /// <param name="type">Buy (+1) or Sell (-1)</param>
        /// <param name="tick">Condition to start trade</param>
        /// <param name="discount">Discount on brokage commission</param>
        public Trade(int type, Tick tick, Setup setup)
        {
            this.Id = Guid.NewGuid();
            this.SetupId = setup.SetupId;
            this.Type = type;
            this.Symbol = tick.Symbol;
            this.EntryTime = tick.Time;
            this.EntryValue = tick.Value;
            this.Slippage = setup.Slippage;
            this.IStatus = Status.Idle;

            this.AssetClass = tick.GetClass();
            this.MinVar = tick.GetMinVar();
            this.Unit = tick.GetUnit();
            var commissions = tick.GetCommissions();
            var rebate = Math.Min(1, Math.Max(0, 1 - setup.Discount / 100));
            
            this.BkrFixComm = commissions[0] * rebate;
            _bkrvarcomm = commissions[1] * rebate;
            _bspvarcomm = commissions[2];

            this.State = "WhiteSmoke";
            this.IsDayTrade = setup.DayTradeDuration > 0;
            this.CloseTime = tick.Time.Date.AddDays(1).AddSeconds(-1);

            this.SetQnty(setup.Capital);
            this.SetLoss(setup.StaticLoss);
            this.SetGain(setup.StaticGain);
            this.GetNetResult(null);   // Clean up "initial" NetResult
        }
        #endregion

        #region Fixed Data Properties;
        public Guid Id { get; set; }
        public int SetupId { get; set; }
        public int Type { get; set; }
        public string Symbol { get; set; }
        public string AssetClass { get; set; }
        public decimal MinVar { get; set; }
        public decimal Unit { get; set; }
        public decimal BkrFixComm { get; set; }
        public DateTime EntryTime { get; set; }
        public decimal EntryValue { get; set; }
        private decimal Slippage { get; set; }
        public bool IsDayTrade { get; set; }
        public DateTime CloseTime { get; set; }
        public bool IsTrading { get; set; }
        public Status IStatus { get; set; }
        public decimal? Result { get; set; }
        public decimal? Cost { get; set; }
        public decimal? NetResult { get; set; }
        public string CurrentMaxLoss { get; set; }
        public string CurrentMaxGain { get; set; }
        public string State { get; set; }
        public int Qnty { get; set; }
        public decimal Capital { get; set; }
        public decimal? CumResult { get; set; }
        #endregion

        #region Self-Notifiable properties
        public string Obs
        {
            get { return _obs; }
            set
            {
                if (_obs == value) return;

                _obs = value;
                this.RaisePropertyChanged(() => this.Obs);
            }
        }
        public decimal? StopLoss
        {
            get { return _stoploss; }
            set 
            {
                if (_stoploss == value) return;

                if (value.HasValue)
                {
                    _stoploss = this.Type > 0
                        ? this.MinVar * Math.Ceiling(value.Value / this.MinVar)
                        : this.MinVar * Math.Floor(value.Value / this.MinVar);

                    if (this.MinVar == .01m) _stoploss = Math.Max(.05m, _stoploss.Value);
                }
                else
                {
                    _stoploss = value;
                }
                
                this.RaisePropertyChanged(() => this.StopLoss);

                this.CurrentMaxLoss = GetCurrentMax(_stoploss);
                if (this.ExitTime > DateTime.Today) this.RaisePropertyChanged(() => this.CurrentMaxLoss);                
            }
        }
        public decimal? StopGain
        {
            get { return _stopgain; }
            set
            {
                if (_stopgain == value) return;

                if (value.HasValue)
                {
                    _stopgain = this.Type < 0
                        ? this.MinVar * Math.Ceiling(value.Value / this.MinVar)
                        : this.MinVar * Math.Floor(value.Value / this.MinVar);

                    if (this.MinVar == .01m) _stopgain = Math.Max(.05m, _stopgain.Value);
                }
                else
                {
                    _stopgain = value;
                }
                
                this.RaisePropertyChanged(() => this.StopGain);

                this.CurrentMaxGain = GetCurrentMax(_stopgain);
                if (this.ExitTime > DateTime.Today) this.RaisePropertyChanged(() => this.CurrentMaxGain);
            }
        }
        public decimal? ExitValue
        {
            get { return _exitvalue; }
            set
            {
                if (_exitvalue == value) return;

                _exitvalue = value;
                this.RaisePropertyChanged(() => this.ExitValue);

                GetNetResult(value);
            }
        }
        public DateTime? ExitTime
        {
            get { return _exittime; }
            set 
            {
                if (_exittime == value) return;

                _exittime = value;
                this.RaisePropertyChanged(() => this.ExitTime);

                this.State = "LightGreen";

                if (value < DateTime.Today) return;
                if ((value.Value - this.EntryTime).TotalSeconds < 10) this.State = "Yellow";
                this.RaisePropertyChanged(() => this.State);   
            }
        }        
        #endregion

        #region Public Functions

        /// <summary>
        /// Updates the trade and decides whether it exists.
        /// </summary>
        /// <param name="tick">New information as tick</param>
        public bool Update(Tick tick)
        {
            if (this.IsTrading)
            {
                var ismatch = this.Symbol == tick.Symbol;
                
                var exit = new List<bool>
                {
                    tick.Time >= this.CloseTime,
                    ismatch && this.Type * tick.Value >= this.StopGain * this.Type,
                    ismatch && this.Type * tick.Value <= this.StopLoss * this.Type
                
                }.IndexOf(true);
               
                if (exit == 0 && !this.IsDayTrade) return this.IsTrading;

                this.ExitTime = tick.Time;
                this.IsTrading = exit < 0;

                if (!this.IsTrading) SetIStatus(exit);

                if (!ismatch) return this.IsTrading;
                
                var slippage = this.ExitTime > DateTime.Today ? 0 : this.Slippage;

                this.ExitValue = new Dictionary<int, decimal?>
                {
                    { -1, tick.Value },
                    { 0, tick.Value },
                    { 1, this.StopGain },
                    { 2, this.StopLoss - slippage * this.Type }
                }[exit];
            }
            return this.IsTrading;
        }

        /// <summary>
        /// Change stop loss
        /// </summary>
        /// <param name="initial">Maximum absolute loss</param>
        public void ChangeLoss(decimal? initial)
        {
            this.StopLoss = initial;
            GetNetResult(null);
        }
        /// <summary>
        /// Change stop loss
        /// </summary>
        /// <param name="initial">Maximum absolute loss</param>
        public void ChangeLoss(double? value, decimal? initial)
        {
            this.SetLoss(value);

            if (this.StopLoss.HasValue && initial.HasValue)
            {
                this.StopLoss = this.Type > 0
                    ? Math.Max(this.StopLoss.Value, initial.Value)
                    : Math.Min(this.StopLoss.Value, initial.Value);
            }
            else
            {
                if (initial.HasValue) this.StopLoss = initial;
            }

            GetNetResult(null);
        }
        /// <summary>
        /// Change stop gain
        /// </summary>
        /// <param name="initial">Maximum absolute loss</param>
        public void ChangeGain(decimal range)
        {
            this.StopGain = this.EntryValue + range * this.Type;
            GetNetResult(null);
        }
        /// <summary>
        /// Change stop gain
        /// </summary>
        /// <param name="initial">Maximum absolute loss</param>
        public void ChangeGain(double? value, decimal? initial)
        {
            this.SetGain(value);

            if (this.StopGain.HasValue && initial.HasValue)
            {
                this.StopGain = this.Type < 0
                    ? Math.Max(this.StopGain.Value, initial.Value)
                    : Math.Min(this.StopGain.Value, initial.Value);
            }
            else
            {
                if (initial.HasValue) this.StopGain = initial;
            }

            GetNetResult(null);
        }
        /// <summary>
        /// ToString()
        /// </summary>
        public override string ToString()
        {
            var output = EntryTime + ";" + Symbol + ";";
            output += Type == 0 ? "N" : Type > 0 ? "C" : "V";
            output += ";" + Qnty + ";" + EntryValue;
            output += StopLoss.HasValue ? ";" + StopLoss.Value : ";";
            output += StopGain.HasValue ? ";" + StopGain.Value : ";";
            output += !ExitTime.HasValue ? ";" : ";" + ExitTime.Value;
            output += ExitValue.HasValue ? ";" + ExitValue.Value : ";";
            output += Result.HasValue ? ";" + Math.Round(Result.Value, 2) : ";";
            output += Cost.HasValue ? ";" + Math.Round(Cost.Value, 2) : ";";
            output += NetResult.HasValue ? ";" + Math.Round(NetResult.Value, 2) : ";";
            output += CumResult.HasValue ? ";" + Math.Round(CumResult.Value, 2) : ";";
            output += ";" + Obs;
            if (this.AssetClass == "BMF.FUT" && Result.HasValue && Cost.HasValue)
                output += ";" + (Result.Value * this.Unit - Cost.Value).ToString();
            return output;
        }
        #endregion

        #region Private Functions
        /// <summary>
        /// Add slippage to simulation
        /// </summary>
        /// <param name="tick">New information as tick</param>
        public void AddEntrySlippage(Tick tick)
        {
            this.EntryTime = tick.Time;
            this.RaisePropertyChanged(() => this.EntryTime);

            // Only add slippage here for backtests
            if (tick.Time > DateTime.Today) return;
            this.EntryValue = this.EntryValue + this.Slippage * this.Type;
            this.RaisePropertyChanged(() => this.EntryValue);
        }

        /// <summary>
        /// Set quantity to trade
        /// </summary>
        /// <param name="capital">Maximum capital to allocate</param>
        private void SetQnty(decimal? capital)
        {
            if (capital.HasValue)
            {
                this.Qnty = this.AssetClass == "BMF.FUT"
                    ? Math.Max(1, Convert.ToInt32(Math.Floor(capital.Value)))
                    : Math.Max(1, Convert.ToInt32(Math.Floor(capital.Value / this.EntryValue / 100))) * 100;
            }
            else
            {
                this.Qnty = this.AssetClass == "BMF.FUT" ? 1 : 100;
            }

            this.Capital = this.AssetClass == "BMF.FUT" ? this.Qnty : capital.Value;// Math.Round(this.Qnty * this.EntryValue, 2);
        }
        /// <summary>
        /// Set stop loss
        /// </summary>
        /// <param name="value">Maximum relative loss</param>
        /// <param name="initial">Maximum absolute loss</param>
        private void SetLoss(double? value)
        {
            if (value.HasValue)
            {
                if (!value.HasValue) return;
                var exitvalue = this.EntryValue * (1 - (decimal)value * this.Type);

                this.StopLoss = Math.Abs(this.EntryValue - exitvalue) > .02m
                    ? exitvalue : this.EntryValue - .02m * this.Type;
            }
        }        
        /// <summary>
        /// Set stop gain
        /// </summary>
        /// <param name="value">Minimum relative gain</param>
        private void SetGain(double? value)
        {
            if (!value.HasValue) return;
            this.StopGain = this.EntryValue * (1 + (decimal)value * this.Type);
        }
        
        /// <summary>
        /// Calculate current result
        /// </summary>
        /// <param name="exitvalue">Last value to evaluate</param>
        public void GetNetResult(decimal? exitvalue)
        {
            if (exitvalue.HasValue)
            {
                this.Result = this.Type * (exitvalue - this.EntryValue);

                this.Cost = this.AssetClass == "BMF.FUT" ? this.BkrFixComm * this.Qnty
                    : _bkrvarcomm * (exitvalue + this.EntryValue) * this.Qnty
                    + _bspvarcomm * (exitvalue + this.EntryValue) * this.Qnty;

                var xCost = this.Cost / this.Unit / this.Qnty;
                this.NetResult = (this.Result - xCost) / this.EntryValue * 100;
            }
            else
            {
                this.Cost = null;
                this.Result = null;
                this.NetResult = null;
            }

            this.RaisePropertyChanged(() => this.Result);
            this.RaisePropertyChanged(() => this.NetResult);
        }
        /// <summary>
        /// Calculate result for a given value
        /// </summary>
        /// <param name="exitvalue">Given value to evaluate</param>
        private string GetCurrentMax(decimal? value)
        {
            var netstr = string.Empty;       

            if (value.HasValue)
            {
                var result = this.Type * (value - this.EntryValue);

                var cost = this.AssetClass == "BMF.FUT" ? this.BkrFixComm / Unit :
                    _bkrvarcomm * this.Qnty * (value + this.EntryValue) +
                    _bspvarcomm * this.Qnty * (value + this.EntryValue);

                var net = this.AssetClass == "BMF.FUT"
                    ? (result - cost) / this.EntryValue
                    : (result - cost / this.Qnty) / this.EntryValue;

                if (net.HasValue) netstr = net.Value.ToString("P2");
            }

            return netstr;
        }
        /// <summary>
        /// Change colors in GUI when trade stops
        /// </summary>
        /// <param name="exit">Determine the color relative to how it stops</param>
        private void SetIStatus(int exit)
        {
            var colors = new string[] { "", "LightBlue", "LightSalmon" };
            colors[0] = this.NetResult > 0 ? colors[1] : colors[2];
            
            this.State = colors[exit];
            this.IStatus = (Status)exit;

            //
            if (this.ExitTime < DateTime.Today) return;
            
            this.State = "Yellow";
            this.RaisePropertyChanged(() => this.State);

            Task.Delay(10000).ContinueWith(_ =>
            {
                this.State = colors[exit];
                this.RaisePropertyChanged(() => this.State);
            });
            
        }
        #endregion       

        public enum Status
        {
            Idle = -1,
            StopEOD = 0,
            StopGain = 1,
            StopLoss = 2,
        };
    }
}