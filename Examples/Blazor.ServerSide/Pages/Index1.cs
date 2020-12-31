using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
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
        private UpdateSubscription _subscriptionKline;
        private IEnumerable<IBinanceKline> _Klines = new List<IBinanceKline>();

        private IEnumerable<Tuple<string, IBinanceKline>> _KlineSymbol = new List<Tuple<string, IBinanceKline>>();
        //kcprivate string SOCKET = "wss://stream.binance.com:9443/ws/ethusdt@kline_1m";
        private int RSI_PERIOD = 14;  // must be even number
        private int RSI_OVERBOUGHT = 70;
        private int RSI_OVERSOLD = 30;
        private string TRADE_SYMBOL = "BTCUSDT";
        private double TRADE_QUANTITY = 0.05;

        IEnumerable<IBinanceKline> closes = default(IEnumerable<IBinanceKline>);
        bool inposition = false;
        string message = string.Empty;
        string message2 = string.Empty;

        DateTime ServerTime = default(DateTime);

        Decimal RSI = 0;

        Decimal Median = 0;

        int closeCount = 0;
        private IEnumerable<decimal> _closePrices;
        private bool PricesInit;

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


        public static decimal StdDev(IEnumerable<decimal> values)
        {
            // ref: http://warrenseen.com/blog/2006/03/13/how-to-calculate-standard-deviation/
            double mean = 0.0;
            double sum = 0.0;
            double stdDev = 0.0;
            int n = 0;
            foreach (double val in values)
            {
                n++;
                double delta = val - mean;
                mean += delta / n;
                sum += delta * (val - mean);
            }
            if (1 < n)
                stdDev = Math.Sqrt(sum / (n - 1));

            return (decimal)stdDev;
        }


        public decimal CalculateMedian()
        {
            var orderedClosing = _Klines.OrderBy(x => x.Close);
            var median = orderedClosing.ElementAt(closeCount / 2).Close + orderedClosing.ElementAt((closeCount - 1) / 2).Close;
            return median /= 2;
        }
        public async Task<decimal> CalculateRsiAsync()
        {
            //await InitializeData().ConfigureAwait(false);

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
            RenderServerTime();

            message = "Received message";
            _dataProvider.RSI_PERIOD = RSI_PERIOD;
            _dataProvider.KLinesStartTime = DateTime.UtcNow.AddMinutes(-15);
            _dataProvider.KlinesEndTime = DateTime.UtcNow.AddMinutes(-1);

            var callKLinesResult = await _dataProvider.GetKlinesAsync(TRADE_SYMBOL, KlineInterval).ConfigureAwait(false);

            if (callKLinesResult)
            {
                _Klines = callKLinesResult.Data;

                RSI = await Task.Run(() => CalculateRsiAsync());

                Median = await Task.Run(() => CalculateMedian());

                if (inposition)
                {
                    // Overbought! Sell!

                    // put binance sell logic here
                }
                else
                {
                    message = "It is overbought, but we don't own any. Nothing to do.";
                }
            }

            var lines = _Klines.Count();

            if (closeCount > RSI_PERIOD)
            {
                message2 = $"RSI_PERIOD Reached {lines}";

            }
            else
            {
                message2 = $"RSI_PERIOD {lines}";

            }

            foreach (var tick in ticks)
            {
                //var callKLinesResult1 = _dataProvider.GetKlinesAsync(TRADE_SYMBOL).ConfigureAwait(false).GetAwaiter().GetResult();
                //if (callKLinesResult1)
                //    _KlineSymbol = callKLinesResult1.Data.Select(x => new Tuple<string, IBinanceKline>(tick.Symbol,x));

                var symbol = _ticks.Single(t => t.Symbol == tick.Symbol);
                symbol.PriceChangePercent = tick.PriceChangePercent;
            }

            await InvokeAsync(StateHasChanged);
        }

        private void RenderServerTime()
        {
            using (var client = new BinanceClient())
            {
                var result = client.Spot.System.GetServerTime();
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
