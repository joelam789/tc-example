
import { autoinject } from 'aurelia-framework';
import { EventAggregator } from 'aurelia-event-aggregator';

import { GameState } from './game-state';
import { HttpClient } from './http-client';
import * as Handlers from './handlers';
import * as UI from './ui-messages';

@autoinject()
export class Messenger {

    wsGameServer: any = null;
    isRequesting: boolean = false;

    pendingMessageQueues: Map<string, Array<any>> = new Map<string, Array<any>>();
    pendingMessageQueueSize = 32;

    handlers: Array<Handlers.MessageHandler> = [];

    constructor(public gameState: GameState, public eventChannel: EventAggregator) {
        this.handlers = [
                         new Handlers.LoginHandler(),
                         new Handlers.SnapshotHandler(),
                         new Handlers.ScanCardHandler(),
                         new Handlers.VoidCardHandler(),
                         new Handlers.CancelRoundHandler(),
                         new Handlers.StatusHandler(),
                        ];
    }

    processPendingMessages(pageName: string = null) {
        if (pageName == null) {
            this.pendingMessageQueues.forEach((pendingMessages, page) => {
                while (pendingMessages.length > 0) {
                    let msg = pendingMessages.shift();
                    if (msg != null) this.eventChannel.publish(msg);
                }
            });
        } else {
            let pendingMessages = this.pendingMessageQueues.get(pageName);
            if (pendingMessages == undefined || pendingMessages == null) return;
            else {
                while (pendingMessages.length > 0) {
                    let msg = pendingMessages.shift();
                    if (msg != null) this.eventChannel.publish(msg);
                    //console.log("pushed one msg for " + pageName);
                }
            }
        }
    }

    enqueueMessage(msg: any, pageName: string) {
        if (pageName == undefined || pageName == null || pageName.length <= 0) return;
        let pendingMessages = this.pendingMessageQueues.get(pageName);
        if (pendingMessages == undefined || pendingMessages == null) {
            this.pendingMessageQueues.set(pageName, []);
            pendingMessages = this.pendingMessageQueues.get(pageName);
        }
        pendingMessages.push(msg);
        if (pendingMessages.length > this.pendingMessageQueueSize) {
            while (pendingMessages.length > 0) {
                let msg = pendingMessages.shift();
                if (msg != null) this.eventChannel.publish(msg);
            }
        }
    }

    dispatch(msg: any, pageName: string = null, important: boolean = false) {
        if (pageName != null && pageName.length > 0) {
            if (pageName == this.gameState.currentPage) this.eventChannel.publish(msg);
            else {
                if (important) this.enqueueMessage(msg, pageName);
                else this.eventChannel.publish(msg);
            }
        } else {
            this.eventChannel.publish(msg);
        }
    }

    processServerMessages(msg: any) {

        for (let handler of this.handlers) {
            if (handler.handle(this, msg)) return;
        }

        // print unknown messages
        console.log(JSON.stringify(msg));
        console.log(msg);

    }

    send(msg: any, url: string = "", needWaitForReply: boolean = false) {
        if (needWaitForReply) this.isRequesting = true;
        if (url && url.length > 0) this.wsGameServer.send(url + "/" + JSON.stringify(msg));
        else this.wsGameServer.send(JSON.stringify(msg));
    }

    


    connectGameServer() {

        console.log(this.gameState.serverAddress);

        if (this.gameState.serverAddress == null || this.gameState.serverAddress.length <= 0) return;
        if (this.gameState.gameCode == null || this.gameState.gameCode.length <= 0) return;
        //if (this.gameState.tableCode == null || this.gameState.tableCode.length <= 0) return;
        if (this.gameState.dealerId == null || this.gameState.dealerId.length <= 0) return;

        if (this.wsGameServer != null) {
            this.wsGameServer.close();
            this.wsGameServer = null;
        }

        if (this.wsGameServer == null) {

            console.log("Connecting to game server: " + this.gameState.serverAddress);
            this.isRequesting = true;
            this.wsGameServer = new WebSocket(this.gameState.serverAddress  
                                            + "/" + this.gameState.tableCode 
                                            + "/" + this.gameState.dealerId);
            this.wsGameServer.onopen = () => {
                console.warn("Connected to game server - " + this.gameState.serverAddress);
                //this.dispatch(new UI.LoginSuccess("ok"));
                //this.gameState.startAutoCountdown();
                this.loginGameServer();
            };

            this.wsGameServer.onmessage = (event) => {
                let reply = JSON.parse(event.data);
                this.processServerMessages(reply);
            };

            this.wsGameServer.onclose = () => {
                console.error("Disconnected from server - " + this.gameState.serverAddress);
                //this.gameState.stopAutoCountdown();
            };
        }

    }

    logout() {
        if (this.wsGameServer != null) {
            this.wsGameServer.close();
            this.wsGameServer = null;
        }
    }


    loginGameServer() {
        console.log("loginGameServer - " + this.gameState.gameCode);
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId,
            token: this.gameState.loginToken
        };
        this.send(reqmsg, this.gameState.gameCode + "/login");
    }

    sendShufflingDone() {
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId
        };
        this.send(reqmsg, this.gameState.gameCode + "/shuffling-done");
    }

    sendOpenTable() {
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId
        };
        this.send(reqmsg, this.gameState.gameCode + "/open-table");
    }

    sendCloseTable() {
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId
        };
        this.send(reqmsg, this.gameState.gameCode + "/close-table");
    }

    sendScanCard(card, target) {
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId,
            target: target,
            card: card
        };
        this.send(reqmsg, this.gameState.gameCode + "/scan-card", true);
    }

    sendHoldGame() {
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId
        };
        this.send(reqmsg, this.gameState.gameCode + "/hold-game");
    }

    sendSetLastHand() {
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId
        };
        this.send(reqmsg, this.gameState.gameCode + "/last-hand");
    }

    sendVoidCard() {
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId,
        };
        this.send(reqmsg, this.gameState.gameCode + "/void-card", true);
    }

    sendCancelRound() {
        let reqmsg = {
            table_code: this.gameState.tableCode,
            dealer_id: this.gameState.dealerId,
        };
        this.send(reqmsg, this.gameState.gameCode + "/cancel-round", true);
    }

}
