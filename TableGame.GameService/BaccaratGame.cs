using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;
using TableGame.Common;

namespace TableGame.GameService
{
    public class BaccaratGame : BaseTableGame
    {
        public BaccaratGame() : base()
        {
            TableType = 1;
            TableCode = "b1";
            GameCode = "baccarat";

            Cards.Clear();
            Cards.Add("player", new List<string>());
            Cards.Add("banker", new List<string>());
        }

        public BaccaratGame(IServerNode node) : base(node)
        {
            TableType = 1;
            TableCode = "b1";
            GameCode = "baccarat";

            Cards.Clear();
            Cards.Add("player", new List<string>());
            Cards.Add("banker", new List<string>());
        }

        public static int GetCardValue(string card)
        {
            int idx = CARD_VALUES.IndexOf(card[0].ToString().ToUpper());
            if (idx < 0) throw new Exception("Wrong card value: " + card);
            int value = idx + 1;
            if (value >= 10) value = 0;
            return value;
        }

        protected int GetPlayerPoints()
        {
            int points = 0;
            var playerCards = Cards["player"];
            foreach (var card in playerCards)
            {
                points += GetCardValue(card[0].ToString());
            }
            return points % 10;
        }

        protected int GetBankerPoints()
        {
            int points = 0;
            var bankerCards = Cards["banker"];
            foreach (var card in bankerCards)
            {
                points += GetCardValue(card[0].ToString());
            }
            return points % 10;
        }

        protected int GetPlayerPairs()
        {
            int pairs = 0;
            var playerCards = Cards["player"];
            for (var i = 0; i < playerCards.Count; i++)
            {
                var cardX = playerCards[i];
                for (var j = i + 1; j < playerCards.Count; j++)
                {
                    var cardY = playerCards[j];
                    if (cardX == cardY)
                    {
                        pairs++;
                        break;
                    }
                }
                if (pairs > 0) break;
            }
            return pairs > 0 ? 1 : 0;
        }

        protected int GetBankerPairs()
        {
            int pairs = 0;
            var bankerCards = Cards["banker"];
            for (var i = 0; i < bankerCards.Count; i++)
            {
                var cardX = bankerCards[i];
                for (var j = i + 1; j < bankerCards.Count; j++)
                {
                    var cardY = bankerCards[j];
                    if (cardX == cardY)
                    {
                        pairs++;
                        break;
                    }
                }
                if (pairs > 0) break;
            }
            return pairs > 0 ? 1 : 0;
        }

        protected int GetTotalCardCount()
        {
            var playerCards = Cards["player"];
            var bankerCards = Cards["banker"];
            return playerCards.Count + bankerCards.Count;
        }

        public int GetSimpleResult()
        {
            int gameResult = 0;
            int playerPoints = GetPlayerPoints();
            int bankerPoints = GetBankerPoints();
            if (playerPoints < bankerPoints) gameResult = 1;
            if (playerPoints > bankerPoints) gameResult = 2;
            if (playerPoints == bankerPoints) gameResult = 3;

            int bankerPairs = GetBankerPairs();
            int playerPairs = GetPlayerPairs();

            int pairs = 0;
            if (bankerPairs > 0 && playerPairs <= 0) pairs = 1;
            else if (bankerPairs <= 0 && playerPairs > 0) pairs = 2;
            else if (bankerPairs > 0 && playerPairs > 0) pairs = 3;

            gameResult = gameResult * 10000 + bankerPoints * 1000 + playerPoints * 100
                         + pairs * 10 + GetTotalCardCount();

            return gameResult;
        }

        public virtual string GetDetailGameResult()
        {
            var playerCards = Cards["player"];
            var bankerCards = Cards["banker"];
            string player = playerCards.Count > 0 ? String.Join(",", playerCards.ToArray()) : "";
            string banker = bankerCards.Count > 0 ? String.Join(",", bankerCards.ToArray()) : "";
            return player + (banker.Length > 0 ? (";" + banker) : "");
        }

        protected override async Task OnGetGameReady()
        {
            await Task.Delay(50);
            TryToRestoreImcompleteGameFromDB();
            if (MainStatus != GAME_STATUS.GetGameReady && Dealer != null)
            {
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "snapshot",
                    snapshot = GetSnapshot()
                }));
            }
        }

        protected override async Task OnCloseTable()
        {
            if (Dealer != null)
            {
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = GetGameStatus(),
                }));
            }

            if (MainStatus == GAME_STATUS.NotWorking)
            {
                //await NotifyCloseTable();
            }
        }

        protected override async Task OnShuffleCards()
        {
            //await NotifyShuffleCards();
        }

        protected override async Task OnPreparing()
        {
            if (Dealer != null && MainStatus > GAME_STATUS.NotWorking)
            {
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = GetGameStatus(),
                    countdown = CurrentCountdown < 0 ? 0 : CurrentCountdown,
                    round = GetRoundId(),
                    hold = IsOnHold,
                    tail = NeedNewShoe(),
                }));
            }
        }

        protected override async Task OnStartNewRound()
        {
            SaveSnapshotToDB();

            if (Dealer != null && MainStatus > GAME_STATUS.NotWorking)
            {
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = GetGameStatus(),
                    countdown = CurrentCountdown,
                    hold = IsOnHold,
                    tail = NeedNewShoe(),
                }));
            }
        }

        protected override async Task BeforeBetting()
        {
            SaveSnapshotToDB();
            Logger.Info("Start betting...");
            //await NotifyStartBetting();
        }

        protected override async Task AfterBetting()
        {
            SaveSnapshotToDB();
            Logger.Info("Stop betting...");
            //await NotifyStopBetting();
        }

        protected override async Task OnBetting()
        {
            if (Dealer != null && MainStatus > GAME_STATUS.NotWorking)
            {
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = GetGameStatus(),
                    countdown = CurrentCountdown,
                    hold = IsOnHold,
                    tail = NeedNewShoe(),
                }));
            }
        }

        protected override async Task OnInputCardValue(string card, string target)
        {
            SaveSnapshotToDB();
            //await NotifyScanCard();
        }

        protected override async Task OnDealing()
        {
            if (Dealer != null && MainStatus > GAME_STATUS.NotWorking)
            {
                var playerCards = Cards["player"];
                var bankerCards = Cards["banker"];
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = GetGameStatus(),
                    countdown = 0,
                    hold = IsOnHold,
                    tail = NeedNewShoe(),
                    cards = new
                    {
                        player = playerCards.ToArray(),
                        banker = bankerCards.ToArray(),
                    },
                }));
            }
        }

        public override async Task<bool> ProcessDealingTime()
        {
            var playerCards = Cards["player"];
            var bankerCards = Cards["banker"];

            if (playerCards.Count == 0) SubStatus = "NeedPlayerCard1";
            else if (bankerCards.Count == 0) SubStatus = "NeedBankerCard1";
            else if (playerCards.Count == 1) SubStatus = "NeedPlayerCard2";
            else if (bankerCards.Count == 1) SubStatus = "NeedBankerCard2";

            if (SubStatus == "CheckFirst4Cards")
            {
                if (playerCards.Count == 2 && bankerCards.Count == 2)
                {
                    int playerCard1 = GetCardValue(playerCards[0][0].ToString());
                    int playerCard2 = GetCardValue(playerCards[1][0].ToString());
                    //int playerCard3 = -1;
                    int playerValue = (playerCard1 + playerCard2) % 10;

                    int bankerCard1 = GetCardValue(bankerCards[0][0].ToString());
                    int bankerCard2 = GetCardValue(bankerCards[1][0].ToString());
                    //int bankerCard3 = -1;
                    int bankerValue = (bankerCard1 + bankerCard2) % 10;

                    if (playerValue == 8 || playerValue == 9
                    || bankerValue == 8 || bankerValue == 9)
                    {
                        // no need to add cards ...
                        SubStatus = STATUS_DONE;
                    }

                    if ((playerValue == 6 || playerValue == 7)
                        && (bankerValue == 6 || bankerValue == 7))
                    {
                        // no need to add cards ...
                        SubStatus = STATUS_DONE;
                    }

                    if (SubStatus != STATUS_DONE) SubStatus = "CheckPlayerCard3";
                }
            }

            if (SubStatus == "CheckPlayerCard3")
            {
                if (playerCards.Count == 2)
                {
                    int playerCard1 = GetCardValue(playerCards[0][0].ToString());
                    int playerCard2 = GetCardValue(playerCards[1][0].ToString());
                    int playerValue = (playerCard1 + playerCard2) % 10;

                    if (playerValue >= 0 && playerValue <= 5)
                    {
                        SubStatus = "NeedPlayerCard3";
                    }

                    if (SubStatus != "NeedPlayerCard3") SubStatus = "CheckBankerCard3";
                }
            }

            if (SubStatus == "CheckBankerCard3")
            {
                if (bankerCards.Count == 2)
                {
                    int playerCard1 = GetCardValue(playerCards[0][0].ToString());
                    int playerCard2 = GetCardValue(playerCards[1][0].ToString());
                    int playerCard3 = -1;
                    int playerValue = (playerCard1 + playerCard2) % 10;
                    if (playerCards.Count == 3)
                    {
                        playerCard3 = GetCardValue(playerCards[2][0].ToString());
                        playerValue = (playerCard1 + playerCard2 + playerCard3) % 10;
                    }

                    int bankerCard1 = GetCardValue(bankerCards[0][0].ToString());
                    int bankerCard2 = GetCardValue(bankerCards[1][0].ToString());
                    //int bankerCard3 = -1;
                    int bankerValue = (bankerCard1 + bankerCard2) % 10;

                    if (bankerValue >= 0 && bankerValue <= 2)
                    {
                        SubStatus = "NeedBankerCard3";
                    }
                    else if (bankerValue == 3)
                    {
                        bool needMore = true;
                        if (playerCard3 == 8) needMore = false;
                        if (needMore)
                        {
                            SubStatus = "NeedBankerCard3";
                        }
                    }
                    else if (bankerValue == 4)
                    {
                        bool needMore = true;
                        if (playerCard3 == 0 || playerCard3 == 1 || playerCard3 == 8 || playerCard3 == 9) needMore = false;
                        if (needMore)
                        {
                            SubStatus = "NeedBankerCard3";
                        }
                    }
                    else if (bankerValue == 5)
                    {
                        bool needMore = true;
                        if (playerCard3 == 0 || playerCard3 == 1 || playerCard3 == 2 || playerCard3 == 3
                            || playerCard3 == 8 || playerCard3 == 9) needMore = false;
                        if (needMore)
                        {
                            SubStatus = "NeedBankerCard3";
                        }
                    }
                    else if (bankerValue == 6)
                    {
                        bool needMore = false;
                        if (playerCard3 == 6 || playerCard3 == 7) needMore = true;
                        if (needMore)
                        {
                            SubStatus = "NeedBankerCard3";
                        }
                    }

                    if (SubStatus != "NeedBankerCard3") SubStatus = STATUS_DONE;
                }
            }

            await OnDealing();

            return MainStatus == GAME_STATUS.DealingTime && SubStatus == STATUS_DONE;

        }

        protected override async Task OnOutputGameResult()
        {
            SaveSnapshotToDB();
            if (Dealer != null && MainStatus > GAME_STATUS.NotWorking)
            {
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = GetGameStatus(),
                    countdown = CurrentCountdown,
                    result = GameResult,
                    hold = IsOnHold,
                    tail = NeedNewShoe(),
                }));
            }
        }

        public override async Task<bool> OutputGameResult()
        {
            Logger.Info("OutputGameResult...");
            GameResult = GetDetailGameResult();
            await OnOutputGameResult();
            return MainStatus == GAME_STATUS.OutputGameResult;
        }

        protected override async Task OnConfirmation()
        {
            if (Dealer != null && MainStatus > GAME_STATUS.NotWorking)
            {
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = GetGameStatus(),
                    countdown = CurrentCountdown,
                    result = GameResult,
                    hold = IsOnHold,
                    tail = NeedNewShoe(),
                }));
            }
        }

        protected override async Task BeforeEndRoundCleanUp()
        {
            SaveSnapshotToDB();
            //await NotifyGameResult();
        }

        protected override async Task OnEndCurrentRound()
        {
            if (Dealer != null && MainStatus > GAME_STATUS.NotWorking)
            {
                await Dealer.Session.Send(JsonHelper.ToJsonString(new
                {
                    msg = "snapshot",
                    snapshot = GetSnapshot()
                }));
            }
        }

        protected override dynamic GetExtra()
        {
            dynamic extra = base.GetExtra();
            if (extra == null) return new { shoe = CurrentShoeNumber };
            else
            {
                dynamic newExtra = JsonHelper.ToJsonObject(JsonHelper.ToJsonString(extra));
                newExtra.shoe = CurrentShoeNumber;
                return newExtra;
            }
        }

        protected override void LoadFromExtra(string value)
        {
            base.LoadFromExtra(value);
            if (string.IsNullOrEmpty(value)) return;
            dynamic extra = JsonHelper.ToJsonObject(value);

            decimal shoeNumber = extra.shoe;
            CurrentShoeNumber = Convert.ToInt32(shoeNumber);
        }

        public virtual void SaveSnapshotToDB()
        {
            if (string.IsNullOrEmpty(DBName)) return;
            if (MainStatus < GAME_STATUS.StartNewRound) return;

            //System.Diagnostics.Debugger.Break();

            dynamic snapshot = GetSnapshot();
            string snapshotValue = JsonHelper.ToJsonString(snapshot);
            dynamic snapshotObject = JsonHelper.ToJsonObject(snapshotValue);
            int status = (int)MainStatus;
            string roundId = snapshotObject.round;

            try
            {
                var dbhelper = Node.GetDataHelper();
                using (var cnn = dbhelper.OpenDatabase(DBName))
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@round_id", roundId);
                        dbhelper.AddParam(cmd, "@round_status", status);
                        dbhelper.AddParam(cmd, "@round_snapshot", snapshotValue);

                        if (MainStatus == GAME_STATUS.StartNewRound)
                        {
                            cmd.CommandText = " insert into tbl_game_round ( round_id , round_status , round_snapshot )"
                                               + " values ( @round_id , @round_status , @round_snapshot ) "
                                               ;
                        }
                        else
                        {
                            cmd.CommandText = " update tbl_game_round "
                                               + " set round_status = @round_status "
                                               + ", round_snapshot = @round_snapshot "
                                               + ", last_update = GETDATE() "
                                               + " where round_id = @round_id "
                                               ;
                        }

                        cmd.ExecuteNonQuery();

                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception found when save snapshot: " + ex.Message);
                Logger.Error(ex.StackTrace);
            }
        }

        public virtual void TryToRestoreImcompleteGameFromDB()
        {
            if (string.IsNullOrEmpty(DBName)) return;

            Logger.Info("Try to restore imcomplete game from DB ...");

            int gameStatus = -1;
            string roundId = "";
            string snapshotValue = "";

            try
            {
                var dbhelper = Node.GetDataHelper();
                using (var cnn = dbhelper.OpenDatabase(DBName))
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.CommandText = " select top 1 * from tbl_game_round order by record_id desc ";
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                gameStatus = Convert.ToInt32(reader["round_status"].ToString());
                                roundId = reader["round_id"].ToString();
                                snapshotValue = reader["round_snapshot"].ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception found when try to restore from snapshot: " + ex.Message);
                Logger.Error(ex.StackTrace);
            }

            if (gameStatus < (int)GAME_STATUS.EndCurrentRound
                && !string.IsNullOrEmpty(roundId)
                && !string.IsNullOrEmpty(snapshotValue))
            {
                Logger.Info("Restoring " + roundId + " ... ");
                LoadFromSnapshot(snapshotValue);
            }

            Logger.Info("Done");
        }

        public int CurrentShoeNumber { get; protected set; } = 0;
        //public string FeedServerUrl { get; set; } = "";
        public string DBName { get; set; } = "";

        public override int GetFirstRoundNumber()
        {
            return DateTime.Now.Minute + 1;
        }

        public virtual string GetOutputDealerId()
        {
            return Dealer == null ? "" : Dealer.DealerId;
        }

        public virtual int GetCurrentDealerNumber()
        {
            return 1;
        }

        public override string CreateNewShoe()
        {
            var result = base.CreateNewShoe();
            var needFirstNumber = CurrentShoeNumber <= 0;
            if (!needFirstNumber && !string.IsNullOrEmpty(ShoeCode))
            {
                var lastDayStr = ShoeCode.Substring(0, 8);
                var currentDayStr = DateTime.Now.ToString("yyyyMMdd");
                if (lastDayStr != currentDayStr) needFirstNumber = true;
            }
            if (needFirstNumber) CurrentShoeNumber = GetFirstRoundNumber();
            else CurrentShoeNumber++;
            if (CurrentShoeNumber <= 0) CurrentShoeNumber = 1;
            if (CurrentShoeNumber >= 99) CurrentShoeNumber = 98;
            return result;
        }

        protected virtual int GetFirstShoeNumber()
        {
            var dtNow = DateTime.Now;
            var dtCurrent = DateTime.Today;
            int firstShoeNumber = 0;
            while (dtCurrent <= dtNow)
            {
                firstShoeNumber++;
                dtCurrent = dtCurrent.AddMinutes(15);
            }
            if (firstShoeNumber <= 0) firstShoeNumber = 1;
            return firstShoeNumber;
        }

        protected long GetTimestemp()
        {
            return new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
        }

        protected long GetSeqNumber()
        {
            return new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
        }

        protected List<string> ChangeCardFormat(List<string> cards)
        {
            List<string> cardList = new List<string>();
            foreach (var card in cards)
            {
                var newCard = card[1].ToString().ToLower();
                int idx = CARD_VALUES.IndexOf(card[0].ToString().ToUpper()) + 1;
                if (idx > 9) newCard += idx.ToString();
                else newCard += "0" + idx;
                cardList.Add(newCard);
            }
            return cardList;
        }

        protected Tuple<int, bool, bool> GetSimpleResultEBet()
        {
            if (GameResult == "-1")
            {
                return new Tuple<int, bool, bool>(-1, false, false);
            }

            int gameResult = 0;
            int playerPoints = GetPlayerPoints();
            int bankerPoints = GetBankerPoints();
            if (playerPoints < bankerPoints) gameResult = 80;
            if (playerPoints > bankerPoints) gameResult = 60;
            if (playerPoints == bankerPoints) gameResult = 68;

            int bankerPairs = GetBankerPairs();
            int playerPairs = GetPlayerPairs();

            return new Tuple<int, bool, bool>(gameResult, bankerPairs > 0, playerPairs > 0);

        }

        protected virtual async Task RemoteRequest(string url, object msg)
        {
            Logger.Warn(JsonHelper.ToJsonString(msg));
            await RemoteCaller.Request(url, msg);
        }

        /*
        protected virtual async Task NotifyStartBetting()
        {
            if (string.IsNullOrEmpty(FeedServerUrl)) return;

            try
            {
                var msg = new
                {
                    tableEventData = new
                    {
                        notifyType = 30001,
                        table = TableCode.ToUpper(),
                        tableType = TableType,
                        timestamp = GetTimestemp(),
                        data = new
                        {
                            bootsNumber = CurrentShoeNumber,
                            roundId = GetRoundId(),
                            betTimeSec = TotalBettingCountdown,
                            dealer = GetOutputDealerId(),
                            dealerId = GetCurrentDealerNumber()
                        }
                    }
                };

                await RemoteRequest(FeedServerUrl, msg);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send NotifyStartBetting");
                Logger.Error(ex.StackTrace);
            }
        }

        protected virtual async Task NotifyStopBetting()
        {
            if (string.IsNullOrEmpty(FeedServerUrl)) return;

            try
            {
                var msg = new
                {
                    tableEventData = new
                    {
                        notifyType = 30004,
                        table = TableCode.ToUpper(),
                        tableType = TableType,
                        timestamp = GetTimestemp(),
                        data = new
                        {
                            roundId = GetRoundId(),
                        }
                    }
                };

                await RemoteRequest(FeedServerUrl, msg);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send NotifyStopBetting");
                Logger.Error(ex.StackTrace);
            }

        }

        protected virtual async Task NotifyScanCard()
        {
            if (string.IsNullOrEmpty(FeedServerUrl)) return;

            try
            {
                var playerCards = Cards["player"];
                var bankerCards = Cards["banker"];

                var playerCardList = ChangeCardFormat(playerCards);
                var bankerCardList = ChangeCardFormat(bankerCards);

                var msg = new
                {
                    tableEventData = new
                    {
                        notifyType = 30005,
                        table = TableCode.ToUpper(),
                        tableType = TableType,
                        timestamp = GetTimestemp(),
                        data = new
                        {
                            roundId = GetRoundId(),
                            result = new
                            {
                                baccarat = new
                                {
                                    playerCard = playerCardList.ToArray(),
                                    bankerCard = bankerCardList.ToArray(),
                                }
                            }
                        }
                    }
                };

                await RemoteRequest(FeedServerUrl, msg);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send NotifyScanCard");
                Logger.Error(ex.StackTrace);
            }
        }

        protected virtual async Task NotifyGameResult()
        {
            if (string.IsNullOrEmpty(FeedServerUrl)) return;

            try
            {
                var playerCards = Cards["player"];
                var bankerCards = Cards["banker"];

                var playerCardList = ChangeCardFormat(playerCards);
                var bankerCardList = ChangeCardFormat(bankerCards);
                var gameResult = GetSimpleResultEBet();

                var msg = new
                {
                    tableEventData = new
                    {
                        notifyType = 30002,
                        table = TableCode.ToUpper(),
                        tableType = TableType,
                        timestamp = GetTimestemp(),
                        data = new
                        {
                            roundId = GetRoundId(),
                            bootsNumber = CurrentShoeNumber,
                            result = new
                            {
                                baccarat = new
                                {
                                    winner = gameResult.Item1,
                                    bankerPair = gameResult.Item2,
                                    playerPair = gameResult.Item3,
                                    playerCard = playerCardList.ToArray(),
                                    bankerCard = bankerCardList.ToArray(),
                                }
                            },
                            seqNo = GetSeqNumber()
                        }
                    }
                };

                await RemoteRequest(FeedServerUrl, msg);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send NotifyGameResult");
                Logger.Error(ex.StackTrace);
            }
        }

        protected virtual async Task NotifyShuffleCards()
        {
            if (string.IsNullOrEmpty(FeedServerUrl)) return;

            try
            {
                var msg = new
                {
                    tableEventData = new
                    {
                        notifyType = 30003,
                        table = TableCode.ToUpper(),
                        tableType = TableType,
                        timestamp = GetTimestemp(),
                        data = new
                        {
                            bootsNumber = CurrentShoeNumber,
                            dealer = GetOutputDealerId(),
                            dealerId = GetCurrentDealerNumber()
                        }
                    }
                };

                await RemoteRequest(FeedServerUrl, msg);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send NotifyShuffleCards");
                Logger.Error(ex.StackTrace);
            }
        }

        protected virtual async Task NotifyCloseTable()
        {
            if (string.IsNullOrEmpty(FeedServerUrl)) return;

            try
            {
                var msg = new
                {
                    tableEventData = new
                    {
                        notifyType = 30006,
                        table = TableCode.ToUpper(),
                        tableType = TableType,
                        timestamp = GetTimestemp(),
                        data = new
                        {
                            isMaintain = 1
                        }
                    }
                };

                await RemoteRequest(FeedServerUrl, msg);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send NotifyCloseTable");
                Logger.Error(ex.StackTrace);
            }

        }
        */
    }
}
