
import {autoinject} from 'aurelia-framework';
import {EventAggregator, Subscription} from 'aurelia-event-aggregator';
import {Router} from 'aurelia-router';

import {App} from './app';
import {GameState} from './game-state';
import {Messenger} from './messenger';
import * as UI from './ui-messages';

@autoinject()
export class LoginPage {

    gameCode: string = "baccarat";
    tableCode: string = "b1";
    dealerId: string = "d1";
    loginToken: string = "";
    serverAddress: string = "127.0.0.1:9990";
    alertMessage: string = null;

    subscribers: Array<Subscription> = [];

    constructor(public router: Router, public gameState: GameState, 
                public messenger: Messenger, public eventChannel: EventAggregator) {
                    
        this.gameCode = App.config.defaultGame;
        this.tableCode = App.config.defaultTable;
        this.dealerId = App.config.defaultDealer;
        this.serverAddress = App.config.serverAddress;

    }

    attached() {
        this.subscribers = [];
        this.subscribers.push(this.eventChannel.subscribe(UI.LoginError, data => this.alertMessage = data.message));
        this.subscribers.push(this.eventChannel.subscribe(UI.LoginSuccess, data => this.router.navigate("game-table")));
    }

    detached() {
        for (let item of this.subscribers) item.dispose();
        this.subscribers = [];
    }

    activate(parameters, routeConfig) {
        console.log("done");
        this.gameState.currentPage = "login";
        App.containers['loading'].style.display = 'none';
        App.containers['app'].style.display = 'block';
    }

    get canLogin() {
        return this.gameCode.length > 0 
                //&& this.tableCode.length > 0 
                && this.dealerId.length > 0
                && this.loginToken.length > 0
                && this.serverAddress.length > 0
                && !this.messenger.isRequesting
                && !this.router.isNavigating;
    }

    get isEmptyAlertMessage() {
        return this.alertMessage == null || this.alertMessage.length <= 0;
    }

    dismissAlertMessage() {
        this.alertMessage = null;
    }

    connectAndLogin() {
        
        console.log("start to connect server and then try to login");
        
        this.gameState.dealerId = this.dealerId;
        this.gameState.loginToken = this.loginToken;
        this.gameState.gameCode = this.gameCode;
        this.gameState.tableCode = this.tableCode;
        this.gameState.serverAddress = this.serverAddress;

        this.messenger.connectGameServer();
        
    }

    

}
