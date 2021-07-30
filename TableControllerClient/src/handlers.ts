
import {Messenger} from './messenger';
import * as UI from './ui-messages';

export interface MessageHandler {
    handle(messenger: Messenger, msg: any): boolean;
}

export class LoginHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "login") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                messenger.gameState.gameSnapshot = msg.snapshot;
                messenger.gameState.tableCode = messenger.gameState.gameSnapshot.table;
                messenger.dispatch(new UI.LoginSuccess(msg.error_message));
            } else {
                console.log("Failed to login game server");
                console.log(msg.error_message);
                messenger.dispatch(new UI.LoginError(msg.error_message));
            }
            return true;
        }
        return false;
    }
}

export class SnapshotHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "snapshot") {
            messenger.gameState.gameSnapshot = msg.snapshot;
            //messenger.dispatch(new UI.ShufflingRequest(msg), "game-table", true);
            messenger.dispatch(new UI.GameStatus(msg.snapshot));
            return true;
        }
        return false;
    }
}


export class ScanCardHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "scan-card") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                messenger.gameState.gameSnapshot.status = msg.status;
                if (msg.countdown != undefined) messenger.gameState.gameSnapshot.countdown = msg.countdown;
                if (msg.round != undefined) messenger.gameState.gameSnapshot.round = msg.round;
                if (msg.result != undefined) messenger.gameState.gameSnapshot.result = msg.result;
                if (msg.hold != undefined) messenger.gameState.gameSnapshot.hold = msg.hold;
                if (msg.tail != undefined) messenger.gameState.gameSnapshot.tail = msg.tail;
                if (msg.cards != undefined) messenger.gameState.gameSnapshot.cards = msg.cards;
                messenger.dispatch(new UI.GameStatus(msg));
            } else {
                console.log("Failed to send scan-card message");
                console.log(msg.error_message);
                //messenger.dispatch(new UI.LoginError(msg.error_message));
            }
            
            return true;
        }
        return false;
    }
}

export class VoidCardHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "void-card") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                messenger.gameState.gameSnapshot.status = msg.status;
                if (msg.countdown != undefined) messenger.gameState.gameSnapshot.countdown = msg.countdown;
                if (msg.round != undefined) messenger.gameState.gameSnapshot.round = msg.round;
                if (msg.result != undefined) messenger.gameState.gameSnapshot.result = msg.result;
                if (msg.hold != undefined) messenger.gameState.gameSnapshot.hold = msg.hold;
                if (msg.tail != undefined) messenger.gameState.gameSnapshot.tail = msg.tail;
                if (msg.cards != undefined) messenger.gameState.gameSnapshot.cards = msg.cards;
                messenger.dispatch(new UI.GameStatus(msg));
            } else {
                console.log("Failed to send void-card message");
                console.log(msg.error_message);
                //messenger.dispatch(new UI.LoginError(msg.error_message));
            }
            
            return true;
        }
        return false;
    }
}

export class StatusHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "status") {
            messenger.gameState.gameSnapshot.status = msg.status;
            if (msg.countdown != undefined) messenger.gameState.gameSnapshot.countdown = msg.countdown;
            if (msg.round != undefined) messenger.gameState.gameSnapshot.round = msg.round;
            if (msg.result != undefined) messenger.gameState.gameSnapshot.result = msg.result;
            if (msg.hold != undefined) messenger.gameState.gameSnapshot.hold = msg.hold;
            if (msg.tail != undefined) messenger.gameState.gameSnapshot.tail = msg.tail;
            if (msg.cards != undefined) messenger.gameState.gameSnapshot.cards = msg.cards;
            messenger.dispatch(new UI.GameStatus(msg));
            return true;
        }
        return false;
    }
}

export class CancelRoundHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "cancel-round") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                messenger.gameState.gameSnapshot.status = msg.status;
                if (msg.result != undefined) messenger.gameState.gameSnapshot.result = msg.result;
                messenger.dispatch(new UI.GameStatus(msg));
            } else {
                console.log("Failed to send void-card message");
                console.log(msg.error_message);
                //messenger.dispatch(new UI.LoginError(msg.error_message));
            }
            
            return true;
        }
        return false;
    }
}

