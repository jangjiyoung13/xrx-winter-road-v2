const express = require('express');
const compression = require('compression');
const path = require('path');
const cors = require('cors');
const WebSocket = require('ws');
const http = require('http');
const { v4: uuidv4 } = require('uuid');
const fs = require('fs');
const QRCode = require('qrcode');
const osc = require('osc');
const https = require('https');
const crypto = require('crypto');

// 설정 파일 로드
const config = JSON.parse(fs.readFileSync('./config.json', 'utf8'));

// ========================================
// Dynu DDNS 자동 IP 업데이트
// ========================================
let lastKnownIP = null;

function getPublicIP() {
    return new Promise((resolve, reject) => {
        https.get('https://api.ipify.org', (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => resolve(data.trim()));
        }).on('error', reject);
    });
}

async function updateDynuDDNS() {
    if (!config.ddns || !config.ddns.enabled) {
        return;
    }

    try {
        const currentIP = await getPublicIP();

        if (currentIP === lastKnownIP) {
            return;
        }

        console.log(`Public IP changed: ${lastKnownIP || '(first check)'} → ${currentIP}`);

        const md5Password = crypto.createHash('md5')
            .update(config.ddns.password)
            .digest('hex');

        const url = `https://api.dynu.com/nic/update?hostname=${config.ddns.hostname}&myip=${currentIP}&password=${md5Password}`;

        https.get(url, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                console.log(`Dynu DDNS updated: ${data.trim()} (IP: ${currentIP})`);
                lastKnownIP = currentIP;
            });
        }).on('error', (err) => {
            console.error(`Dynu DDNS update failed:`, err.message);
        });
    } catch (error) {
        console.error(`Failed to get public IP:`, error.message);
    }
}

function initDDNS() {
    if (!config.ddns || !config.ddns.enabled) {
        console.log(`DDNS auto-update is disabled (dev mode)`);
        return;
    }

    const intervalMs = (config.ddns.updateIntervalMinutes || 5) * 60 * 1000;
    console.log(`DDNS auto-update enabled for ${config.ddns.hostname} (every ${config.ddns.updateIntervalMinutes || 5} min)`);

    // 서버 시작 시 즉시 업데이트
    updateDynuDDNS();
    // 주기적 업데이트
    setInterval(updateDynuDDNS, intervalMs);
}

// DDNS 초기화
initDDNS();

// ========================================
// TD 서버 OSC 통신 설정
// ========================================
const TD_SERVER = {
    host: config.osc.host,
    port: config.osc.port
};

let oscUDPPort = null;

function initTDServerConnection() {
    if (!config.osc.enabled) {
        console.log(`OSC communication is disabled in config.json`);
        return;
    }

    try {
        oscUDPPort = new osc.UDPPort({
            localAddress: '0.0.0.0',
            localPort: 0,
            remoteAddress: TD_SERVER.host,
            remotePort: TD_SERVER.port,
            metadata: true
        });

        oscUDPPort.on('ready', () => {
            console.log(`TD Server OSC connection ready (${TD_SERVER.host}:${TD_SERVER.port})`);
            // 서버 시작 시 TD 상태를 idle로 동기화 (방들의 tdState 초기값과 일치시킴)
            console.log(`Syncing TD to idle state on server startup`);
            sendOSCtoTD('/goto_preset');
        });

        oscUDPPort.on('error', (error) => {
            console.error(`TD Server OSC error:`, error);
        });

        oscUDPPort.open();
        console.log(`Initializing TD Server OSC connection...`);
    } catch (error) {
        console.error(`Failed to initialize TD Server OSC:`, error);
    }
}

function sendOSCtoTD(address, args = []) {
    if (!oscUDPPort) {
        console.error(`OSC port not initialized.`);
        return false;
    }

    try {
        oscUDPPort.send({ address, args });
        console.log(`[OSC → TD] Sent: ${address}`, args.length > 0 ? args : '');
        return true;
    } catch (error) {
        console.error(`Failed to send OSC message:`, error);
        return false;
    }
}

function triggerWinterRoadStart() {
    console.log(`[TD Trigger] Winter Road Start - goto_live`);
    return sendOSCtoTD('/goto_live');
}

function triggerWinterRoadEnd() {
    console.log(`[TD Trigger] Winter Road End - goto_preset`);
    return sendOSCtoTD('/goto_preset');
}

// ========================================
// Express 서버 설정
// ========================================
const app = express();
const server = http.createServer(app);
const wss = new WebSocket.Server({
    server,
    perMessageDeflate: false,
    maxPayload: 1024 * 1024,
    clientTracking: true
});

// ========================================
// Heartbeat: TCP silent death 탐지
// ========================================
// 30초마다 프로토콜 레벨 PING 전송. 다음 사이클까지 PONG 응답 없으면 소켓 강제 종료.
// terminate() → 'close' 이벤트 발생 → 기존 markDisconnected 로직이 grace/cleanup 수행.
// 브라우저/WebSocketSharp 라이브러리가 PONG을 자동 처리하므로 클라이언트 변경 불필요.
const HEARTBEAT_INTERVAL_MS = 30000;
const heartbeatTimer = setInterval(() => {
    wss.clients.forEach((ws) => {
        if (ws.isAlive === false) {
            console.log(`Heartbeat timeout — terminating unresponsive client`);
            try { ws.terminate(); } catch (e) { /* ignore */ }
            return;
        }
        ws.isAlive = false;
        try { ws.ping(); } catch (e) { /* ignore — 소켓 상태 이상 시 다음 사이클에서 terminate됨 */ }
    });
}, HEARTBEAT_INTERVAL_MS);

wss.on('close', () => {
    clearInterval(heartbeatTimer);
    console.log(`Heartbeat timer stopped (server shutdown)`);
});

const PORT = process.env.PORT || config.server.port;

app.use(cors());
app.use(express.json());

// Unity WebGL 빌드 정적 파일 서빙
app.use('/unity', express.static(path.join(__dirname, 'UnityWebGLBuild'), {
    setHeaders: (res, filePath) => {
        if (filePath.endsWith('.gz')) {
            res.setHeader('Content-Encoding', 'gzip');
            res.setHeader('Content-Type', getContentType(filePath.replace('.gz', '')));
        } else if (filePath.endsWith('.br')) {
            res.setHeader('Content-Encoding', 'br');
            res.setHeader('Content-Type', getContentType(filePath.replace('.br', '')));
        } else if (filePath.endsWith('.wasm')) {
            res.setHeader('Content-Type', 'application/wasm');
        } else {
            const contentType = getContentType(filePath);
            if (contentType !== 'application/octet-stream') {
                res.setHeader('Content-Type', contentType);
            }
        }
    }
}));

function getContentType(filePath) {
    const ext = path.extname(filePath).toLowerCase();
    const mimeTypes = {
        '.js': 'application/javascript',
        '.css': 'text/css',
        '.html': 'text/html',
        '.json': 'application/json',
        '.png': 'image/png',
        '.jpg': 'image/jpeg',
        '.jpeg': 'image/jpeg',
        '.gif': 'image/gif',
        '.ico': 'image/x-icon',
        '.svg': 'image/svg+xml',
        '.woff': 'font/woff',
        '.woff2': 'font/woff2',
        '.ttf': 'font/ttf',
        '.eot': 'application/vnd.ms-fontobject',
        '.wasm': 'application/wasm',
        '.data': 'application/octet-stream',
        '.mem': 'application/octet-stream'
    };
    return mimeTypes[ext] || 'application/octet-stream';
}

// ========================================
// 게임 상태 관리 — 개인전 (Free-for-All)
// ========================================
class GameManager {
    constructor() {
        this.rooms = new Map();
        this.players = new Map(); // playerId → WebSocket
        this.lobbyTimers = new Map();
        this.scoreUpdateTimers = new Map();
        this.botTimers = new Map(); // roomId → botTimer
        this.disconnectTimers = new Map(); // playerId → grace period Timeout
        this.GRACE_PERIOD_MS = 30000; // 재연결 유예 시간
        this.gameTimers = new Map();       // roomId → game setInterval handle
        this.endGameTimers = new Map();    // roomId → [setTimeout, ...] (10s/100ms/20s)
        this.countdownTimers = new Map();  // roomId → countdown setTimeout handle
        this.gameConfig = {
            gameDuration: config.game.gameDuration,
            lobbyWaitTime: config.game.lobbyDuration,
            countdownDuration: config.game.countdownDuration,
            pressRateLimit: config.game.pressRateLimit,
            maxPlayers: config.game.maxPlayers || 100,
            scoreUpdateInterval: 1000,
            botName: config.game.botName || 'WinterBot',
            botPressMin: config.game.botPressIntervalMin || 300,
            botPressMax: config.game.botPressIntervalMax || 800
        };
        this.createRoom('main_room');
    }

    // ===== 방 생성 =====
    createRoom(roomId = null) {
        const finalRoomId = roomId || uuidv4().substring(0, 8);
        const room = {
            id: finalRoomId,
            status: 'lobby', // lobby → waiting → countdown → playing → finished
            lobbyPlayers: new Map(),   // 로비 대기 중인 플레이어
            players: new Map(),        // 게임 참여 중인 플레이어
            waitingPlayers: new Map(), // 게임 진행 중 접속하여 다음 게임 대기하는 플레이어
            usedNicknames: new Set(),
            gameStartTime: null,
            gameEndTime: null,
            remainingTime: this.gameConfig.gameDuration,
            lobbyCountdown: 0,
            lastPressTime: new Map(),
            qrCodeUrl: `${config.server.protocol}://${config.server.host}:${PORT}/unity/index.html?room=${finalRoomId}`,
            scoreChanged: false,
            lastPressInfo: null,
            tdState: 'idle' // 'idle' | 'live' — TD OSC 트리거 중복 발사 방지
        };
        this.rooms.set(finalRoomId, room);
        console.log(`Room created: ${finalRoomId}`);
        return room;
    }

    // ===== 닉네임 중복 확인 =====
    checkNickname(roomId, nickname) {
        const room = this.rooms.get(roomId);
        if (!room) return { available: false, message: 'Room not found' };

        if (!nickname || nickname.trim() === '') {
            return { available: false, message: 'Nickname cannot be empty' };
        }

        const trimmedNickname = nickname.trim();
        if (trimmedNickname.length < 2 || trimmedNickname.length > 12) {
            return { available: false, message: 'Nickname must be 2-12 characters' };
        }

        // 특수문자 필터링: 한글, 영문, 숫자, 한자(CJK), 히라가나, 가타카나, 밑줄, 하이픈만 허용
        const nicknameRegex = /^[가-힣ㄱ-ㅎㅏ-ㅣa-zA-Z0-9\u4E00-\u9FFF\u3040-\u309F\u30A0-\u30FF\u3400-\u4DBF_\- ]+$/;
        if (!nicknameRegex.test(trimmedNickname)) {
            return { available: false, message: 'Nickname can only contain Korean, English, Japanese, Chinese, numbers, underscores, and hyphens' };
        }

        if (room.usedNicknames.has(trimmedNickname)) {
            return { available: false, message: 'Nickname already in use' };
        }

        return { available: true, message: 'Nickname is available' };
    }

    // ===== 닉네임 설정 후 로비 입장 (팀 없음) =====
    setNickname(roomId, playerId, nickname, element = 'None') {
        const room = this.rooms.get(roomId);
        if (!room) throw new Error('Room not found');

        const trimmedNickname = nickname.trim();

        const checkResult = this.checkNickname(roomId, trimmedNickname);
        if (!checkResult.available) {
            throw new Error(checkResult.message);
        }

        const gameStatus = room.status;

        if (gameStatus === 'lobby') {
            room.usedNicknames.add(trimmedNickname);

            const lobbyPlayer = {
                id: playerId,
                nickname: trimmedNickname,
                element: element || 'None',
                joinTime: Date.now()
            };
            room.lobbyPlayers.set(playerId, lobbyPlayer);

            console.log(`${trimmedNickname} (${playerId}) joined lobby. Total: ${room.lobbyPlayers.size}`);

            // 첫 번째 플레이어 → 로비 타이머 시작 + TD 트리거 (디바운스됨)
            if (room.lobbyPlayers.size === 1) {
                this.startLobbyTimer(roomId);
                this.maybeTriggerWinterRoadStart(room);
                console.log(`First player joined lobby - TD trigger attempted (state=${room.tdState})`);
            }

            return {
                success: true,
                inLobby: true,
                lobbyCountdown: room.lobbyCountdown,
                nickname: trimmedNickname,
                gameStatus: 'lobby',
                message: 'Joined lobby. Waiting for game to start...'
            };
        } else if (gameStatus === 'playing' || gameStatus === 'countdown' || gameStatus === 'waiting') {
            console.log(`Game in progress, ${trimmedNickname} must wait`);
            return {
                success: true,
                inLobby: false,
                isWaiting: true,
                gameStatus: gameStatus,
                remainingTime: room.remainingTime,
                nickname: trimmedNickname,
                message: `Game in progress. Please wait ${room.remainingTime} seconds...`
            };
        } else {
            // finished
            return {
                success: true,
                inLobby: false,
                isWaiting: true,
                gameStatus: 'finished',
                remainingTime: 10,
                nickname: trimmedNickname,
                message: 'Game finished. Lobby will open in 10 seconds...'
            };
        }
    }

    // ===== 방 입장 (Admin/Observer용) =====
    joinRoom(roomId, playerId, playerName = 'Player') {
        const room = this.rooms.get(roomId);
        if (!room) throw new Error('Room not found');

        if (!playerName || playerName === 'Player' || playerName.trim() === '') {
            const shortId = playerId.length > 8 ? playerId.substring(playerId.length - 8) : playerId;
            playerName = `Player_${shortId}`;
        }

        // 재접속 처리
        if (room.players.has(playerId)) {
            const existingPlayer = room.players.get(playerId);
            existingPlayer.connected = true;
            existingPlayer.name = playerName;
            console.log(`${existingPlayer.name} (${playerId}) reconnected`);

            this.broadcastToRoom(roomId, {
                type: 'playerReconnected',
                data: {
                    playerId, playerName: existingPlayer.name,
                    playerCount: this.getActivePlayerCount(room)
                }
            });
            return { player: existingPlayer, room };
        }

        // Admin
        if (playerName.includes('Admin')) {
            const player = {
                id: playerId, name: playerName,
                score: 0, pressCount: 0, lastPressTime: 0,
                connected: true, isAdmin: true, isObserver: false
            };
            room.players.set(playerId, player);
            console.log(`Admin ${playerName} (${playerId}) joined room ${roomId}`);
            return { player, room };
        }

        // Observer
        if (playerName === 'Observer' || playerName === 'ConnectionTest' || playerName.includes('Observer')) {
            const player = {
                id: playerId, name: playerName,
                score: 0, pressCount: 0, lastPressTime: 0,
                connected: true, isAdmin: false, isObserver: true
            };
            room.players.set(playerId, player);
            console.log(`Observer (${playerId}) joined room ${roomId}`);
            return { player, room };
        }

        // 일반 플레이어 (joinRoom 직접 호출 시)
        if (room.status !== 'waiting') {
            throw new Error('Game already started');
        }

        const player = {
            id: playerId, name: playerName,
            score: 0, pressCount: 0, lastPressTime: 0,
            connected: true, isAdmin: false, isObserver: false
        };
        room.players.set(playerId, player);
        console.log(`${playerName} (${playerId}) joined room ${roomId}`);
        return { player, room };
    }

    // ===== 로비 타이머 =====
    startLobbyTimer(roomId) {
        const room = this.rooms.get(roomId);
        if (!room || room.status !== 'lobby') return;

        this.cancelLobbyTimer(roomId);

        room.lobbyCountdown = this.gameConfig.lobbyWaitTime;
        console.log(`Lobby timer started: ${this.gameConfig.lobbyWaitTime}s`);

        // 타이머 시작 알림
        this.broadcastToAll(roomId, {
            type: 'lobbyTimerStarted',
            data: {
                countdown: this.gameConfig.lobbyWaitTime,
                playerCount: room.lobbyPlayers.size
            }
        });

        const timer = setInterval(() => {
            const room = this.rooms.get(roomId);
            if (!room || room.status !== 'lobby') {
                clearInterval(timer);
                this.lobbyTimers.delete(roomId);
                return;
            }

            room.lobbyCountdown--;

            this.broadcastToAll(roomId, {
                type: 'lobbyTimerUpdate',
                data: {
                    countdown: room.lobbyCountdown,
                    playerCount: room.lobbyPlayers.size
                }
            });

            if (room.lobbyCountdown <= 0) {
                clearInterval(timer);
                this.lobbyTimers.delete(roomId);
                console.log(`Lobby timer expired. Moving players to game.`);
                try {
                    this.moveLobbyPlayersToGame(roomId);
                } catch (error) {
                    console.error(`Failed to move lobby players:`, error);
                }
            }
        }, 1000);

        this.lobbyTimers.set(roomId, timer);
    }

    cancelLobbyTimer(roomId) {
        const timer = this.lobbyTimers.get(roomId);
        if (timer) {
            clearInterval(timer);
            this.lobbyTimers.delete(roomId);
            const room = this.rooms.get(roomId);
            if (room) room.lobbyCountdown = 0;
            console.log(`Lobby timer cancelled`);

            this.broadcastToAll(roomId, { type: 'lobbyTimerCancelled' });
        }
    }

    // ===== 모든 게임 사이클 타이머 일괄 정리 =====
    clearAllGameTimers(roomId) {
        // endGame이 예약한 setTimeout 배열 (10s/100ms/20s)
        const ets = this.endGameTimers.get(roomId);
        if (ets) {
            ets.forEach(t => { try { clearTimeout(t); } catch (e) {} });
            this.endGameTimers.delete(roomId);
            console.log(`Cleared endGame timers for ${roomId}`);
        }

        // 게임 진행 setInterval
        const gt = this.gameTimers.get(roomId);
        if (gt) {
            clearInterval(gt);
            this.gameTimers.delete(roomId);
            console.log(`Cleared game timer for ${roomId}`);
        }

        // 카운트다운 setTimeout
        const ct = this.countdownTimers.get(roomId);
        if (ct) {
            clearTimeout(ct);
            this.countdownTimers.delete(roomId);
            console.log(`Cleared countdown timer for ${roomId}`);
        }

        // 기존에 Map으로 추적하던 타이머들도 일관되게 같이 정리
        this.stopScoreUpdateTimer(roomId);
        this.stopBotTimer(roomId);
    }

    // ===== OSC 트리거 디바운스 (TD 상태 반영) =====
    maybeTriggerWinterRoadStart(room) {
        if (!room) return;
        if (room.tdState === 'live') {
            console.log(`Skip /goto_live (room already live)`);
            return;
        }
        triggerWinterRoadStart();
        room.tdState = 'live';
    }

    maybeTriggerWinterRoadEnd(room) {
        if (!room) return;
        if (room.tdState === 'idle') {
            console.log(`Skip /goto_preset (room already idle)`);
            return;
        }
        triggerWinterRoadEnd();
        room.tdState = 'idle';
    }

    // ===== 로비 → 게임 이동 =====
    moveLobbyPlayersToGame(roomId) {
        const room = this.rooms.get(roomId);
        if (!room || room.status !== 'lobby') return;

        console.log(`Moving ${room.lobbyPlayers.size} players from lobby to game`);

        room.lobbyPlayers.forEach((lobbyPlayer, playerId) => {
            const player = {
                id: playerId,
                name: lobbyPlayer.nickname,
                element: lobbyPlayer.element || 'None',
                score: 0,
                pressCount: 0,
                lastPressTime: 0,
                connected: true,
                isAdmin: false,
                isObserver: false
            };
            room.players.set(playerId, player);
            console.log(`   ${lobbyPlayer.nickname} moved to game`);
        });

        room.lobbyPlayers.clear();
        room.status = 'waiting';

        this.broadcastToRoom(roomId, {
            type: 'movedToGameRoom',
            data: {
                message: 'Game will start soon!',
                playerCount: this.getActivePlayerCount(room)
            }
        });

        console.log(`All lobby players moved. Starting game...`);
        this.startGame(roomId);
    }

    // ===== 게임 시작 =====
    startGame(roomId) {
        const room = this.rooms.get(roomId);
        if (!room || room.status !== 'waiting') {
            throw new Error('Game is not in a waiting state');
        }

        room.status = 'countdown';
        console.log(`Starting countdown for room: ${roomId}`);

        this.broadcastToRoom(roomId, {
            type: 'switchToGameVideo',
            message: 'Switching to game video'
        });

        const countdownTime = this.gameConfig.countdownDuration;

        this.broadcastToRoom(roomId, {
            type: 'countdownStart',
            countdown: countdownTime
        });

        if (countdownTime > 0) {
            this.startCountdown(roomId, countdownTime);
        } else {
            this.actuallyStartGame(roomId);
        }
    }

    startCountdown(roomId, countdown) {
        const room = this.rooms.get(roomId);
        if (!room || room.status !== 'countdown') {
            this.countdownTimers.delete(roomId);
            return;
        }

        if (countdown > 0) {
            this.broadcastToRoom(roomId, {
                type: 'countdownUpdate',
                countdown: countdown
            });
            const next = setTimeout(() => this.startCountdown(roomId, countdown - 1), 1000);
            this.countdownTimers.set(roomId, next);
        } else {
            this.countdownTimers.delete(roomId);
            this.actuallyStartGame(roomId);
        }
    }

    actuallyStartGame(roomId) {
        const room = this.rooms.get(roomId);
        if (!room || room.status !== 'countdown') return;

        room.status = 'playing';
        room.gameStartTime = Date.now();
        room.gameEndTime = room.gameStartTime + (this.gameConfig.gameDuration * 1000);
        room.remainingTime = this.gameConfig.gameDuration;

        console.log(`Game started: ${roomId}`);

        // 실제 플레이어가 1명이면 봇 추가 (gameStart 브로드캐스트 전에 추가해야 플레이어 목록에 포함됨)
        const realPlayerCount = this.getRealPlayerCount(room);
        if (realPlayerCount === 1) {
            this.addBot(roomId);
        }

        const playersList = this.getPlayersList(room);
        console.log(`   ${playersList.length} players in game`);

        this.broadcastToRoom(roomId, {
            type: 'gameStart',
            gameDuration: this.gameConfig.gameDuration,
            data: {
                startTime: room.gameStartTime,
                duration: this.gameConfig.gameDuration,
                players: playersList,
                playerCount: this.getActivePlayerCount(room)
            }
        });

        this.startGameTimer(roomId);
        this.startScoreUpdateTimer(roomId);
        this.sendGameStateToObservers(roomId);
    }

    // ===== 게임 타이머 =====
    startGameTimer(roomId) {
        // 이전 타이머가 남아있으면 먼저 정리 (중첩 방지)
        const prev = this.gameTimers.get(roomId);
        if (prev) clearInterval(prev);

        const timer = setInterval(() => {
            try {
                const room = this.rooms.get(roomId);
                if (!room || room.status !== 'playing') {
                    clearInterval(timer);
                    this.gameTimers.delete(roomId);
                    return;
                }

                const now = Date.now();
                room.remainingTime = Math.max(0, Math.floor((room.gameEndTime - now) / 1000));

                this.broadcastToRoom(roomId, {
                    type: 'timeUpdate',
                    remainingTime: room.remainingTime,
                    data: { remainingTime: room.remainingTime }
                });

                if (room.remainingTime % 10 === 0) {
                    this.sendGameStateToObservers(roomId);
                }

                if (room.remainingTime <= 0) {
                    clearInterval(timer);
                    this.gameTimers.delete(roomId);
                    this.endGame(roomId);
                }
            } catch (error) {
                console.error(`Game timer error:`, error);
            }
        }, 1000);

        this.gameTimers.set(roomId, timer);
    }

    // ===== 봇 관리 =====
    addBot(roomId) {
        const room = this.rooms.get(roomId);
        if (!room) return;

        const botId = `bot_${Date.now()}`;
        
        // 봇에게 랜덤 element 부여 (실제 플레이어와 다른 element 선택)
        const allElements = ['Joy', 'Sadness', 'Courage', 'Love', 'Hope', 'Friendship'];
        let botElement = allElements[Math.floor(Math.random() * allElements.length)];
        
        // 실제 플레이어의 element와 다른 것을 선택 시도
        const realPlayerElements = [];
        room.players.forEach(p => {
            if (!p.isAdmin && !p.isObserver && !p.isBot && p.element && p.element !== 'None') {
                realPlayerElements.push(p.element);
            }
        });
        
        if (realPlayerElements.length > 0) {
            const availableElements = allElements.filter(e => !realPlayerElements.includes(e));
            if (availableElements.length > 0) {
                botElement = availableElements[Math.floor(Math.random() * availableElements.length)];
            }
        }
        
        const bot = {
            id: botId,
            name: this.gameConfig.botName,
            element: botElement,
            score: 0,
            pressCount: 0,
            lastPressTime: 0,
            connected: true,
            isAdmin: false,
            isObserver: false,
            isBot: true
        };

        room.players.set(botId, bot);
        console.log(`Bot added: ${bot.name} (${botId}) - element: ${botElement}`);

        // 봇 press 타이머 시작
        this.startBotTimer(roomId, botId);
    }

    startBotTimer(roomId, botId) {
        const scheduleNext = () => {
            const room = this.rooms.get(roomId);
            if (!room || room.status !== 'playing') return;

            const bot = room.players.get(botId);
            if (!bot || !bot.isBot) return;

            // 봇 press 실행
            bot.score++;
            bot.pressCount++;
            bot.lastPressTime = Date.now();
            room.scoreChanged = true;

            // 다음 press를 랜덤 간격으로 예약
            const min = this.gameConfig.botPressMin;
            const max = this.gameConfig.botPressMax;
            const delay = Math.floor(Math.random() * (max - min)) + min;
            const timer = setTimeout(scheduleNext, delay);
            this.botTimers.set(roomId, timer);
        };

        // 최초 시작 (1초 후부터)
        const timer = setTimeout(scheduleNext, 1000);
        this.botTimers.set(roomId, timer);
    }

    stopBotTimer(roomId) {
        const timer = this.botTimers.get(roomId);
        if (timer) {
            clearTimeout(timer);
            this.botTimers.delete(roomId);
            console.log(`Bot timer stopped for room: ${roomId}`);
        }
    }

    removeBot(roomId) {
        // 봇 타이머 정리
        const timer = this.botTimers.get(roomId);
        if (timer) {
            clearTimeout(timer);
            this.botTimers.delete(roomId);
        }

        // 봇 플레이어 제거
        const room = this.rooms.get(roomId);
        if (room) {
            room.players.forEach((player, playerId) => {
                if (player.isBot) {
                    console.log(`Bot removed: ${player.name} (score: ${player.score})`);
                    room.players.delete(playerId);
                }
            });
        }
    }

    getRealPlayerCount(room) {
        let count = 0;
        room.players.forEach(p => {
            if (!p.isAdmin && !p.isObserver && !p.isBot) count++;
        });
        return count;
    }

    // ===== 게임 종료 =====
    endGame(roomId) {
        const room = this.rooms.get(roomId);
        if (!room) return;

        room.status = 'finished';
        this.stopBotTimer(roomId);
        this.stopScoreUpdateTimer(roomId);

        // 최종 점수 즉시 전송
        if (room.scoreChanged) {
            this.broadcastToRoom(roomId, {
                type: 'scoreUpdate',
                data: {
                    players: this.getPlayersList(room),
                    lastPress: room.lastPressInfo
                }
            });
            room.scoreChanged = false;
        }

        // 순위 생성 (점수 내림차순)
        const rankings = [];
        room.players.forEach((player, playerId) => {
            if (!player.isAdmin && !player.isObserver) {
                rankings.push({
                    playerId,
                    nickname: player.name,
                    element: player.element || 'None',
                    score: player.score,
                    pressCount: player.pressCount
                });
            }
        });

        rankings.sort((a, b) => {
            if (b.score !== a.score) return b.score - a.score;
            return b.pressCount - a.pressCount;
        });

        // 순위 번호 부여
        rankings.forEach((r, i) => r.rank = i + 1);

        // 우승자 (1등)
        const winner = rankings.length > 0 ? rankings[0] : null;

        console.log(`Game ended: ${roomId}`);
        if (winner) {
            console.log(`Winner: ${winner.nickname} (${winner.score}pts)`);
        }
        rankings.forEach((r) => {
            console.log(`   ${r.rank}. ${r.nickname} - ${r.score}pts (${r.pressCount} presses)`);
        });

        // TD 서버 종료 트리거 (14초 딜레이)
        const t1_tdEnd = setTimeout(() => {
            const r = this.rooms.get(roomId);
            if (!r || r.status !== 'finished') {
                console.log(`Skip deferred /goto_preset (room state changed)`);
                return;
            }
            this.maybeTriggerWinterRoadEnd(r);
            this.broadcastToRoom(roomId, {
                type: 'videoReset',
                message: '풀영상 정지 및 초기화'
            });
        }, 14000);

        // gameEnd 메시지 전송
        this.broadcastToRoom(roomId, {
            type: 'gameEnd',
            data: {
                winner: winner ? {
                    nickname: winner.nickname,
                    score: winner.score,
                    playerId: winner.playerId
                } : null,
                rankings: rankings,
                playerCount: this.getActivePlayerCount(room)
            }
        });

        // 플레이어들을 닉네임 스텝으로 복귀
        const t2_return = setTimeout(() => {
            const r = this.rooms.get(roomId);
            if (!r || r.status !== 'finished') {
                console.log(`Skip deferred returnPlayersToNicknameStep (room state changed)`);
                return;
            }
            this.returnPlayersToNicknameStep(roomId);
        }, 100);

        // 20초 후 자동 리셋
        const t3_autoReset = setTimeout(() => {
            this.autoResetToWaiting(roomId);
        }, 20000);

        // 위 3개 타이머 핸들 저장 (resetGame/autoReset에서 일괄 취소 가능)
        this.endGameTimers.set(roomId, [t1_tdEnd, t2_return, t3_autoReset]);
    }

    // ===== 닉네임 스텝으로 복귀 =====
    returnPlayersToNicknameStep(roomId) {
        const room = this.rooms.get(roomId);
        if (!room) return;

        const nicknamesToRemove = [];

        room.players.forEach((player, playerId) => {
            if (!player.isAdmin && !player.isObserver) {
                const ws = this.players.get(playerId);
                if (ws && ws.readyState === WebSocket.OPEN) {
                    ws.send(JSON.stringify({
                        type: 'returnToNicknameStep',
                        data: {
                            message: 'Game finished. Please set your nickname to play again.',
                            previousNickname: player.name
                        }
                    }));
                }
                nicknamesToRemove.push(player.name);
            }
        });

        nicknamesToRemove.forEach(name => room.usedNicknames.delete(name));
        console.log(`Returned players to nickname step, freed ${nicknamesToRemove.length} nicknames`);
    }

    // ===== 대기 중인 플레이어 입장 허용 =====
    allowWaitingPlayersToJoin(roomId) {
        const room = this.rooms.get(roomId);
        if (!room || room.status !== 'lobby' || room.waitingPlayers.size === 0) return;

        console.log(`Returning ${room.waitingPlayers.size} waiting players to nickname step`);

        const nicknamesToRemove = [];
        room.waitingPlayers.forEach((waitingPlayer, playerId) => {
            const ws = this.players.get(playerId);
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({
                    type: 'returnToNicknameStep',
                    data: {
                        message: 'Game finished. Please set nickname again.',
                        previousNickname: waitingPlayer.nickname
                    }
                }));
                if (waitingPlayer.nickname) nicknamesToRemove.push(waitingPlayer.nickname);
            }
        });

        nicknamesToRemove.forEach(name => room.usedNicknames.delete(name));
        room.waitingPlayers.clear();
    }

    // ===== 자동 리셋 =====
    autoResetToWaiting(roomId) {
        const room = this.rooms.get(roomId);
        if (!room || room.status !== 'finished') return;

        console.log(`Auto resetting room to lobby: ${roomId}`);

        // 혹시 남아있을 수 있는 게임 사이클 타이머 일괄 정리
        this.clearAllGameTimers(roomId);

        // 일반 플레이어 제거 (Admin/Observer 유지)
        const toRemove = [];
        room.players.forEach((player, playerId) => {
            if (!player.isAdmin && !player.isObserver) {
                toRemove.push(playerId);
            }
        });
        toRemove.forEach(id => room.players.delete(id));

        room.status = 'lobby';
        room.remainingTime = this.gameConfig.gameDuration;
        room.gameStartTime = null;
        room.gameEndTime = null;
        room.lobbyCountdown = 0;
        room.scoreChanged = false;
        room.lastPressInfo = null;

        this.broadcastToRoom(roomId, {
            type: 'autoReset',
            data: {
                status: 'lobby',
                playerCount: this.getActivePlayerCount(room),
                remainingTime: room.remainingTime
            }
        });

        this.sendGameStateToObservers(roomId);
        this.allowWaitingPlayersToJoin(roomId);
    }

    // ===== 게임 리셋 (Admin 수동) =====
    resetGame(roomId) {
        const room = this.rooms.get(roomId);
        if (!room) throw new Error('Room not found');

        // 모든 게임 사이클 타이머 취소 (좀비 타이머 방지)
        this.cancelLobbyTimer(roomId);
        this.clearAllGameTimers(roomId);

        // TD가 live 상태였다면 preset으로 되돌림
        this.maybeTriggerWinterRoadEnd(room);

        room.status = 'lobby';
        room.remainingTime = this.gameConfig.gameDuration;
        room.lobbyCountdown = 0;
        room.scoreChanged = false;
        room.lastPressInfo = null;

        room.players.forEach(player => {
            player.score = 0;
            player.pressCount = 0;
            player.lastPressTime = 0;
        });

        console.log(`Game reset: ${roomId}`);
        this.broadcastToRoom(roomId, {
            type: 'gameReset',
            data: {
                status: 'lobby',
                playerCount: this.getActivePlayerCount(room)
            }
        });
    }

    // ===== Press 처리 =====
    handlePress(roomId, playerId) {
        const room = this.rooms.get(roomId);
        if (!room || room.status !== 'playing') {
            return { success: false, message: 'Game not in progress' };
        }

        const player = room.players.get(playerId);
        if (!player) return { success: false, message: 'Player not found' };

        if (player.isAdmin || player.isObserver) {
            return { success: false, message: 'Observers cannot play' };
        }

        const now = Date.now();
        const timeSinceLastPress = now - player.lastPressTime;
        if (timeSinceLastPress < (1000 / this.gameConfig.pressRateLimit)) {
            return { success: false, message: 'Too fast' };
        }

        player.score++;
        player.pressCount++;
        player.lastPressTime = now;

        room.scoreChanged = true;
        room.lastPressInfo = {
            playerId,
            playerName: player.name,
            playerScore: player.score,
            timestamp: now,
            pressCount: player.pressCount
        };

        // Observer에게 즉시 알림 (파티클용 - element 포함)
        this.broadcastToObservers(roomId, {
            type: 'playerPressImmediate',
            data: {
                playerId,
                playerName: player.name,
                element: player.element || 'None',
                playerScore: player.score,
                timestamp: now
            }
        });

        return { success: true, score: player.score };
    }

    // ===== 점수 배치 업데이트 =====
    startScoreUpdateTimer(roomId) {
        const timer = setInterval(() => {
            const room = this.rooms.get(roomId);
            if (!room || room.status !== 'playing') {
                clearInterval(timer);
                this.scoreUpdateTimers.delete(roomId);
                return;
            }

            if (room.scoreChanged) {
                this.broadcastToRoom(roomId, {
                    type: 'scoreUpdate',
                    data: {
                        players: this.getPlayersList(room),
                        lastPress: room.lastPressInfo
                    }
                });
                this.sendGameStateToObservers(roomId);
                room.scoreChanged = false;
            }
        }, this.gameConfig.scoreUpdateInterval);

        this.scoreUpdateTimers.set(roomId, timer);
    }

    stopScoreUpdateTimer(roomId) {
        const timer = this.scoreUpdateTimers.get(roomId);
        if (timer) {
            clearInterval(timer);
            this.scoreUpdateTimers.delete(roomId);
        }
    }

    // ===== 유틸리티 =====
    getPlayersList(room) {
        const list = [];
        room.players.forEach((player, playerId) => {
            // Admin/Observer는 플레이어 목록에서 제외
            if (player.isAdmin || player.isObserver) return;
            
            list.push({
                id: playerId,
                name: player.name,
                element: player.element || 'None',
                score: player.score,
                pressCount: player.pressCount,
                connected: player.connected,
                isAdmin: false,
                isObserver: false
            });
        });
        return list;
    }

    getActivePlayerCount(room) {
        let count = 0;
        room.players.forEach(p => {
            if (!p.isAdmin && !p.isObserver) count++;
        });
        return count;
    }

    getDetailedGameInfo(room) {
        const activePlayers = [];
        const observers = [];

        room.players.forEach((player, playerId) => {
            const info = {
                id: playerId,
                name: player.name,
                element: player.element || 'None',
                score: player.score,
                pressCount: player.pressCount,
                connected: player.connected
            };
            if (player.isObserver || player.isAdmin) {
                observers.push(info);
            } else {
                activePlayers.push(info);
            }
        });

        // 점수 내림차순 정렬
        activePlayers.sort((a, b) => b.score - a.score);

        return {
            room: {
                id: room.id,
                status: room.status,
                remainingTime: room.remainingTime,
                gameStartTime: room.gameStartTime,
                gameEndTime: room.gameEndTime
            },
            players: activePlayers,
            observers: observers,
            playerCount: activePlayers.length
        };
    }

    // ===== 브로드캐스트 =====
    broadcastToRoom(roomId, message) {
        const room = this.rooms.get(roomId);
        if (!room) return;

        let messageStr;
        try { messageStr = JSON.stringify(message); }
        catch (e) { console.error(`Stringify error:`, e.message); return; }

        room.players.forEach((player, playerId) => {
            try {
                const ws = this.players.get(playerId);
                if (ws && ws.readyState === WebSocket.OPEN) {
                    ws.send(messageStr);
                }
            } catch (e) { /* skip failed send */ }
        });
    }

    // 로비 + 게임방 플레이어 모두에게 전송
    broadcastToAll(roomId, message) {
        const room = this.rooms.get(roomId);
        if (!room) return;

        let messageStr;
        try { messageStr = JSON.stringify(message); }
        catch (e) { return; }

        // 로비 플레이어
        room.lobbyPlayers.forEach((lp, playerId) => {
            try {
                const ws = this.players.get(playerId);
                if (ws && ws.readyState === WebSocket.OPEN) ws.send(messageStr);
            } catch (e) { /* skip */ }
        });

        // 게임방 플레이어 (Admin/Observer 포함)
        room.players.forEach((player, playerId) => {
            try {
                const ws = this.players.get(playerId);
                if (ws && ws.readyState === WebSocket.OPEN) ws.send(messageStr);
            } catch (e) { /* skip */ }
        });
    }

    broadcastToObservers(roomId, message) {
        const room = this.rooms.get(roomId);
        if (!room) return;

        let messageStr;
        try { messageStr = JSON.stringify(message); }
        catch (e) { return; }

        room.players.forEach((player, playerId) => {
            if (player.isObserver || player.isAdmin) {
                try {
                    const ws = this.players.get(playerId);
                    if (ws && ws.readyState === WebSocket.OPEN) ws.send(messageStr);
                } catch (e) { /* skip */ }
            }
        });
    }

    sendGameStateToObservers(roomId) {
        const room = this.rooms.get(roomId);
        if (!room) return;
        this.broadcastToObservers(roomId, {
            type: 'gameStateUpdate',
            data: this.getDetailedGameInfo(room)
        });
    }

    // ===== 플레이어 연결 끊김 처리 (grace period 시작) =====
    markDisconnected(playerId) {
        if (!playerId) return;

        // 해당 플레이어가 속한 방/위치 찾기
        let foundRoom = null;
        let playerEntry = null;
        let location = null; // 'lobby' | 'game' | 'waiting'

        for (const [, room] of this.rooms) {
            if (room.lobbyPlayers.has(playerId)) {
                foundRoom = room;
                playerEntry = room.lobbyPlayers.get(playerId);
                location = 'lobby';
                break;
            }
            if (room.players.has(playerId)) {
                foundRoom = room;
                playerEntry = room.players.get(playerId);
                location = 'game';
                break;
            }
            if (room.waitingPlayers.has(playerId)) {
                foundRoom = room;
                playerEntry = room.waitingPlayers.get(playerId);
                location = 'waiting';
                break;
            }
        }

        // 방 어디에도 없는 소켓(닉네임 설정 전 연결 끊김 등) → 소켓 매핑만 해제
        if (!foundRoom) {
            this.players.delete(playerId);
            return;
        }

        // Admin/Observer/Bot은 grace 불필요 → 즉시 정리
        if (playerEntry.isAdmin || playerEntry.isObserver || playerEntry.isBot) {
            this.cleanupPlayer(playerId);
            return;
        }

        // 게임방 플레이어는 connected=false로 표시 (UI상 흐리게 표시용)
        if (location === 'game') {
            playerEntry.connected = false;
        }

        // 이전 타이머가 있으면 중복 방지
        const prevTimer = this.disconnectTimers.get(playerId);
        if (prevTimer) clearTimeout(prevTimer);

        const displayName = playerEntry.name || playerEntry.nickname || playerId;
        console.log(`${displayName} (${playerId}) disconnected from ${location}, grace ${this.GRACE_PERIOD_MS}ms`);

        // 다른 참가자에게 "연결 끊김" 알림 (퇴장은 아님)
        if (location === 'game') {
            this.broadcastToRoom(foundRoom.id, {
                type: 'playerDisconnected',
                data: {
                    playerId,
                    playerName: displayName,
                    graceMs: this.GRACE_PERIOD_MS
                }
            });
        }

        // grace 만료 시 실제 정리
        const timer = setTimeout(() => {
            this.disconnectTimers.delete(playerId);
            console.log(`Grace expired for ${displayName} (${playerId})`);
            this.cleanupPlayer(playerId);
        }, this.GRACE_PERIOD_MS);

        this.disconnectTimers.set(playerId, timer);

        // 끊어진 소켓 매핑 해제 (재연결 시 새 ws로 교체됨)
        this.players.delete(playerId);
    }

    // ===== 재연결 시도 =====
    tryReconnect(playerId, ws) {
        if (!playerId) return null;

        // 해당 playerId가 속한 위치 찾기 (grace 유무와 관계없이 살아있는 데이터가 있는지 확인)
        let foundRoom = null;
        let playerEntry = null;
        let location = null;

        for (const [, room] of this.rooms) {
            if (room.lobbyPlayers.has(playerId)) { foundRoom = room; playerEntry = room.lobbyPlayers.get(playerId); location = 'lobby'; break; }
            if (room.players.has(playerId)) { foundRoom = room; playerEntry = room.players.get(playerId); location = 'game'; break; }
            if (room.waitingPlayers.has(playerId)) { foundRoom = room; playerEntry = room.waitingPlayers.get(playerId); location = 'waiting'; break; }
        }

        if (!foundRoom) return null; // 이미 cleanup됐거나 알 수 없는 playerId

        // 중복 연결 감지: 같은 playerId로 현재 살아있는 소켓이 있으면 강제 종료
        const existingWs = this.players.get(playerId);
        if (existingWs && existingWs !== ws && existingWs.readyState === WebSocket.OPEN) {
            console.log(`Duplicate connection for ${playerId}, closing previous socket`);
            try {
                existingWs.send(JSON.stringify({
                    type: 'duplicateConnection',
                    data: { message: 'Another session started on a new device/tab.' }
                }));
                existingWs.close(1000, 'Duplicate session');
            } catch (e) { /* ignore */ }
        }

        // grace 타이머 취소
        const timer = this.disconnectTimers.get(playerId);
        if (timer) {
            clearTimeout(timer);
            this.disconnectTimers.delete(playerId);
        }

        // 새 소켓 바인딩
        this.players.set(playerId, ws);
        if (location === 'game') playerEntry.connected = true;

        const displayName = playerEntry.name || playerEntry.nickname || playerId;
        console.log(`Reconnected: ${displayName} (${playerId}) at ${location}`);

        // 다른 참가자에게 복귀 알림
        if (location === 'game') {
            this.broadcastToRoom(foundRoom.id, {
                type: 'playerReconnected',
                data: {
                    playerId,
                    playerName: displayName,
                    playerCount: this.getActivePlayerCount(foundRoom)
                }
            });
        }

        return { room: foundRoom, player: playerEntry, location };
    }

    // ===== 플레이어 실제 정리 (grace 만료 또는 Admin/Observer/Bot) =====
    cleanupPlayer(playerId) {
        for (const [roomId, room] of this.rooms) {
            // 로비에서 제거
            if (room.lobbyPlayers.has(playerId)) {
                const lp = room.lobbyPlayers.get(playerId);
                room.lobbyPlayers.delete(playerId);
                room.usedNicknames.delete(lp.nickname);
                console.log(`${lp.nickname} left lobby (${room.lobbyPlayers.size} remaining)`);

                if (room.lobbyPlayers.size === 0 && room.status === 'lobby') {
                    this.cancelLobbyTimer(roomId);
                }
            }

            // 대기자에서 제거
            if (room.waitingPlayers.has(playerId)) {
                const wp = room.waitingPlayers.get(playerId);
                room.waitingPlayers.delete(playerId);
                room.usedNicknames.delete(wp.nickname);
                console.log(`${wp.nickname} removed from waiting list`);
            }

            // 게임방에서 제거
            if (room.players.has(playerId)) {
                const player = room.players.get(playerId);
                room.players.delete(playerId);
                room.usedNicknames.delete(player.name);

                if (!player.isAdmin && !player.isObserver) {
                    console.log(`Player ${player.name} removed`);
                } else {
                    console.log(`${player.isAdmin ? 'Admin' : 'Observer'} ${player.name} removed`);
                }

                this.broadcastToRoom(roomId, {
                    type: 'playerLeft',
                    data: {
                        playerId,
                        playerName: player.name,
                        playerCount: this.getActivePlayerCount(room)
                    }
                });
                break;
            }
        }

        this.players.delete(playerId);
    }

    // 하위 호환용 별칭 (외부 호출부가 있을 경우 대비)
    removePlayer(playerId) {
        this.markDisconnected(playerId);
    }
}

const gameManager = new GameManager();

// ========================================
// WebSocket 연결 처리
// ========================================
wss.on('connection', (ws, req) => {
    let playerId = null;

    // Heartbeat: 이 소켓을 살아있다고 표시. PONG 수신 시 갱신됨.
    ws.isAlive = true;
    ws.on('pong', () => { ws.isAlive = true; });

    ws.on('message', (message) => {
        try {
            const data = JSON.parse(message);

            switch (data.type) {
                case 'ping':
                    ws.send(JSON.stringify({
                        type: 'pong',
                        timestamp: Date.now(),
                        originalTimestamp: data.timestamp
                    }));
                    break;

                case 'reconnect': {
                    const clientPlayerId = data.playerId;
                    if (!clientPlayerId) {
                        ws.send(JSON.stringify({
                            type: 'reconnectFailed',
                            data: { reason: 'missing_player_id' }
                        }));
                        break;
                    }

                    const result = gameManager.tryReconnect(clientPlayerId, ws);
                    if (!result) {
                        ws.send(JSON.stringify({
                            type: 'reconnectFailed',
                            data: { reason: 'session_expired' }
                        }));
                        break;
                    }

                    playerId = clientPlayerId; // 이 소켓 스코프의 playerId 복원
                    const { room, player, location } = result;

                    // 현재 게임 상태 스냅샷 구성
                    ws.send(JSON.stringify({
                        type: 'reconnected',
                        data: {
                            playerId,
                            roomId: room.id,
                            location, // 'lobby' | 'game' | 'waiting'
                            nickname: player.name || player.nickname || '',
                            element: player.element || 'None',
                            score: player.score || 0,
                            pressCount: player.pressCount || 0,
                            isAdmin: !!player.isAdmin,
                            isObserver: !!player.isObserver,
                            room: {
                                id: room.id,
                                status: room.status,
                                remainingTime: room.remainingTime,
                                lobbyCountdown: room.lobbyCountdown,
                                playerCount: gameManager.getActivePlayerCount(room),
                                qrCodeUrl: room.qrCodeUrl
                            },
                            players: gameManager.getPlayersList(room)
                        }
                    }));
                    break;
                }

                case 'checkNickname': {
                    const roomId = data.roomId || 'main_room';
                    try {
                        const result = gameManager.checkNickname(roomId, data.nickname);
                        ws.send(JSON.stringify({ type: 'nicknameCheckResult', data: result }));
                    } catch (error) {
                        ws.send(JSON.stringify({ type: 'error', data: { message: error.message } }));
                    }
                    break;
                }

                case 'setNickname': {
                    try {
                        // playerId는 서버에서만 생성
                        if (!playerId) {
                            playerId = uuidv4();
                            console.log(`🆕 New playerId: ${playerId}`);
                        }
                        gameManager.players.set(playerId, ws);

                        const roomId = data.roomId || 'main_room';
                        const result = gameManager.setNickname(roomId, playerId, data.nickname, data.element);

                        ws.send(JSON.stringify({
                            type: 'nicknameSetResult',
                            data: {
                                ...result,
                                playerId: playerId,
                                roomId: roomId
                            }
                        }));
                    } catch (error) {
                        ws.send(JSON.stringify({ type: 'error', data: { message: error.message } }));
                    }
                    break;
                }

                case 'getGameStatus': {
                    const roomId = data.roomId || 'main_room';
                    const room = gameManager.rooms.get(roomId);
                    if (room) {
                        ws.send(JSON.stringify({
                            type: 'gameStatusResult',
                            data: {
                                roomId, status: room.status,
                                remainingTime: room.remainingTime,
                                canJoin: room.status === 'waiting',
                                playerCount: gameManager.getActivePlayerCount(room)
                            }
                        }));
                    } else {
                        ws.send(JSON.stringify({ type: 'error', data: { message: 'Room not found' } }));
                    }
                    break;
                }

                case 'joinRoom': {
                    try {
                        // playerId는 서버에서만 생성
                        if (!playerId) {
                            playerId = uuidv4();
                            console.log(`🆕 New playerId for joinRoom: ${playerId}`);
                        }

                        const { player, room } = gameManager.joinRoom(data.roomId, playerId, data.playerName);
                        gameManager.players.set(playerId, ws);

                        const allPlayers = gameManager.getPlayersList(room);

                        const responseData = {
                            playerId,
                            player,
                            room: {
                                id: room.id,
                                status: room.status,
                                playerCount: gameManager.getActivePlayerCount(room),
                                remainingTime: room.remainingTime,
                                qrCodeUrl: room.qrCodeUrl
                            },
                            players: allPlayers
                        };

                        ws.send(JSON.stringify({ type: 'joinedRoom', data: responseData }));

                        if (player.isObserver) {
                            setTimeout(() => gameManager.sendGameStateToObservers(room.id), 500);
                        }

                        gameManager.broadcastToRoom(room.id, {
                            type: 'playerJoined',
                            data: {
                                playerId, playerName: player.name,
                                playerCount: gameManager.getActivePlayerCount(room)
                            }
                        });
                    } catch (error) {
                        ws.send(JSON.stringify({ type: 'error', data: { message: error.message } }));
                    }
                    break;
                }

                case 'startGame': {
                    if (!playerId) break;
                    let foundRoom = null, foundPlayer = null;

                    gameManager.rooms.forEach((room) => {
                        if (room.players.has(playerId)) {
                            foundRoom = room;
                            foundPlayer = room.players.get(playerId);
                        }
                    });

                    if (foundRoom && foundPlayer && foundPlayer.isAdmin) {
                        try {
                            gameManager.startGame(foundRoom.id);
                            console.log(`Game started by ${foundPlayer.name}`);
                        } catch (error) {
                            ws.send(JSON.stringify({ type: 'error', data: { message: error.message } }));
                        }
                    } else {
                        ws.send(JSON.stringify({ type: 'error', data: { message: 'Admin permission required' } }));
                    }
                    break;
                }

                case 'press': {
                    if (!playerId) break;
                    let foundRoom = null;

                    gameManager.rooms.forEach((room) => {
                        if (room.players.has(playerId)) foundRoom = room;
                    });

                    if (foundRoom) {
                        const result = gameManager.handlePress(foundRoom.id, playerId);
                        ws.send(JSON.stringify({ type: 'pressResult', data: result }));
                    } else {
                        ws.send(JSON.stringify({
                            type: 'pressResult',
                            data: { success: false, message: 'Player not in game' }
                        }));
                    }
                    break;
                }

                case 'getRoomInfo': {
                    if (!playerId) break;
                    let foundRoom = null, foundPlayer = null;

                    gameManager.rooms.forEach((room) => {
                        if (room.players.has(playerId)) {
                            foundRoom = room;
                            foundPlayer = room.players.get(playerId);
                        }
                    });

                    if (foundRoom) {
                        ws.send(JSON.stringify({
                            type: 'roomInfo',
                            data: {
                                room: {
                                    id: foundRoom.id, status: foundRoom.status,
                                    playerCount: gameManager.getActivePlayerCount(foundRoom),
                                    remainingTime: foundRoom.remainingTime,
                                    qrCodeUrl: foundRoom.qrCodeUrl
                                },
                                player: foundPlayer
                            }
                        }));
                    } else {
                        const mainRoom = gameManager.rooms.get('main_room');
                        if (mainRoom) {
                            ws.send(JSON.stringify({
                                type: 'roomInfo',
                                data: {
                                    room: {
                                        id: mainRoom.id, status: mainRoom.status,
                                        playerCount: gameManager.getActivePlayerCount(mainRoom),
                                        remainingTime: mainRoom.remainingTime,
                                        qrCodeUrl: mainRoom.qrCodeUrl
                                    },
                                    player: null
                                }
                            }));
                        }
                    }
                    break;
                }

                case 'resetGame': {
                    if (!playerId) break;
                    let foundRoom = null, foundPlayer = null;

                    gameManager.rooms.forEach((room) => {
                        if (room.players.has(playerId)) {
                            foundRoom = room;
                            foundPlayer = room.players.get(playerId);
                        }
                    });

                    if (foundRoom && foundPlayer && foundPlayer.isAdmin) {
                        try {
                            gameManager.resetGame(foundRoom.id);
                            console.log(`Game reset by ${foundPlayer.name}`);
                        } catch (error) {
                            ws.send(JSON.stringify({ type: 'error', data: { message: error.message } }));
                        }
                    } else {
                        ws.send(JSON.stringify({ type: 'error', data: { message: 'Admin permission required' } }));
                    }
                    break;
                }
            }
        } catch (error) {
            console.error('Message processing error:', error);
            ws.send(JSON.stringify({ type: 'error', data: { message: 'Invalid message format' } }));
        }
    });

    ws.on('close', () => {
        console.log(`Connection closed: ${playerId}`);
        if (playerId) gameManager.markDisconnected(playerId);
    });

    ws.on('error', (error) => {
        console.error(`WebSocket error [${playerId}]:`, error);
        if (playerId) gameManager.markDisconnected(playerId);
    });
});

// ========================================
// Admin 대시보드
// ========================================
app.get('/', async (req, res) => {
    let mainRoom = gameManager.rooms.get('main_room');
    if (!mainRoom) {
        mainRoom = gameManager.createRoom('main_room');
    }

    let qrCodeDataUrl = '';
    try {
        qrCodeDataUrl = await QRCode.toDataURL(mainRoom.qrCodeUrl);
    } catch (err) {
        console.error('Failed to generate QR code:', err);
    }

    const playerCount = gameManager.getActivePlayerCount(mainRoom);

    const html = `
        <!DOCTYPE html>
        <html lang="ko">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Winter Road Game Server</title>
            <style>
                body { margin: 0; padding: 20px; font-family: Arial, sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; display: flex; flex-direction: column; align-items: center; }
                .container { text-align: center; background: rgba(255, 255, 255, 0.1); padding: 30px; border-radius: 15px; backdrop-filter: blur(10px); box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1); max-width: 600px; width: 100%; color: white; }
                h1 { margin-bottom: 20px; text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.3); }
                .button { display: inline-block; background: linear-gradient(45deg, #ff6b6b, #ee5a24); color: white; padding: 15px 30px; text-decoration: none; border-radius: 25px; font-size: 18px; font-weight: bold; transition: all 0.3s ease; box-shadow: 0 4px 15px rgba(0, 0, 0, 0.2); margin: 10px; border: none; cursor: pointer; }
                .button:hover { transform: translateY(-2px); box-shadow: 0 6px 20px rgba(0, 0, 0, 0.3); }
                .room-info { background: rgba(255, 255, 255, 0.1); padding: 20px; border-radius: 10px; margin: 20px 0; }
                .qr-code { background: white; padding: 20px; border-radius: 10px; margin: 20px 0; }
                .stat { font-size: 1.4em; font-weight: bold; margin: 10px 0; color: #ffd700; }
                .timer { font-size: 2.5em; font-weight: bold; color: #ffd700; text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5); }
                .timer-section, .winner-section { background: rgba(255, 255, 255, 0.1); padding: 20px; border-radius: 10px; margin: 20px 0; }
                .winner { font-size: 1.5em; font-weight: bold; color: #ffd700; }
                .info { color: rgba(255, 255, 255, 0.8); margin-top: 20px; font-size: 14px; }
            </style>
        </head>
        <body>
            <div class="container">
                <h1>🎮 Winter Road (개인전)</h1>
                <div class="room-info">
                    <h3>🎯 Main Game Room</h3>
                    <p>Status: <span id="roomStatus">${mainRoom.status}</span></p>
                    <div class="stat" id="playerCount">${playerCount} players</div>
                    <div class="timer-section" id="timerSection" style="display: ${mainRoom.status === 'playing' ? 'block' : 'none'};">
                        <h4>⏰ Game Timer</h4>
                        <div class="timer" id="gameTimer"></div>
                    </div>
                    <div class="timer-section" id="autoStartSection" style="display: none;">
                        <h4>⏰ 자동 시작 타이머</h4>
                        <div class="timer" id="autoStartTimer"></div>
                    </div>
                    <div class="winner-section" id="winnerSection" style="display: ${mainRoom.status === 'finished' ? 'block' : 'none'};">
                        <h4>🏆 Game Result</h4>
                        <div class="winner" id="winnerInfo"></div>
                    </div>
                    <div id="qrCode" class="qr-code" style="display: ${mainRoom.status === 'playing' ? 'none' : 'block'};">
                        <h4>📱 Mobile Access QR Code</h4>
                        <img src="${qrCodeDataUrl}" alt="QR Code" style="max-width: 200px;">
                    </div>
                    <button class="button" id="startButton" onclick="startGame()" style="display: ${mainRoom.status === 'waiting' ? 'inline-block' : 'none'};">Start Game</button>
                    <button class="button" id="resetButton" onclick="resetGame()" style="display: ${mainRoom.status !== 'waiting' ? 'inline-block' : 'none'};">Reset Game</button>
                </div>
                <div class="info">
                    <p>Server: ${config.server.protocol}://${config.server.host}:${PORT}</p>
                    <p>WebSocket: ${config.websocket.protocol}://${config.server.host}:${PORT}</p>
                </div>
            </div>
            <script>
                let ws;
                function startWebSocket() {
                    ws = new WebSocket('${config.websocket.protocol}://${config.server.host}:${PORT}');
                    ws.onopen = () => {
                        ws.send(JSON.stringify({ type: 'joinRoom', roomId: 'main_room', playerName: 'Admin' }));
                    };
                    ws.onmessage = (e) => handleMsg(JSON.parse(e.data));
                    ws.onclose = () => setTimeout(startWebSocket, 3000);
                }
                document.addEventListener('DOMContentLoaded', startWebSocket);

                function handleMsg(m) {
                    switch(m.type) {
                        case 'joinedRoom':
                            updateUI('waiting', m.data.room?.playerCount || 0);
                            break;
                        case 'lobbyTimerStarted':
                            showAutoStartTimer(m.data.countdown);
                            updatePlayerCount(m.data.playerCount);
                            break;
                        case 'lobbyTimerUpdate':
                            updateAutoStartTimer(m.data.countdown);
                            updatePlayerCount(m.data.playerCount);
                            break;
                        case 'lobbyTimerCancelled':
                            hideAutoStartTimer();
                            break;
                        case 'gameStart':
                            updateUI('playing', m.data?.playerCount || 0);
                            updateTimer(m.data?.duration || m.gameDuration || 60);
                            hideAutoStartTimer();
                            break;
                        case 'scoreUpdate':
                            if (m.data?.players) updatePlayerCount(m.data.players.filter(p => !p.isAdmin && !p.isObserver).length);
                            break;
                        case 'timeUpdate':
                            updateTimer(m.data?.remainingTime ?? m.remainingTime);
                            break;
                        case 'gameEnd':
                            showGameResult(m.data);
                            break;
                        case 'playerJoined':
                        case 'playerLeft':
                            updatePlayerCount(m.data?.playerCount || 0);
                            break;
                        case 'gameReset':
                        case 'autoReset':
                            updateUI('waiting', m.data?.playerCount || 0);
                            hideAutoStartTimer();
                            break;
                    }
                }

                function updateUI(status, count) {
                    document.getElementById('roomStatus').textContent = status;
                    document.getElementById('playerCount').textContent = count + ' players';
                    document.getElementById('startButton').style.display = status === 'waiting' ? 'inline-block' : 'none';
                    document.getElementById('resetButton').style.display = status !== 'waiting' ? 'inline-block' : 'none';
                    document.getElementById('qrCode').style.display = status === 'waiting' || status === 'lobby' ? 'block' : 'none';
                    document.getElementById('timerSection').style.display = status === 'playing' ? 'block' : 'none';
                    document.getElementById('winnerSection').style.display = status === 'finished' ? 'block' : 'none';
                }
                function updatePlayerCount(c) { document.getElementById('playerCount').textContent = c + ' players'; }
                function updateTimer(t) {
                    const m = Math.floor(t/60), s = t%60;
                    document.getElementById('gameTimer').textContent = String(m).padStart(2,'0') + ':' + String(s).padStart(2,'0');
                }
                function showAutoStartTimer(c) { document.getElementById('autoStartSection').style.display = 'block'; updateAutoStartTimer(c); }
                function updateAutoStartTimer(c) {
                    const m = Math.floor(c/60), s = c%60;
                    document.getElementById('autoStartTimer').textContent = String(m).padStart(2,'0') + ':' + String(s).padStart(2,'0');
                }
                function hideAutoStartTimer() { document.getElementById('autoStartSection').style.display = 'none'; }
                function showGameResult(d) {
                    const w = d.winner;
                    document.getElementById('winnerInfo').textContent = w ? '🏆 ' + w.nickname + ' Wins! (' + w.score + 'pts)' : '🏆 No winner';
                    updateUI('finished', d.playerCount || 0);
                }
                function startGame() { if (ws?.readyState === 1) ws.send(JSON.stringify({ type: 'startGame' })); }
                function resetGame() { if (ws?.readyState === 1) ws.send(JSON.stringify({ type: 'resetGame', roomId: 'main_room' })); }
            </script>
        </body>
        </html>
    `;
    res.send(html);
});

// Admin REST API
app.post('/api/reset-game/:roomId', (req, res) => {
    try {
        gameManager.resetGame(req.params.roomId);
        res.json({ success: true, message: 'Game reset successfully' });
    } catch (error) {
        res.status(400).json({ success: false, message: error.message });
    }
});

app.post('/api/generate-qr', async (req, res) => {
    const { text } = req.body;
    if (!text) return res.status(400).json({ success: false, message: 'Text is required.' });
    try {
        const qrCodeDataUrl = await QRCode.toDataURL(text);
        res.json({ success: true, dataUrl: qrCodeDataUrl });
    } catch (err) {
        res.status(500).json({ success: false, message: 'Failed to generate QR code.' });
    }
});

// ========================================
// 서버 시작
// ========================================
server.listen(PORT, config.server.host, () => {
    console.log(`Winter Road Game Server Started! (개인전 모드)`);
    console.log(`Server: http://${config.server.host}:${PORT}`);
    console.log(`Unity WebGL: http://${config.server.host}:${PORT}/unity`);
    console.log(`WebSocket: ws://${config.server.host}:${PORT}`);
    console.log(`Admin Panel: http://${config.server.host}:${PORT}`);
    console.log(`Ready!`);
    console.log(`========================================`);
    initTDServerConnection();
}).on('error', (err) => {
    console.error(`Server failed to start: ${err.message}`);
    if (err.code === 'EADDRINUSE') {
        console.error(`Port ${PORT} is already in use!`);
    } else if (err.code === 'EACCES') {
        console.error(`Permission denied for port ${PORT}`);
    }
    process.exit(1);
});