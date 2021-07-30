using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace TableGame.Common
{
    public enum GAME_STATUS
    {
        Unknown = 0,
        NotWorking,       // 1
        GetGameReady,     // 2 
        PreparingTime,    // 3
        StartNewRound,    // 4
        BettingTime,      // 5
        DealingTime,      // 6
        OutputGameResult, // 7
        ConfirmationTime, // 8
        EndCurrentRound   // 9 
    };

    public class BaseTableGame
    {
        public static List<string> CARD_SUITS = new List<string>
        {
            "S" /* Spade (♠) */,
            "H" /* Heart (♥) */,
            "C" /* Club (♣) */,
            "D" /* Diamond (♦) */,

        };
        public static List<string> CARD_VALUES = new List<string>
        {
            "A" /* Ace */,
            "2" /* 2 */,
            "3" /* 3 */,
            "4" /* 4 */,
            "5" /* 5 */,
            "6" /* 6 */,
            "7" /* 7 */,
            "8" /* 8 */,
            "9" /* 9 */,
            "T" /* 10 */,
            "J" /* Jack */,
            "Q" /* Queen */,
            "K" /* King */,

        };

        protected List<string> GameStatusList = new List<string>
        {
            "Unknown",
            "NotWorking",
            "GetGameReady",
            "PreparingTime",
            "StartNewRound",
            "BettingTime",
            "DealingTime",
            "OutputGameResult",
            "ConfirmationTime",
            "EndCurrentRound"
        };

        public const string STATUS_DONE = "Done";
        public const string STATUS_ON_GOING = "OnGoing";
        public const string STATUS_SHUFFLING = "ShuffleCards";

        public GAME_STATUS MainStatus { get; protected set; } = GAME_STATUS.Unknown;
        public string SubStatus { get; protected set; } = "";

        public GAME_STATUS LastMainStatus { get; protected set; } = GAME_STATUS.Unknown;
        public string LastSubStatus { get; protected set; } = "";

        public string TableCode { get; set; } = "T9";
        public string GameCode { get; protected set; } = "table";

        public int TableType { get; protected set; } = 1;
        public int GameType { get; protected set; } = 1;

        public string ShoeCode { get; set; } = "";
        public int RoundNumber { get; set; } = 0;

        public int MaxRoundNumber { get; set; } = 60;

        public Dictionary<string, List<string>> Cards { get; protected set; } = new Dictionary<string, List<string>>();

        public bool IsOnHold { get; set; } = false;
        public bool RequestNewShoe { get; set; } = false;
        public bool RequestToClose { get; set; } = false;
        public bool RequestToShuffle { get; set; } = false;

        public int TotalPreparingCountdown { get; set; } = 5;
        public int TotalBettingCountdown { get; set; } = 30;
        public int TotalDealingCountdown { get; set; } = 5;
        public int TotalConfirmationCountdown { get; set; } = 5;

        public int CurrentCountdown { get; set; } = -1; // disable it by default

        public string GameResult { get; set; } = "";

        public static CommonRng Rng { get; private set; } = new CommonRng();

        private Timer m_Timer = null;

        public IServerNode Node { get; protected set; } = null;
        public IServerLogger Logger { get; protected set; } = null;
        public IJsonCodec JsonHelper { get; protected set; } = null;

        public TableGameDealer Dealer { get; set; } = null;

        protected ConcurrentQueue<TableRequest> Requests { get; set; } = new ConcurrentQueue<TableRequest>();

        private int m_RuntimeErrors = 0;
        private bool m_IsServerWorking = false;
        private bool m_IsRunningGameLoop = false;

        public static string GenCard()
        {
            return CARD_VALUES[Rng.Next(CARD_VALUES.Count)] + CARD_SUITS[Rng.Next(CARD_SUITS.Count)];
        }

        public virtual bool IsValidCard(string card)
        {
            int idx1 = -1;
            int idx2 = -1;
            if (!string.IsNullOrEmpty(card))
            {
                idx1 = CARD_VALUES.IndexOf(card[0].ToString().ToUpper());
                if (card.Length > 1) idx2 = CARD_SUITS.IndexOf(card[1].ToString().ToUpper());
            }
            return idx1 >= 0 && idx2 >= 0;
        }

        public BaseTableGame()
        {
            Node = null;
            Logger = null;
            JsonHelper = null;

            Cards.Clear();
            Cards.Add("player", new List<string>());
        }

        public BaseTableGame(IServerNode node) : this()
        {
            Node = node;
            Logger = Node.GetLogger();
            JsonHelper = Node.GetJsonHelper();
        }

        public virtual string GetRoundId()
        {
            return TableCode.ToUpper() + "-" 
                    + (string.IsNullOrEmpty(ShoeCode) ? "[?]" : ShoeCode) + "-" 
                    + (RoundNumber <= 0 ? 1 : RoundNumber);
        }

        public virtual string GetDealerId()
        {
            return Dealer == null ? null : Dealer.DealerId;
        }

        public virtual string GetGameStatus()
        {
            var status = GameStatusList[(int)MainStatus];
            if (!string.IsNullOrEmpty(SubStatus)) status += "-" + SubStatus;
            return status;
        }

        public virtual void NextStatus()
        {
            // skip 2 non-working state
            if (MainStatus == GAME_STATUS.Unknown || MainStatus == GAME_STATUS.NotWorking)
            {
                CurrentCountdown = -1; // disable it by default
                SubStatus = "";
                return;
            }

            // loop states for every round
            LastMainStatus = MainStatus;
            if (MainStatus == GAME_STATUS.EndCurrentRound) MainStatus = GAME_STATUS.PreparingTime;
            else MainStatus++;

            // reset sub state
            LastSubStatus = SubStatus;
            SubStatus = "";

            // reset countdown if need
            if (MainStatus == GAME_STATUS.PreparingTime) CurrentCountdown = TotalPreparingCountdown;
            else if (MainStatus == GAME_STATUS.BettingTime) CurrentCountdown = TotalBettingCountdown;
            else if (MainStatus == GAME_STATUS.DealingTime) CurrentCountdown = TotalDealingCountdown;
            else if (MainStatus == GAME_STATUS.ConfirmationTime) CurrentCountdown = TotalConfirmationCountdown;
            else CurrentCountdown = -1;
            
        }

        public virtual void SetSubStatus(string newSubStatus)
        {
            if (newSubStatus != null)
            {
                LastSubStatus = SubStatus;
                SubStatus = newSubStatus;
            }
        }

        public virtual void SetStatus(GAME_STATUS newMainStatus, string newSubStatus = null)
        {
            if (MainStatus != newMainStatus)
            {
                LastMainStatus = MainStatus;
                MainStatus = newMainStatus;
                CurrentCountdown = -1; // disable it by default
                LastSubStatus = SubStatus;
                SubStatus = "";
            }

            if (newSubStatus != null)
            {
                LastSubStatus = SubStatus;
                SubStatus = newSubStatus;
            }

            // always reset countdown
            if (MainStatus == GAME_STATUS.PreparingTime) CurrentCountdown = TotalPreparingCountdown;
            else if (MainStatus == GAME_STATUS.BettingTime) CurrentCountdown = TotalBettingCountdown;
            else if (MainStatus == GAME_STATUS.DealingTime) CurrentCountdown = TotalDealingCountdown;
            else if (MainStatus == GAME_STATUS.ConfirmationTime) CurrentCountdown = TotalConfirmationCountdown;
            else CurrentCountdown = -1;
        }

        public virtual bool NeedNewShoe()
        {
            return RequestNewShoe 
                    || (RoundNumber > MaxRoundNumber && MaxRoundNumber > 0)
                    || RoundNumber < 0
                    || string.IsNullOrEmpty(ShoeCode);
        }

        public virtual int GetFirstRoundNumber()
        {
            return 1;
        }

        public virtual string CreateNewShoe()
        {
            ShoeCode = DateTime.Now.ToString("yyyyMMddHHmmss");
            RequestNewShoe = false;
            RoundNumber = GetFirstRoundNumber() - 1; // ...
            if (RoundNumber < 0) RoundNumber = 0;
            return ShoeCode;
        }

        public bool IsWorking()
        {
            return m_IsServerWorking;
        }

        public virtual bool IsOpen()
        {
            return IsWorking() && MainStatus > GAME_STATUS.NotWorking;
        }

        public virtual void ClearCards()
        {
            foreach (var cardSet in Cards) cardSet.Value.Clear();
        }

        public virtual async Task<bool> OpenTable()
        {
            if (MainStatus == GAME_STATUS.NotWorking)
            {
                if (Dealer != null)
                {                    
                    SetStatus(GAME_STATUS.GetGameReady, "");

                    RoundNumber = 0;
                    RequestNewShoe = true;
                    RequestToClose = false;
                    RequestToShuffle = false;
                    ShoeCode = "";

                    IsOnHold = false;
                    CurrentCountdown = -1;

                    GameResult = "";
                    ClearCards();

                    TableRequest req = null;
                    while (Requests.TryDequeue(out req)) { }

                    await OnOpenTable();

                    return true;
                }
            }
            return false;
        }

        protected virtual async Task OnOpenTable()
        {
            Logger.Info("OnOpenTable");
            await Task.Delay(1);
        }

        public virtual async Task<bool> CloseTable()
        {
            if (MainStatus > GAME_STATUS.NotWorking)
            {
                SetStatus(GAME_STATUS.NotWorking, "");

                IsOnHold = false;
                RequestNewShoe = false;
                RequestToClose = false;
                RequestToShuffle = false;
                CurrentCountdown = -1;

                TableRequest req = null;
                while (Requests.TryDequeue(out req)) { }

                await OnCloseTable();

                return true;
            }
            return false;
        }

        protected virtual async Task OnCloseTable()
        {
            Logger.Info("OnCloseTable");
            await Task.Delay(1);
        }

        public virtual async Task Start()
        {
            await Stop();

            m_RuntimeErrors = 0;
            m_IsServerWorking = false;

            RoundNumber = 0;
            RequestNewShoe = true;
            RequestToClose = false;
            RequestToShuffle = false;
            ShoeCode = "";

            //MainStatus = GAME_STATUS.GetGameReady;
            //SubStatus = "GetServerReady";
            MainStatus = GAME_STATUS.NotWorking;
            SubStatus = "";

            LastMainStatus = MainStatus;
            LastSubStatus = SubStatus;

            IsOnHold = false;

            //m_CurrentGameResult = "";
            //m_History.Clear();

            m_IsRunningGameLoop = false;

            m_IsServerWorking = true;
            m_Timer = new Timer(Tick, Rng, 500, 1000 * 1);
        }

        public virtual async Task Stop()
        {
            m_IsServerWorking = false;

            LastMainStatus = MainStatus;
            LastSubStatus = SubStatus;

            MainStatus = GAME_STATUS.NotWorking;
            SubStatus = "";

            if (m_Timer != null)
            {
                await Task.Delay(500);
                m_Timer.Dispose();
                await Task.Delay(500);
                m_Timer = null;
            }

            IsOnHold = false;
            RequestNewShoe = false;
            RequestToClose = false;
            RequestToShuffle = false;
            CurrentCountdown = -1;

            GameResult = "";
            ClearCards();

            TableRequest req = null;
            while (Requests.TryDequeue(out req)) { }

            var dealer = Dealer;
            if (dealer != null && dealer.Session != null)
            {
                dealer.Session.CloseConnection();
            }
            Dealer = null;

            m_IsRunningGameLoop = false;
            m_RuntimeErrors = 0;
        }

        protected virtual async Task CheckDealerOnline()
        {
            var dealer = Dealer;
            if (dealer != null && dealer.Session != null)
            {
                if (!dealer.Session.IsConnected())
                {
                    Dealer = null;
                    await Task.Delay(1);
                }
            }
        }

        public virtual void SetDealer(TableGameDealer dealer)
        {
            var oldOne = Dealer;
            if (oldOne != null && oldOne.Session != null)
            {
                oldOne.Session.CloseConnection();
            }
            Dealer = dealer;
        }

        protected virtual async Task BeforeGameFrame()
        {
            await CheckDealerOnline();
            await ProcessRequests();
        }

        protected virtual async Task GameFrame()
        {
            bool canGoToNext = false;
            switch (MainStatus)
            {
                case (GAME_STATUS.Unknown): break;
                case (GAME_STATUS.NotWorking): break;
                case (GAME_STATUS.GetGameReady):
                    canGoToNext = await GetGameReady();
                    break;
                case (GAME_STATUS.PreparingTime):
                    if (string.IsNullOrEmpty(SubStatus))
                    {
                        await BeforePreparing();
                        SetSubStatus(STATUS_ON_GOING);
                    }
                    canGoToNext = await ProcessPreparingTime();
                    if (canGoToNext) await AfterPreparing();
                    break;
                case (GAME_STATUS.StartNewRound):
                    canGoToNext = await StartNewRound();
                    break;
                case (GAME_STATUS.BettingTime):
                    if (string.IsNullOrEmpty(SubStatus))
                    {
                        await BeforeBetting();
                        SetSubStatus(STATUS_ON_GOING);
                    }
                    canGoToNext = await ProcessBettingTime();
                    if (canGoToNext) await AfterBetting();
                    break;
                case (GAME_STATUS.DealingTime):
                    if (string.IsNullOrEmpty(SubStatus))
                    {
                        await BeforeDealing();
                        SetSubStatus(STATUS_ON_GOING);
                    }
                    canGoToNext = await ProcessDealingTime();
                    if (canGoToNext) await AfterDealing();
                    break;
                case (GAME_STATUS.OutputGameResult):
                    canGoToNext = await OutputGameResult();
                    break;
                case (GAME_STATUS.ConfirmationTime):
                    if (string.IsNullOrEmpty(SubStatus))
                    {
                        await BeforeConfirmation();
                        SetSubStatus(STATUS_ON_GOING);
                    }
                    canGoToNext = await ProcessConfirmationTime();
                    if (canGoToNext) await AfterConfirmation();
                    break;
                case (GAME_STATUS.EndCurrentRound):
                    canGoToNext = await EndCurrentRound();
                    break;
            }

            if (canGoToNext) NextStatus();
        }

        protected virtual async Task AfterGameFrame()
        {
            //Logger.Info("AfterGameFrame");
            await Task.Delay(1);
        }

        protected virtual async void Tick(object param)
        {
            if (m_RuntimeErrors > 0) return;
            if (!m_IsServerWorking) return;

            if (m_IsRunningGameLoop) return;
            m_IsRunningGameLoop = true;
            try
            {
                await BeforeGameFrame();
                await GameFrame();
                await AfterGameFrame();
            }
            catch (Exception ex)
            {
                m_RuntimeErrors++;
                Logger.Error(ex.ToString());
                Logger.Error(ex.StackTrace);
            }
            finally
            {
                m_IsRunningGameLoop = false;
            }

        }

        public virtual void AddRequest(RequestContext context, dynamic data, Func<BaseTableGame, RequestContext, dynamic, Task> process)
        {
            if (m_IsServerWorking) Requests.Enqueue(new TableRequest(context, data, process));
        }

        public virtual async Task ProcessRequests()
        {
            TableRequest req = null;
            while (Requests.TryDequeue(out req))
            {
                //Logger.Info("Processing request...");
                await req.Process(this, req.Context, req.Data);
            }
        }

        protected virtual async Task OnGetGameReady()
        {
            Logger.Info("OnGetGameReady");
            await Task.Delay(1);
        }

        protected virtual async Task<bool> GetGameReady()
        {
            if (MainStatus != GAME_STATUS.GetGameReady) return false;

            var foundError = false;

            Logger.Info("Check server settings...");
            await Task.Delay(100);
            Logger.Info("Check game settings...");

            if (foundError) return false;
            Logger.Info("Done");

            await OnGetGameReady();

            return MainStatus == GAME_STATUS.GetGameReady;
        }

        protected virtual async Task BeforePreparing()
        {
            await Task.Delay(1);
        }

        protected virtual async Task OnShuffleCards()
        {
            Logger.Info("Shuffling ...");
            await Task.Delay(1);
        }

        protected virtual async Task OnPreparing()
        {
            await Task.Delay(1);
        }

        public virtual async Task<bool> ProcessPreparingTime()
        {
            Logger.Info("Preparing...");

            if (RequestToClose)
            {
                await CloseTable();
                Logger.Info("Table Closed");
                return false;
            }

            if (Dealer == null)
            {
                Logger.Info("Preparing Game - Await dealer login...");
                return false;
            }

            bool startNewShoe = NeedNewShoe();
            if (startNewShoe) CreateNewShoe(); // ...

            if (RequestToShuffle || (startNewShoe && SubStatus == STATUS_ON_GOING))
            {
                RequestToShuffle = false;
                SubStatus = STATUS_SHUFFLING;
                await OnShuffleCards();
            }

            if (SubStatus == STATUS_SHUFFLING)
            {
                Logger.Info("Preparing Game - Shuffling...");
                CurrentCountdown = -1;
            }

            if (CurrentCountdown > 0 || CurrentCountdown < 0)
            {
                if (CurrentCountdown > 0) Logger.Info("Preparing time - " + CurrentCountdown);
                await OnPreparing();
                if (CurrentCountdown > 0 && !IsOnHold) CurrentCountdown--;

                if (SubStatus != STATUS_SHUFFLING && CurrentCountdown < 0)
                {
                    Logger.Info("Preparing Game - ShufflingDone");
                    CurrentCountdown = TotalPreparingCountdown;
                    SubStatus = STATUS_ON_GOING;
                }

                return false;
            }

            return MainStatus == GAME_STATUS.PreparingTime;
        }

        protected virtual async Task AfterPreparing()
        {
            await Task.Delay(1);
        }

        protected virtual async Task OnStartNewRound()
        {
            await Task.Delay(1);
        }

        public virtual async Task<bool> StartNewRound()
        {
            Logger.Info("Start a new round...");

            RoundNumber++;
            if (RoundNumber <= 0) RoundNumber = 1;

            IsOnHold = false;

            var newId = GetRoundId();
            Logger.Info("New round: " + newId);

            GameResult = "";
            ClearCards();

            //Logger.Info("Saving new game record to database...");

            await OnStartNewRound();

            //Logger.Info("Start betting time...");

            return MainStatus == GAME_STATUS.StartNewRound;
        }

        protected virtual async Task BeforeBetting()
        {
            await Task.Delay(1);
        }

        protected virtual async Task AfterBetting()
        {
            await Task.Delay(1);
        }

        protected virtual async Task OnBetting()
        {
            await Task.Delay(1);
        }

        public virtual async Task<bool> ProcessBettingTime()
        {
            if (CurrentCountdown > 0)
            {
                Logger.Info("Betting time - " + CurrentCountdown);
                await OnBetting();
                if (!IsOnHold) CurrentCountdown--;
                return false;
            }
            return MainStatus == GAME_STATUS.BettingTime;
        }

        protected virtual async Task BeforeDealing()
        {
            await Task.Delay(1);
        }

        protected virtual async Task AfterDealing()
        {
            await Task.Delay(1);
        }

        protected virtual async Task OnDealing()
        {
            await Task.Delay(1);
        }

        protected virtual async Task OnInputCardValue(string card, string target)
        {
            await Task.Delay(1);
        }

        public virtual async Task AddNewCard(string card, string target, string newSubStatus = null)
        {
            if (IsValidCard(card))
            {
                foreach (var cardSet in Cards)
                {
                    if (target.ToLower().Contains(cardSet.Key.ToLower()))
                    {
                        cardSet.Value.Add(card);
                        if (newSubStatus != null) SetSubStatus(newSubStatus);
                        await OnInputCardValue(card, target);
                    }
                }
            }
        }

        protected virtual async Task OnVoidLastCard(string card, string target)
        {
            await Task.Delay(1);
        }

        public virtual async Task RemoveLastCard(string target, string newSubStatus = null)
        {
            foreach (var cardSet in Cards)
            {
                if (target.ToLower().Contains(cardSet.Key.ToLower()))
                {
                    string card = cardSet.Value.Last();
                    cardSet.Value.RemoveAt(cardSet.Value.Count - 1);
                    if (newSubStatus != null) SetStatus(GAME_STATUS.DealingTime, newSubStatus);
                    else SetStatus(GAME_STATUS.DealingTime);
                    await OnVoidLastCard(card, target);
                }
            }
        }


        public virtual async Task<bool> ProcessDealingTime()
        {
            var playerCards = Cards["player"];
            if (playerCards.Count == 0) SubStatus = "NeedPlayerCard1";
            await OnDealing();
            return MainStatus == GAME_STATUS.DealingTime && SubStatus == STATUS_DONE;
        }

        protected virtual async Task OnOutputGameResult()
        {
            await Task.Delay(1);
            Logger.Info("OutputGameResult...");
        }

        public virtual async Task<bool> OutputGameResult()
        {
            await OnOutputGameResult();
            return MainStatus == GAME_STATUS.OutputGameResult;
        }

        protected virtual async Task BeforeConfirmation()
        {
            await Task.Delay(1);
        }

        protected virtual async Task AfterConfirmation()
        {
            await Task.Delay(1);
        }

        protected virtual async Task OnConfirmation()
        {
            await Task.Delay(1);
        }

        protected virtual async Task OnEndConfirmation()
        {
            await Task.Delay(1);
        }

        public virtual async Task<bool> ProcessConfirmationTime()
        {
            if (CurrentCountdown > 0)
            {
                Logger.Info("Confirmation time - " + CurrentCountdown);
                await OnConfirmation();
                if (!IsOnHold) CurrentCountdown--;
                return false;
            }

            await OnEndConfirmation();

            return MainStatus == GAME_STATUS.ConfirmationTime;
        }

        protected virtual async Task BeforeEndRoundCleanUp()
        {
            await Task.Delay(1);
        }

        protected virtual async Task AfterEndRoundCleanUp()
        {
            await Task.Delay(1);
        }

        protected virtual async Task OnEndCurrentRound()
        {
            await Task.Delay(1);
        }

        public virtual async Task<bool> EndCurrentRound()
        {
            Logger.Info("EndCurrentRound...");

            IsOnHold = false;

            if (string.IsNullOrEmpty(SubStatus))
            {
                await BeforeEndRoundCleanUp();
                SubStatus = STATUS_ON_GOING;
            }

            if (SubStatus == STATUS_ON_GOING)
            {
                GameResult = "";
                ClearCards();

                await AfterEndRoundCleanUp();
                SubStatus = STATUS_DONE;
            }

            await OnEndCurrentRound();

            return MainStatus == GAME_STATUS.EndCurrentRound && SubStatus == STATUS_DONE;
        }

        protected virtual async Task OnCancelRound()
        {
            await Task.Delay(1);
            Logger.Info("Round is cancelled...");
        }

        public virtual async Task CancelRound()
        {
            if (MainStatus <= GAME_STATUS.NotWorking) return;
            GameResult = "-1";
            SetStatus(GAME_STATUS.EndCurrentRound);
            await OnCancelRound();
        }

        protected virtual dynamic GetExtra()
        {
            return new
            {
                round = RoundNumber
            };
        }

        public virtual dynamic GetSnapshot()
        {
            var cardSets = new Dictionary<string, string[]>();
            foreach (var cardSet in Cards) cardSets.Add(cardSet.Key, cardSet.Value.ToArray());

            dynamic more = GetExtra();
            var game = new
            {
                game = GameCode,
                table = TableCode,
                round = GetRoundId(),
                dealer = GetDealerId(),
                status = GetGameStatus(),
                result = GameResult,
                countdown = CurrentCountdown,
                hold = IsOnHold,
                tail = NeedNewShoe(),
                cards = cardSets,
                extra = more
            };

            return game;
        }

        protected virtual void LoadFromExtra(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            dynamic extra = JsonHelper.ToJsonObject(value);

            decimal realRound = extra.round;
            RoundNumber = Convert.ToInt32(realRound);
        }

        public virtual void LoadFromSnapshot(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            dynamic snapshot = JsonHelper.ToJsonObject(value);

            string gameCode = snapshot.game;
            string tableCode = snapshot.table;
            string roundId = snapshot.round;
            string statusCode = snapshot.status;
            string gameResult = snapshot.result;
            decimal countdown = snapshot.countdown;            
            bool isOnHold = snapshot.hold;
            bool isLastHand = snapshot.tail;

            dynamic extra = snapshot.extra;

            if (string.IsNullOrEmpty(gameCode) || gameCode != GameCode) return;
            
            GameCode = gameCode;
            TableCode = tableCode;

            var roundIdParts = roundId.Split('-');
            ShoeCode = roundIdParts[1];
            RoundNumber = Convert.ToInt32(roundIdParts[2]);

            var statusCodeParts = statusCode.Split('-');
            var statusIndex = GameStatusList.IndexOf(statusCodeParts[0]);
            var subStatus = statusCodeParts.Length >= 2 ? statusCodeParts[1] : "";
            SetStatus((GAME_STATUS)statusIndex, subStatus);

            CurrentCountdown = Convert.ToInt32(countdown);
            GameResult = gameResult;

            IsOnHold = false;
            RequestToClose = false;
            RequestToShuffle = false;
            RequestNewShoe = isLastHand;

            //System.Diagnostics.Debugger.Break();

            ClearCards();
            Cards.Clear();
            string cardsValue = JsonHelper.ToJsonString(snapshot.cards);
            var cardSets = JsonHelper.ToDictionary(cardsValue);
            foreach (var cardSet in cardSets)
            {
                var cardArray = cardSet.Value as IEnumerable<object>;
                if (cardArray != null)
                {
                    var cardList = new List<string>();
                    foreach (var card in cardArray) cardList.Add(card.ToString());
                    Cards.Add(cardSet.Key, cardList);
                }
            }

            if (extra != null) LoadFromExtra(JsonHelper.ToJsonString(extra));
        }
    }

    public class TableGameDealer
    {
        public string DealerId { get; set; } = "";
        public IWebSession Session { get; set; } = null;
        public dynamic Details { get; set; } = null;

        public TableGameDealer(string dealerId, IWebSession session)
        {
            DealerId = dealerId;
            Session = session;
            Details = null;
        }

    }

    public class TableRequest
    {
        public RequestContext Context { get; set; } = null;
        public dynamic Data { get; set; } = null;
        public Func<BaseTableGame, RequestContext, dynamic, Task> Process { get; set; } = null;

        public TableRequest(RequestContext context, dynamic data, Func<BaseTableGame, RequestContext, dynamic, Task> process)
        {
            Context = context;
            Data = data;
            Process = process;
        }

    }
}
