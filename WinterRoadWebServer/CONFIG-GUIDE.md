# ⚙️ config.json 설정 가이드

## 📋 전체 설정 구조

```json
{
  "server": {
    "host": "0.0.0.0",
    "port": 3000,
    "protocol": "http"
  },
  "websocket": {
    "protocol": "ws"
  },
  "game": {
    "defaultRoomId": "main_room",
    "gameDuration": 30,
    "maxPlayersPerTeam": 50,
    "pressRateLimit": 10
  },
  "osc": {
    "enabled": true,
    "host": "192.168.0.13",
    "port": 50013
  }
}
```

---

## 🌐 Server 설정

### `server.host`
- **기본값**: `"0.0.0.0"`
- **설명**: 서버가 바인딩할 IP 주소
- **옵션**:
  - `"0.0.0.0"` - 모든 네트워크 인터페이스에서 접속 허용 (권장)
  - `"127.0.0.1"` - 로컬호스트만 접속 가능
  - `"192.168.0.10"` - 특정 IP에서만 접속 가능

### `server.port`
- **기본값**: `3000`
- **설명**: 웹 서버 포트 번호
- **권장**: 3000-9999 사이의 포트 사용

### `server.protocol`
- **기본값**: `"http"`
- **설명**: 서버 프로토콜
- **옵션**: `"http"` 또는 `"https"`

---

## 🔌 WebSocket 설정

### `websocket.protocol`
- **기본값**: `"ws"`
- **설명**: WebSocket 프로토콜
- **옵션**: `"ws"` 또는 `"wss"` (HTTPS 사용 시)

---

## 🎮 Game 설정

### `game.defaultRoomId`
- **기본값**: `"main_room"`
- **설명**: 기본 게임 룸 ID
- **권장**: 변경 불필요

### `game.gameDuration`
- **기본값**: `30`
- **설명**: 게임 진행 시간 (초)
- **권장**: 30-180초
- **예시**:
  - `30` - 30초
  - `60` - 1분
  - `120` - 2분

### `game.maxPlayersPerTeam`
- **기본값**: `50`
- **설명**: 팀당 최대 플레이어 수
- **권장**: 실제 예상 인원에 맞게 조정

### `game.pressRateLimit`
- **기본값**: `10`
- **설명**: 초당 최대 버튼 누름 횟수
- **권장**: 5-20 사이
- **예시**:
  - `10` - 초당 10회 (0.1초당 1회)
  - `20` - 초당 20회 (0.05초당 1회)

---

## 🎵 OSC 설정 (TD 서버 연동)

### `osc.enabled`
- **기본값**: `true`
- **설명**: OSC 통신 활성화 여부
- **옵션**:
  - `true` - OSC 통신 사용
  - `false` - OSC 통신 비활성화 (TD 서버 없이 테스트)

### `osc.host` ⭐
- **기본값**: `"192.168.0.13"`
- **설명**: TD 서버의 IP 주소
- **설정 방법**:
  1. TD 서버 컴퓨터에서 `ipconfig` 실행
  2. IPv4 주소 확인
  3. 해당 IP를 여기에 입력

**예시:**
```json
"osc": {
  "host": "192.168.0.13"  // ← TD 서버의 실제 IP로 변경
}
```

### `osc.port` ⭐
- **기본값**: `50013`
- **설명**: TD 서버의 OSC 수신 포트
- **설정 방법**:
  - TouchDesigner의 OSC In CHOP에서 설정한 포트와 동일하게 설정

**예시:**
```json
"osc": {
  "port": 50013  // ← TouchDesigner에서 설정한 포트와 동일하게
}
```

---

## 🧪 환경별 설정 예시

### 로컬 개발 환경 (OSC 없이 테스트)

```json
{
  "server": {
    "host": "0.0.0.0",
    "port": 3000,
    "protocol": "http"
  },
  "websocket": {
    "protocol": "ws"
  },
  "game": {
    "defaultRoomId": "main_room",
    "gameDuration": 30,
    "maxPlayersPerTeam": 50,
    "pressRateLimit": 10
  },
  "osc": {
    "enabled": false,
    "host": "127.0.0.1",
    "port": 50013
  }
}
```

### OSC 로컬 테스트 환경

```json
{
  "server": {
    "host": "0.0.0.0",
    "port": 3000,
    "protocol": "http"
  },
  "websocket": {
    "protocol": "ws"
  },
  "game": {
    "defaultRoomId": "main_room",
    "gameDuration": 30,
    "maxPlayersPerTeam": 50,
    "pressRateLimit": 10
  },
  "osc": {
    "enabled": true,
    "host": "127.0.0.1",
    "port": 50013
  }
}
```

**사용 방법:**
1. `node test-osc-receiver.js` 실행
2. `node server.js` 실행

### 프로덕션 환경 (실제 TD 서버)

```json
{
  "server": {
    "host": "0.0.0.0",
    "port": 3000,
    "protocol": "http"
  },
  "websocket": {
    "protocol": "ws"
  },
  "game": {
    "defaultRoomId": "main_room",
    "gameDuration": 120,
    "maxPlayersPerTeam": 50,
    "pressRateLimit": 10
  },
  "osc": {
    "enabled": true,
    "host": "192.168.0.13",
    "port": 50013
  }
}
```

---

## 🔧 TD 서버 IP 확인 방법

### Windows

```bash
ipconfig
```

**출력 예시:**
```
이더넷 어댑터 이더넷:
   IPv4 주소 . . . . . . . . . : 192.168.0.13
                                 ↑
                                 이 IP를 config.json에 입력
```

### 네트워크 연결 테스트

```bash
# TD 서버 IP로 ping 테스트
ping 192.168.0.13

# OSC 연결 테스트
node test-network-connection.js 192.168.0.13 50013
```

---

## ⚡ 빠른 설정 체크리스트

설정을 변경할 때 확인하세요:

### OSC 통신을 사용하는 경우:
- [ ] TD 서버의 IP 주소 확인 (`ipconfig`)
- [ ] `config.json`의 `osc.host`를 TD 서버 IP로 변경
- [ ] `config.json`의 `osc.port`를 TouchDesigner 포트와 동일하게 설정
- [ ] `config.json`의 `osc.enabled`를 `true`로 설정
- [ ] TD 서버 방화벽에서 UDP 포트 허용
- [ ] `node test-network-connection.js` 테스트 성공 확인

### OSC 통신 없이 테스트하는 경우:
- [ ] `config.json`의 `osc.enabled`를 `false`로 설정

---

## 🚨 주의사항

### 1. JSON 형식 유지
- 큰따옴표(`"`) 사용 필수
- 마지막 항목에는 쉼표(`,`) 없음
- 숫자는 따옴표 없이 입력
- boolean은 `true` / `false` (소문자)

**❌ 잘못된 예시:**
```json
{
  "osc": {
    "enabled": "true",     // ❌ 문자열이 아니라 boolean
    "host": 192.168.0.13,  // ❌ IP는 문자열로
    "port": "50013",       // ❌ 포트는 숫자로
  }                        // ❌ 마지막 쉼표 제거
}
```

**✅ 올바른 예시:**
```json
{
  "osc": {
    "enabled": true,
    "host": "192.168.0.13",
    "port": 50013
  }
}
```

### 2. 설정 변경 후 재시작 필요
- `config.json` 변경 후 서버 재시작 필수
- `Ctrl+C`로 서버 중지 후 `node server.js` 재실행

### 3. IP 주소 확인
- TD 서버의 IP가 변경될 수 있으므로 주기적으로 확인
- DHCP 사용 시 IP가 자동 변경될 수 있음
- 고정 IP 설정 권장

---

## 📚 관련 문서

- `OSC-TEST-GUIDE.md` - OSC 테스트 상세 가이드
- `NETWORK-SETUP-GUIDE.md` - 네트워크 연결 설정 가이드
- `OSC-QUICK-START.md` - 빠른 시작 가이드







