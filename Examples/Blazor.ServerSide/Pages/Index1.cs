﻿using Binance.Net.Interfaces;
using CryptoExchange.Net.Sockets;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Blazor.ServerSide.Pages
{
    public partial class Index : ComponentBase
    {
        private IEnumerable<IBinanceTick> _ticks = new List<IBinanceTick>();
        private UpdateSubscription _subscription;
        private UpdateSubscription _subscriptionKline;
        private IEnumerable<IBinanceKline> _Klines = new List<IBinanceKline>();
        private IEnumerable<IBinanceKline> _KlinesClosed = new List<IBinanceKline>();
        private IEnumerable<Tuple<string, IBinanceKline>> _KlineSymbol = new List<Tuple<string, IBinanceKline>>();
        private string SOCKET = "wss://stream.binance.com:9443/ws/ethusdt@kline_1m";
        private int RSI_PERIOD = 14;
        private int RSI_OVERBOUGHT = 70;
        private int RSI_OVERSOLD = 30;
        private string TRADE_SYMBOL = "BTCUSDT";
        private double TRADE_QUANTITY = 0.05;

        IEnumerable<IBinanceKline> closes = default(IEnumerable<IBinanceKline>);
        bool inposition = false;

        string message = string.Empty;
        string message2 = string.Empty;

        int closeCount = 0;

        public IBinanceStreamKlineData LastKline { get; private set; } 

        protected override async Task OnInitializedAsync()
        {
            var subResultKline = await _dataProvider.SubscribeToKlineUpdatesAsync(HandleKLineUpdates).ConfigureAwait(false);
            if (subResultKline)
                _subscriptionKline = subResultKline.Data;

            var callResult = await _dataProvider.Get24HPrices().ConfigureAwait(false);
            if (callResult)
                _ticks = callResult.Data;

            var subResult = await _dataProvider.SubscribeTickerUpdates(HandleTickUpdates).ConfigureAwait(false);
            if (subResult)
                _subscription = subResult.Data;




            //var callKLinesResult = await _dataProvider.GetKlinesAsync(TRADE_SYMBOL).ConfigureAwait(false);
            //if (callKLinesResult)
            //    _Klines = callKLinesResult.Data;



            //var callKLinesResult = _dataProvider.GetKlinesAsync(TRADE_SYMBOL).ConfigureAwait(false).GetAwaiter().GetResult();
            //if (callKLinesResult)
            //    _Klines = callKLinesResult.Data;


        }

        private void HandleKLineUpdates(IBinanceStreamKlineData klineData)
        {

            LastKline = klineData;
            InvokeAsync(StateHasChanged);

        }

        private void HandleTickUpdates(IEnumerable<IBinanceTick> ticks)
        {

            message = "Received message";

            var callKLinesResult = _dataProvider.GetKlinesAsync(TRADE_SYMBOL).ConfigureAwait(false).GetAwaiter().GetResult();

            if (callKLinesResult)
            {
                _Klines = callKLinesResult.Data;

                var closedC = _Klines.Where(x => x.CloseTime > DateTime.UtcNow).Count();

                if (closedC > RSI_PERIOD)
                {
                    // TODO: Calculate RSI with talib

                    //var rsi = 



                    if (inposition)
                    {
                        // Overbought! Sell!

                        // put binance sell logic here





                    }
                    else
                    {
                        // It is overbought, but we don't own any. Nothing to do.
                    }
                }

            }



            var lines = _KlinesClosed.Count();

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

            InvokeAsync(StateHasChanged);
        }

        public async ValueTask DisposeAsync()
        {
            await _dataProvider.Unsubscribe(_subscription);
        }
    }

}