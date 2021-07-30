
export class GameCard {
    suit: number = -1; // CARD_SUIT_SPADE = 0, CARD_SUIT_HEART, CARD_SUIT_CLUB, CARD_SUIT_DIAMOND
    value: number = -1; // CARD_ACE = 0, CARD_2, CARD_3, ... , CARD_10, CARD_JACK, CARD_QUEEN, CARD_KING
    open: boolean = false;
}

export class GameState {

    // some global ui info
    currentPage: string = "";
    currentLang: string = "en";

    dealerId: string = "";
    gameCode: string = "";
    tableCode: string = "";
    loginToken: string = "";
    serverAddress: string = "";

    gameSnapshot: any = null;
    
    countdownTimer: any = null;

    startAutoCountdown() {
        if (this.countdownTimer != null) {
            clearInterval(this.countdownTimer);
            this.countdownTimer = null;
        }
        this.countdownTimer = setInterval(() => {
            // ...
        }, 1000);
    }

    stopAutoCountdown() {
        if (this.countdownTimer != null) {
            clearInterval(this.countdownTimer);
            this.countdownTimer = null;
        }
    }

    static getCardCode(card: GameCard = null): string {
        if (card != null && card.suit >= 0 && card.value >= 0) {
            let cardNamePart1Chars = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'T', 'J', 'Q', 'K'];
            let cardNamePart2Chars = ['S', 'H', 'C', 'D'];
            if (card.suit < cardNamePart2Chars.length && card.value < cardNamePart1Chars.length) {
                return cardNamePart1Chars[card.value] + cardNamePart2Chars[card.suit];
            }
        }
        return "01";
    }

    static getCardByCode(code: string = null): GameCard {
        if (code == undefined || code == null || code.length <= 1) return new GameCard();
        let cardNamePart1Chars = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'T', 'J', 'Q', 'K'];
        let cardNamePart2Chars = ['S', 'H', 'C', 'D'];
        let card = new GameCard();
        card.open = true;
        card.suit = cardNamePart2Chars.indexOf(code.charAt(1));
        card.value = cardNamePart1Chars.indexOf(code.charAt(0));
        return card;
    }

}
