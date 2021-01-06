using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.MarketStream;
using CryptoExchange.Net.Sockets;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Blazor.ServerSide.Pages
{
    public partial class Index : ComponentBase
    {
        public KlineInterval KlineInterval { get; set; } = KlineInterval.OneHour;
        private IEnumerable<IBinanceTick> _ticks = new List<IBinanceTick>();
        private UpdateSubscription _subscription;
        private UpdateSubscription _subscriptionToBookTickerUpdates;
        private IEnumerable<IBinanceKline> _Klines = new List<IBinanceKline>();

        private IEnumerable<Tuple<string, IBinanceKline>> _KlineSymbol = new List<Tuple<string, IBinanceKline>>();
        //kcprivate string SOCKET = "wss://stream.binance.com:9443/ws/ethusdt@kline_1m";
        private int RSI_PERIOD = 14;  // must be even number
        private int RSI_OVERBOUGHT = 70;
        private int RSI_OVERSOLD = 30;
        private string TRADE_SYMBOL = "BTCUSDT";
        private double TRADE_QUANTITY = 0.05;

        bool inposition = false;
        string message = string.Empty;
        string message2 = string.Empty;

        DateTime ServerTime = default(DateTime);

        Decimal RSI = 0;
        Decimal Median = 0;
        Decimal StdDev = 0;
        Decimal Movement24Hrs = 0;

        private IEnumerable<decimal> _closePrices;
        private bool PricesInit;

        private BinanceStreamBookPrice BookPrice = new BinanceStreamBookPrice();
        public IBinanceStreamKlineData LastKline { get; private set; }

        protected override async Task OnInitializedAsync()
        {
            await InitializeData().ConfigureAwait(false);
        }

        private async Task InitializeData()
        {

            if (!PricesInit)
            {
                var callResult = await _dataProvider.Get24HPrices().ConfigureAwait(false);
                if (callResult)
                    _ticks = callResult.Data;

                PricesInit = true;
            }

            var subResult = await _dataProvider.SubscribeTickerUpdates(HandleTickUpdates).ConfigureAwait(false);
            if (subResult)
                _subscription = subResult.Data;

            // SubscribeToBookTickerUpdatesAsync  _subscriptionToBookTickerUpdates

            var subscribeToBookTickerUpdatesResult = await _dataProvider.SubscribeToBookTickerUpdatesAsync(TRADE_SYMBOL,HandleBookTickerUpdates);
            if (subscribeToBookTickerUpdatesResult)
                _subscriptionToBookTickerUpdates = subscribeToBookTickerUpdatesResult.Data;

        }

        private void HandleBookTickerUpdates(BinanceStreamBookPrice data)
        {
            BookPrice = data;
        }

        public void OnUpdated(ChangeEventArgs e)
        {
            InitializeData().GetAwaiter().GetResult();
            var selected = e.Value;
        }

        private void HandleKLineUpdates(IBinanceStreamKlineData klineData)
        {
            LastKline = klineData;
           // InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Calulate the Standard Deviation
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public decimal CalulateStdDev(IEnumerable<decimal> values)
        {
            // ref: http://warrenseen.com/blog/2006/03/13/how-to-calculate-standard-deviation/
            decimal mean = 0.0M;
            decimal sum = 0.0M;
            decimal stdDev = 0.0M;
            int n = 0;

            foreach (var val in values)
            {
                n++;
                var delta = val - mean;
                mean += delta / n;
                sum += delta * (val - mean);
            }

            if (1 < n)
                stdDev = (decimal)Math.Sqrt((double)(sum / (n - 1)));

            StdDev = stdDev;

            return stdDev;
        }


        public decimal CalculateMedian()
        {
            var orderedClosing = _Klines.Where(x => x.CloseTime < DateTime.UtcNow).TakeLast(RSI_PERIOD).OrderBy(x => x.Close);
            int closeCount = orderedClosing.Count();
            var median = orderedClosing.ElementAt(closeCount / 2).Close + orderedClosing.ElementAt((closeCount - 1) / 2).Close;
            Median = median /= 2;
            return Median;
        }
        public async Task<decimal> CalculateRsiAsync()
        {
            decimal sumGain = 0;
            decimal sumLoss = 0;

            var counter1 = 0;
            var _KlinesArr = _Klines.ToArray();

            var upwardMovements = new decimal[RSI_PERIOD * 2];
            var downwardMovements = new decimal[RSI_PERIOD * 2];

            var averageUpwardMovements = new decimal[RSI_PERIOD];
            var averageDownwardMovements = new decimal[RSI_PERIOD];

            var currentAverageUpwardMovements = new decimal[RSI_PERIOD];
            var currentAverageDownwardMovements = new decimal[RSI_PERIOD];

            var previousAverageUpwardMovements = new decimal[RSI_PERIOD * 2];
            var previouseAverageDownwardMovements = new decimal[RSI_PERIOD * 2];

            var relativeStrenths = new decimal[RSI_PERIOD];
            var RSIs = new decimal[RSI_PERIOD];

            foreach (var price in _KlinesArr)
            {
                var currentClose = _KlinesArr[counter1].Close;

                if (counter1 == 0)
                {
                    counter1++;
                    continue;
                }
                await Task.Run(() =>
                {
                    var previouseClose = _KlinesArr[counter1 - 1].Close;

                    var difference = default(decimal);

                    if (currentClose > previouseClose)
                    {
                        difference = currentClose - previouseClose;

                        upwardMovements[counter1 - 1] = difference;

                        previousAverageUpwardMovements[counter1 - 1] = upwardMovements.Skip(counter1 - 1).Take(RSI_PERIOD).Average();

                        if (counter1 > RSI_PERIOD)
                        {
                            sumGain += difference;
                            currentAverageUpwardMovements[counter1 - RSI_PERIOD - 1] = upwardMovements.Skip(counter1 - RSI_PERIOD - 1).Take(RSI_PERIOD).Average();

                            if (counter1 > RSI_PERIOD)
                            {
                                averageUpwardMovements[counter1 - RSI_PERIOD - 1] = (averageUpwardMovements[counter1 - RSI_PERIOD - 1] * RSI_PERIOD - 1
                                + upwardMovements[counter1 - 1]) / RSI_PERIOD;
                            }
                            else
                            {
                                averageUpwardMovements[counter1 - RSI_PERIOD - 1] = currentAverageUpwardMovements[counter1 - RSI_PERIOD - 1];
                            }
                        }
                    }
                    else
                    {
                        difference = previouseClose - currentClose;

                        downwardMovements[counter1 - 1] = difference;

                        if (counter1 > RSI_PERIOD)
                        {
                            sumLoss -= difference;

                            currentAverageDownwardMovements[counter1 - RSI_PERIOD] = downwardMovements.Skip(counter1 - RSI_PERIOD - 1).Take(RSI_PERIOD).Average();

                            if (counter1 > RSI_PERIOD)
                            {
                                averageDownwardMovements[counter1 - RSI_PERIOD] = (averageDownwardMovements[counter1 - RSI_PERIOD - 1] * RSI_PERIOD - 1
                            + downwardMovements[counter1 - 1]) / RSI_PERIOD;
                            }
                            else
                            {
                                averageDownwardMovements[counter1 - RSI_PERIOD - 1] = currentAverageDownwardMovements[counter1 - RSI_PERIOD - 1];
                            }
                        }

                        previouseAverageDownwardMovements[counter1 - 1] = downwardMovements.Skip(counter1 - 1).Take(RSI_PERIOD).Average();
                    }

                    if (counter1 > RSI_PERIOD && currentAverageDownwardMovements.Where(x => x > 0).Any() && currentAverageUpwardMovements.Where(x => x > 0).Any())
                    {
                        var averageUpwardMovement = currentAverageUpwardMovements[counter1 - RSI_PERIOD - 1];
                        var averageDownwardMovement = currentAverageDownwardMovements[counter1 - RSI_PERIOD - 1];

                        if (averageDownwardMovement > 0)
                        {
                            var relativeStrength = averageUpwardMovement / averageDownwardMovement;
                            relativeStrenths[counter1 - RSI_PERIOD - 1] = relativeStrength;

                            RSI = (100 - (100 / (relativeStrength + 1)));

                            RSIs[counter1 - RSI_PERIOD - 1] = RSI;
                        }
                    }

                    counter1++;
                });
            }

            return RSIs.Where(x => x > 0).Last();
        }

        private async void HandleTickUpdates(IEnumerable<IBinanceTick> ticks)
        {
            await GetTradeStatsForPeriod().ConfigureAwait(false);

            //var lines = _Klines.Count();

            foreach (var tick in ticks)
            {
                var symbol = _ticks.Single(t => t.Symbol == tick.Symbol);
                symbol.PriceChangePercent = tick.PriceChangePercent;

                if (tick.Symbol == TRADE_SYMBOL)
                {
                    Movement24Hrs = symbol.PriceChangePercent;
                }
            }

            await InvokeAsync(StateHasChanged);
        }

        private async Task GetTradeStatsForPeriod()
        {
            await Task.Run(() => RenderServerTime());

            message = "Received message";
            _dataProvider.RSI_PERIOD = RSI_PERIOD;

            var callKLinesResult = await _dataProvider.GetKlinesAsync(TRADE_SYMBOL, KlineInterval).ConfigureAwait(false);

            if (callKLinesResult)
            {
                _Klines = callKLinesResult.Data;

                RSI = await Task.Run(() => CalculateRsiAsync());

                Median = await Task.Run(() => CalculateMedian());

                StdDev = await Task.Run(() => CalulateStdDev(_Klines.TakeLast(RSI_PERIOD).Select(x => x.Close)));

                if (RSI >= RSI_OVERBOUGHT)
                {
                    message = "It is overbought, Sell!";

                    // TODO: put binance sell logic here
                }
                else if (RSI <= RSI_OVERSOLD)
                {
                    message = "It is oversold, Buy!";
                }
                else
                {
                    message = "Normal";
                }
            }
        }

        private void RenderServerTime()
        {
            using (var client = new BinanceClient())
            {
                var result = Task.Run(() =>  client.Spot.System.GetServerTime()).ConfigureAwait(false).GetAwaiter().GetResult();
                if (result.Success)
                {
                    ServerTime = result.Data;
                    Console.WriteLine($"Server time: {ServerTime}");
                }
                else
                    Console.WriteLine($"Error: {result.Error}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _dataProvider.Unsubscribe(_subscription);
        }
    }
}
