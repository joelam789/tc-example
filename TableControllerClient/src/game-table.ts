
import {autoinject, customElement} from 'aurelia-framework';
import {EventAggregator, Subscription} from 'aurelia-event-aggregator';
import {Router} from 'aurelia-router';

import {DialogService} from 'aurelia-dialog';
import {I18N} from 'aurelia-i18n';

import {GameState} from './game-state';
import {Messenger} from './messenger';
import * as UI from './ui-messages';

@autoinject()
export class GameTablePage {

    dealerId: string = "";
    gameCode: string = "";
    tableCode: string = "";
    
    gameRound = "";
    gameStatus = "";
    gameHold = false;
    gameTail = false;

    gameRobot = false;

    headerCards: Array<string> = [];
    playerCards: Array<string> = [];
    bankerCards: Array<string> = [];

    playerScore: number = 0;
    bankerScore: number = 0;

    playerWinMsg: string = "";
    bankerWinMsg: string = "";

    logData: string = "";
    logLines: Array<string> = [];

    //canGoAfterShuffling = false;
    canConfirmResult = false;
    //canSetLastHand = false;
    //canHoldGame = false;
    //canCancelRound = false;
    //canVoidCard = false;
    //canScanCard = false;

    alertMessage: string = null;

    robotTimer: any = null;

    subscribers: Array<Subscription> = [];

    //pressKeyCallback = (event) => {
    //    if (event.which == 13 || event.keyCode == 13) {
    //        if (this.canSendChatMessage) this.sendChatMessage();
    //        return false;
    //    }
    //    return true;
    //};

    nowToString(): string {
        let now = new Date();
        let h = now.getHours();
        let m = now.getMinutes();
        let s = now.getSeconds();
        return  (h > 9 ? h.toString() : "0" + h.toString()) 
        + ":" + (m > 9 ? m.toString() : "0" + m.toString()) 
        + ":" + (s > 9 ? s.toString() : "0" + s.toString());
    }

    constructor(public dialogService: DialogService, public router: Router, 
                public i18n: I18N, public gameState: GameState,
                public messenger: Messenger, public eventChannel: EventAggregator) {

        this.subscribers = [];
        
    }

    activate(parameters, routeConfig) {

        this.dealerId = this.gameState.dealerId;
        this.gameCode = this.gameState.gameCode;
        this.tableCode = this.gameState.tableCode;
        
        this.changeLang(this.gameState.currentLang);

        this.updateTableInfo();

        if (this.robotTimer != null) {
            clearInterval(this.robotTimer);
            this.robotTimer = null;
        }
        this.robotTimer = setInterval(() => {
            if (this.gameRobot) {
                if (this.canOpenTable) {
                    this.sendOpenTable();
                    return;
                }
                if (this.canGoAfterShuffling) {
                    this.sendShufflingDone();
                    return;
                }
                if (this.canScanCard) {
                    this.sendScanCard();
                    return;
                }
            }
        }, 3000);

        //window.addEventListener('keypress', this.pressKeyCallback, false);
        
    }

    deactivate() {
        if (this.robotTimer != null) {
            clearInterval(this.robotTimer);
            this.robotTimer = null;
        }
        //window.removeEventListener('keypress', this.pressKeyCallback);
    }

    attached() {

        this.subscribers = [];

        this.subscribers.push(this.eventChannel.subscribe(UI.LogMessage, data => {
            //console.log("ui log: " + data.content);
            let line = data.content.message;
            this.addLog(line);
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.ShufflingRequest, data => {
            //console.log(data);
            let status: string = data.content.status;
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.GameStatus, data => {
            //console.log(data);
            let status: string = data.content.status;
            let countdown: number = data.content.countdown;

            if (data.content.result != undefined && data.content.result.length > 0) 
                console.log(data.content.result);

            this.updateTableInfo();

            let line = countdown && countdown > 0 ? (status + " (" + countdown + ")") : status;
            this.addLog(line);

        }));

        this.gameState.currentPage = "game-table";
        this.messenger.processPendingMessages("game-table");

    }

    detached() {
        for (let item of this.subscribers) item.dispose();
        this.subscribers = [];
    }

    changeLang(lang: string) {
        this.i18n.setLocale(lang)
        .then(() => {
            this.gameState.currentLang = this.i18n.getLocale();
            console.log(this.gameState.currentLang);
        });
    }

    addLog(msg) {
        this.logLines.push("[" + this.nowToString() + "] " + msg);
        if (this.logLines.length > 1000) this.logLines.splice(0, 500);
        this.logData = this.logLines.join("\n");
        setTimeout(() => {
            let logger = document.getElementById('main_logger');
            logger.scrollTop = logger.scrollHeight;
        }, 500);
    }

    get isEmptyAlertMessage() {
        return this.alertMessage == null || this.alertMessage.length <= 0;
    }

    get canGoAfterShuffling() {
        return !this.messenger.isRequesting && !this.router.isNavigating
        && (this.gameStatus.indexOf("PreparingTime") >= 0 
            && this.gameStatus.indexOf("ShuffleCards") >= 0)  
        ;
    }

    get canScanCard() {
        return !this.messenger.isRequesting && !this.router.isNavigating
        && this.gameStatus.indexOf("Deal") >= 0
        && this.gameStatus.indexOf("Need") >= 0 
        && this.gameStatus.indexOf("Card") >= 0;
    }

    get canSetLastHand() {
        return !this.messenger.isRequesting && !this.router.isNavigating
        && (this.gameStatus.indexOf("BettingTime") >= 0 
            || this.gameStatus.indexOf("DealingTime") >= 0
            || this.gameStatus.indexOf("ConfirmationTime") >= 0)  
        ;
    }

    get canHoldGame() {
        return !this.messenger.isRequesting && !this.router.isNavigating
        && (this.gameStatus.indexOf("BettingTime") >= 0 
            || this.gameStatus.indexOf("DealingTime") >= 0
            || this.gameStatus.indexOf("ConfirmationTime") >= 0)  
        ;
    }

    get canOpenTable() {
        return !this.messenger.isRequesting && !this.router.isNavigating
        && this.gameStatus.indexOf("NotWorking") >= 0 
        ;
    }

    get canCloseTable() {
        return !this.messenger.isRequesting && !this.router.isNavigating
        && (this.gameStatus.indexOf("PreparingTime") >= 0 
            && this.gameStatus.indexOf("ShuffleCards") >= 0)  
        ;
    }

    get canVoidCard() {
        return !this.messenger.isRequesting && !this.router.isNavigating
        && this.gameHold
        //&& this.gameCode != 'andar-bahar'
        && (this.gameStatus.indexOf("DealingTime") >= 0
            || this.gameStatus.indexOf("ConfirmationTime") >= 0)  
        ;
    }

    get canCancelRound() {
        return !this.messenger.isRequesting && !this.router.isNavigating
        && this.gameHold
        && (this.gameStatus.indexOf("BettingTime") >= 0
            || this.gameStatus.indexOf("DealingTime") >= 0
            || this.gameStatus.indexOf("ConfirmationTime") >= 0)  
        ;
    }

    dismissAlertMessage() {
        this.alertMessage = null;
    }

    updateTableInfo() {
        if (this.gameState && this.gameState.gameSnapshot) {

            this.gameStatus = this.gameState.gameSnapshot.status;

            this.gameRound = this.gameState.gameSnapshot.round;
            this.gameHold = this.gameState.gameSnapshot.hold;
            this.gameTail = this.gameState.gameSnapshot.tail;

            this.headerCards = this.gameState.gameSnapshot.cards.header;
            this.playerCards = this.gameState.gameSnapshot.cards.player;
            this.bankerCards = this.gameState.gameSnapshot.cards.banker;

            this.playerScore = this.playerCards.length > 0 ? this.gameState.gameSnapshot.scores.player : 0;
            this.bankerScore = this.bankerCards.length > 0 ? this.gameState.gameSnapshot.scores.banker : 0;

            if (this.gameStatus.indexOf("ConfirmationTime") >= 0) {
                if (this.playerScore > this.bankerScore) {
                    this.playerWinMsg = "WIN";
                    this.bankerWinMsg = "";
                } else if (this.playerScore < this.bankerScore) {
                    this.playerWinMsg = "";
                    this.bankerWinMsg = "WIN";
                } else {
                    this.playerWinMsg = "TIE";
                    this.bankerWinMsg = "TIE";
                }
            } else {
                this.playerWinMsg = "";
                this.bankerWinMsg = "";
            }

            //console.log(this.playerCards);
        }
        
    }


    logout() {
        this.messenger.logout();
        this.router.navigate("login"); // go to login
    }

    sendShufflingDone() {
        //this.canGoAfterShuffling = false;
        this.messenger.sendShufflingDone();
    }

    sendScanCard() {

        let target = "";

        if (this.gameStatus.indexOf("Need") >= 0 && this.gameStatus.indexOf("Card") >= 0) {
            let idx = this.gameStatus.indexOf("Need");
            target = this.gameStatus.substring(idx + 4);
        }

        if (target.length <= 0) return;

        let arr1 = ['A', '2', '3', '4', '5', '6', '7', '8', '9', 'T', 'J', 'Q', 'K'];
        let arr2 = ['C', 'D', 'H', 'S'];

        let card = arr1[Math.floor(Math.random() * arr1.length)] + arr2[Math.floor(Math.random() * arr2.length)];
        //let card = arr1[0] + arr2[Math.floor(Math.random() * arr2.length)];

        console.log("sending card: " + card);

        this.messenger.sendScanCard(card, target);
        
    }

    sendHoldGame() {
        this.messenger.sendHoldGame();
    }

    sendOpenTable() {
        this.messenger.sendOpenTable();
    }

    sendCloseTable() {
        this.messenger.sendCloseTable();
    }

    sendSetLastHand() {
        this.messenger.sendSetLastHand();
    }

    sendVoidCard() {
        this.messenger.sendVoidCard();
    }

    sendCancelRound() {
        this.messenger.sendCancelRound();
    }

    
}
