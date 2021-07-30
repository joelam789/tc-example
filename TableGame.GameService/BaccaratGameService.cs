using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;
using TableGame.Common;

namespace TableGame.GameService
{
    [Access(Name = "baccarat")]
    public class BaccaratGameService
    {
        protected BaccaratGame m_Game = null;
        protected string m_GameCode = "";

        protected virtual BaccaratGame CreateGame(IServerNode node)
        {
            return new BaccaratGame(node);
        }

        public dynamic GetRequest(RequestContext ctx)
        {
            dynamic req = null;
            string reqstr = ctx.Data.ToString();
            try
            {
                if (reqstr.Trim().Length > 0) req = ctx.JsonHelper.ToJsonObject(reqstr);
            }
            catch { }
            return req;
        }

        public async Task SendSimpleError(RequestContext ctx, string msg, string error = "Invalid request")
        {
            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                msg = msg,
                error_code = -1,
                error_message = error
            }));
        }

        [Access(Name = "on-load", IsLocal = true)]
        public virtual async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            if (m_Game == null) m_Game = CreateGame(node);

            //node.GetLogger().Info(m_Game.GetFirstShoeNumber().ToString());

            node.GetLogger().Info(this.GetType().Name + " is loading settings from config...");

            try
            {
                ConfigurationManager.RefreshSection("appSettings");
                await Task.Delay(100);
                var keys = ConfigurationManager.AppSettings.Keys;
                foreach (var key in keys)
                {
                    if (key.ToString() == "TableCode")
                    {
                        m_Game.TableCode = ConfigurationManager.AppSettings["TableCode"].ToString();
                        continue;
                    }
                    if (key.ToString() == "GameCode")
                    {
                        m_GameCode = ConfigurationManager.AppSettings["GameCode"].ToString();
                        continue;
                    }
                    if (key.ToString() == "FeedServerUrl")
                    {
                        //m_Game.FeedServerUrl = ConfigurationManager.AppSettings["FeedServerUrl"].ToString();
                        continue;
                    }
                    if (key.ToString() == "BettingTime")
                    {
                        m_Game.TotalBettingCountdown = Convert.ToInt32(ConfigurationManager.AppSettings["BettingTime"].ToString());
                        continue;
                    }
                    if (key.ToString() == "ConfirmationTime")
                    {
                        m_Game.TotalConfirmationCountdown = Convert.ToInt32(ConfigurationManager.AppSettings["ConfirmationTime"].ToString());
                        continue;
                    }
                    if (key.ToString() == "DBName")
                    {
                        m_Game.DBName = ConfigurationManager.AppSettings["DBName"].ToString();
                        continue;
                    }
                }

                if (m_Game.GameCode == m_GameCode)
                {
                    node.GetLogger().Info("Game Code: " + m_Game.GameCode);
                    node.GetLogger().Info("Table Code: " + m_Game.TableCode);
                    node.GetLogger().Info("Betting Time: " + m_Game.TotalBettingCountdown);
                    //node.GetLogger().Info("Feed Server Url: " + m_Game.FeedServerUrl);
                    node.GetLogger().Info("Database Name: " + m_Game.DBName);
                    node.GetLogger().Info("Done");
                }
                
            }
            catch (Exception ex)
            {
                node.GetLogger().Error("Failed to load settings from config for GameServerService: ");
                node.GetLogger().Error(ex.ToString());
            }

            if (m_Game != null && m_Game.GameCode == m_GameCode)
            {
                await m_Game.Start();
                node.GetLogger().Info(this.GetType().Name + " started");
            }

            return "";
        }

        [Access(Name = "on-unload", IsLocal = true)]
        public virtual async Task<string> Unload(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            if (m_Game != null && m_Game.GameCode == m_GameCode)
            {
                await m_Game.Stop();
                m_Game = null;
            }
            await Task.Delay(100);

            return "";
        }

        [Access(Name = "login")]
        public virtual async Task DealerLogin(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode)
            {
                await SendSimpleError(ctx, "login", "Service not available");
                return;
            }

            dynamic req = GetRequest(ctx);
            if (req == null)
            {
                await SendSimpleError(ctx, "login", "Invalid request");
                return;
            }

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                string dealerId = data.dealer_id;
                var dealer = new TableGameDealer(dealerId, context.Session);
                game.SetDealer(dealer);
                if (game.Dealer != null)
                {
                    await context.Session.Send(context.JsonHelper.ToJsonString(new
                    {
                        msg = "login",
                        snapshot = game.GetSnapshot(),
                        error_code = 0,
                        error_message = "ok"
                    }));
                }
            };

            m_Game.AddRequest(ctx, req, process);
        }

        [Access(Name = "open-table")]
        public virtual async Task OpenTable(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode) return;

            dynamic req = GetRequest(ctx);
            if (req == null) return;

            await Task.Delay(50);

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                if (game.MainStatus == GAME_STATUS.NotWorking && game.Dealer != null)
                {
                    await game.OpenTable();
                }
            };

            m_Game.AddRequest(ctx, req, process);
        }

        [Access(Name = "close-table")]
        public virtual async Task CloseTable(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode) return;

            dynamic req = GetRequest(ctx);
            if (req == null) return;

            await Task.Delay(50);

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                if (game.MainStatus == GAME_STATUS.PreparingTime
                    && game.SubStatus == "ShuffleCards")
                {
                    game.RequestToClose = true;
                    await Task.Delay(50);
                }
            };

            m_Game.AddRequest(ctx, req, process);
        }

        [Access(Name = "cancel-round")]
        public virtual async Task CancelRound(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode)
            {
                await SendSimpleError(ctx, "cancel-round", "Service not available");
                return;
            }

            dynamic req = GetRequest(ctx);
            if (req == null)
            {
                await SendSimpleError(ctx, "cancel-round", "Invalid request");
                return;
            }

            await Task.Delay(50);

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                if (game.MainStatus > GAME_STATUS.NotWorking && game.Dealer != null)
                {
                    await game.CancelRound();
                    await context.Session.Send(context.JsonHelper.ToJsonString(new
                    {
                        msg = "cancel-round",
                        status = game.GetGameStatus(),
                        result = game.GameResult,
                        error_code = 0,
                        error_message = "ok"
                    }));
                }
                else
                {
                    await context.Session.Send(context.JsonHelper.ToJsonString(new
                    {
                        msg = "cancel-round",
                        error_code = -1,
                        error_message = "Not allowed to cancel round"
                    }));
                }
            };

            m_Game.AddRequest(ctx, req, process);
        }

        [Access(Name = "shuffling-done")]
        public virtual async Task ShufflingDone(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode) return;

            dynamic req = GetRequest(ctx);
            if (req == null) return;

            await Task.Delay(100);

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                game.SetSubStatus("ShufflingDone");
                await context.Session.Send(context.JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = game.GetGameStatus(),
                }));
            };

            m_Game.AddRequest(ctx, req, process);
        }

        [Access(Name = "last-hand")]
        public virtual async Task LastHand(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode) return;

            dynamic req = GetRequest(ctx);
            if (req == null) return;

            await Task.Delay(100);

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                game.RequestNewShoe = true;
                await context.Session.Send(context.JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = game.GetGameStatus(),
                    tail = game.NeedNewShoe(),
                }));
            };

            m_Game.AddRequest(ctx, req, process);
        }

        [Access(Name = "hold-game")]
        public virtual async Task HoldGame(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode) return;

            dynamic req = GetRequest(ctx);
            if (req == null) return;

            await Task.Delay(100);

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                game.IsOnHold = !game.IsOnHold;
                await context.Session.Send(context.JsonHelper.ToJsonString(new
                {
                    msg = "status",
                    status = game.GetGameStatus(),
                    hold = game.IsOnHold,
                }));
            };

            m_Game.AddRequest(ctx, req, process);
        }

        protected virtual void ProcessScanCardRequest(BaseTableGame tableGame, RequestContext reqCtx, string card, string target)
        {
            var reqData = new
            {
                card = card,
                target = target
            };

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                string reqCard = data.card;
                string reqTarget = data.target;

                var playerCards = game.Cards["player"];
                var bankerCards = game.Cards["banker"];

                if (game.SubStatus == "NeedPlayerCard1" && playerCards.Count == 0)
                {
                    await game.AddNewCard(reqCard, reqTarget, "NeedBankerCard1");
                }
                else if (game.SubStatus == "NeedBankerCard1" && bankerCards.Count == 0)
                {
                    await game.AddNewCard(reqCard, reqTarget, "NeedPlayerCard2");
                }
                else if (game.SubStatus == "NeedPlayerCard2" && playerCards.Count == 1)
                {
                    await game.AddNewCard(reqCard, reqTarget, "NeedBankerCard2");
                }
                else if (game.SubStatus == "NeedBankerCard2" && bankerCards.Count == 1)
                {
                    await game.AddNewCard(reqCard, reqTarget, "CheckFirst4Cards");
                }
                else if (game.SubStatus == "NeedPlayerCard3" && playerCards.Count == 2)
                {
                    await game.AddNewCard(reqCard, reqTarget, "CheckBankerCard3");
                }
                else if (game.SubStatus == "NeedBankerCard3" && bankerCards.Count == 2)
                {
                    await game.AddNewCard(reqCard, reqTarget, "Done");
                }

                await context.Session.Send(context.JsonHelper.ToJsonString(new
                {
                    msg = "scan-card",
                    status = game.GetGameStatus(),
                    cards = new
                    {
                        player = game.Cards["player"].ToArray(),
                        banker = game.Cards["banker"].ToArray()
                    },
                    error_code = 0,
                    error_message = "ok"
                }));
            };

            tableGame.AddRequest(reqCtx, reqData, process);
        }

        [Access(Name = "scan-card")]
        public virtual async Task ScanCard(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode)
            {
                await SendSimpleError(ctx, "scan-card", "Service not available");
                return;
            }

            dynamic req = GetRequest(ctx);
            if (req == null)
            {
                await SendSimpleError(ctx, "scan-card", "Invalid request");
                return;
            }

            string card = req.card;
            string target = req.target;
            if (target == null) target = "";

            //ctx.Logger.Info("Target = " + target);
            //ctx.Logger.Info("SubState = " + m_Game.SubStatus);

            string expectedSubStatus = "Need" + target;
            if (m_Game.SubStatus == expectedSubStatus) target = target.Trim();
            else target = "";

            if (m_Game.IsValidCard(card)) card = card.Substring(0, 2).ToUpper();
            else card = "";

            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(card))
            {
                await SendSimpleError(ctx, "scan-card", "Invalid request");
                return;
            }

            ProcessScanCardRequest(m_Game, ctx, card, target);
        }

        protected virtual void ProcessVoidCardRequest(BaseTableGame tableGame, RequestContext reqCtx, string reqData)
        {
            dynamic req = tableGame.JsonHelper.ToJsonObject(reqData);

            Func<BaseTableGame, RequestContext, dynamic, Task> process = async (game, context, data) =>
            {
                var playerCards = game.Cards["player"];
                var bankerCards = game.Cards["banker"];

                if (bankerCards.Count == 3)
                {
                    await game.RemoveLastCard("banker", "NeedBankerCard3");
                }
                else if (playerCards.Count == 3)
                {
                    await game.RemoveLastCard("player", "NeedPlayerCard3");
                }
                else if (bankerCards.Count == 2)
                {
                    await game.RemoveLastCard("banker", "NeedBankerCard2");
                }
                else if (playerCards.Count == 2)
                {
                    await game.RemoveLastCard("player", "NeedPlayerCard2");
                }
                else if (bankerCards.Count == 1)
                {
                    await game.RemoveLastCard("banker", "NeedBankerCard1");
                }
                else if (playerCards.Count == 1)
                {
                    await game.RemoveLastCard("player", "NeedPlayerCard1");
                }

                await context.Session.Send(context.JsonHelper.ToJsonString(new
                {
                    msg = "void-card",
                    status = game.GetGameStatus(),
                    cards = new
                    {
                        player = game.Cards["player"].ToArray(),
                        banker = game.Cards["banker"].ToArray()
                    },
                    error_code = 0,
                    error_message = "ok"
                }));
            };

            tableGame.AddRequest(reqCtx, req, process);
        }

        [Access(Name = "void-card")]
        public virtual async Task VoidCard(RequestContext ctx)
        {
            if (m_Game == null || m_Game.GameCode != m_GameCode)
            {
                await SendSimpleError(ctx, "void-card", "Service not available");
                return;
            }

            dynamic req = GetRequest(ctx);
            if (req == null)
            {
                await SendSimpleError(ctx, "void-card", "Invalid request");
                return;
            }

            if (!m_Game.IsOnHold
                || (m_Game.MainStatus != GAME_STATUS.DealingTime
                    && m_Game.MainStatus != GAME_STATUS.ConfirmationTime))
            {
                await SendSimpleError(ctx, "void-card", "Invalid request");
                return;
            }

            ProcessVoidCardRequest(m_Game, ctx, ctx.JsonHelper.ToJsonString(req));
        }
    }
}
