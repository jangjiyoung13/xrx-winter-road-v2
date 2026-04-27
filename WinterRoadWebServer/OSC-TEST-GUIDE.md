# 🎵 OSC 통신 테스트 가이드

## 📋 목차
1. [OSC란?](#osc란)
2. [테스트 환경 설정](#테스트-환경-설정)
3. [로컬 테스트 방법](#로컬-테스트-방법)
4. [실제 TD 서버 연결](#실제-td-서버-연결)
5. [문제 해결](#문제-해결)

---

## 🎯 OSC란?

**OSC (Open Sound Control)**는 실시간 멀티미디어 통신을 위한 프로토콜입니다.
- UDP 기반으로 빠른 메시지 전송
- TouchDesigner, Max/MSP, Unity 등에서 널리 사용
- 주소 패턴과 인자로 구성된 메시지 형식

---

## 🛠️ 테스트 환경 설정

### 1. 필요한 패키지 확인
```bash
npm install
```

현재 프로젝트에 `osc` 패키지가 이미 설치되어 있습니다.

### 2. 테스트 파일 확인
- `test-osc-receiver.js` - OSC 메시지 수신 서버
- `test-osc-sender.js` - OSC 메시지 송신 테스트
- `server.js` - 실제 게임 서버 (OSC 송신 포함)

---

## 🧪 로컬 테스트 방법

### 방법 1: 수동 테스트 (추천)

#### Step 1: OSC 수신 서버 실행
```bash
node test-osc-receiver.js
```

**출력 예시:**
```
========================================
🎵 OSC Test Receiver Server
========================================

✅ OSC Receiver is ready!
   Listening on: 0.0.0.0:50013

📍 Test Configuration:
   - Local Test: 127.0.0.1:50013
   - Same Network: [Your-IP]:50013

⏳ Waiting for OSC messages...
```

#### Step 2: 새 터미널에서 OSC 송신 테스트 실행
```bash
node test-osc-sender.js
```

**출력 예시:**
```
========================================
🚀 OSC Test Sender
========================================

✅ OSC Sender is ready!
   Target: 127.0.0.1:50013

🧪 Starting OSC Test Sequence...

📤 Test 1: Sending /goto_live (Winter Road Start)
   ✅ Sent!

⏳ Waiting 5 seconds...

📤 Test 2: Sending /goto_preset (Winter Road End)
   ✅ Sent!

📤 Test 3: Sending custom message with arguments
   ✅ Sent!

✅ All tests completed!
```

#### Step 3: 수신 서버에서 메시지 확인
첫 번째 터미널(receiver)에서 다음과 같은 출력을 확인:
```
📨 OSC Message Received:
   ├─ Address: /goto_live
   ├─ Args: []
   ├─ From: 127.0.0.1:xxxxx
   └─ Time: 오후 2:30:45
   🎮 [ACTION] Winter Road START triggered!

📨 OSC Message Received:
   ├─ Address: /goto_preset
   ├─ Args: []
   ├─ From: 127.0.0.1:xxxxx
   └─ Time: 오후 2:30:50
   🏁 [ACTION] Winter Road END triggered!
```

---

### 방법 2: 게임 서버에서 직접 테스트

#### Step 1: server.js의 TD_SERVER 설정 변경

**현재 설정 (실제 TD 서버):**
```javascript
const TD_SERVER = {
    host: '192.168.0.13',
    port: 50013
};
```

**로컬 테스트 설정으로 변경:**
```javascript
const TD_SERVER = {
    host: '127.0.0.1',  // 또는 'localhost'
    port: 50013
};
```

#### Step 2: OSC 수신 서버 실행
```bash
node test-osc-receiver.js
```

#### Step 3: 게임 서버 실행
```bash
node server.js
```

#### Step 4: 게임 플레이하여 OSC 메시지 트리거
1. 브라우저에서 `http://localhost:3000` 접속
2. QR 코드로 모바일 접속 또는 Unity 빌드 접속
3. 첫 번째 플레이어가 대기방에 입장하면 → `/goto_live` 전송
4. 게임 종료 시 → `/goto_preset` 전송

**수신 서버에서 확인:**
```
📨 OSC Message Received:
   ├─ Address: /goto_live
   ├─ Args: []
   └─ Time: 오후 2:35:12
   🎮 [ACTION] Winter Road START triggered!
```

---

## 🌐 실제 TD 서버 연결

### 네트워크 확인

#### 1. TD 서버의 IP 주소 확인
```bash
# Windows (TD 서버 컴퓨터에서)
ipconfig

# 출력에서 IPv4 주소 확인
Ethernet adapter:
   IPv4 Address: 192.168.0.13
```

#### 2. 연결 가능 여부 테스트
```bash
# 게임 서버 컴퓨터에서
ping 192.168.0.13
```

### server.js 설정

```javascript
const TD_SERVER = {
    host: '192.168.0.13',  // TD 서버의 실제 IP
    port: 50013            // TD에서 설정한 OSC 수신 포트
};
```

### TouchDesigner 설정 (TD 서버 측)

1. **OSC In CHOP** 추가
   - Protocol: UDP
   - Network Port: 50013
   - Active: ✓

2. **테스트 메시지 확인**
   - OSC In CHOP의 출력을 모니터링
   - `/goto_live`, `/goto_preset` 메시지 수신 확인

---

## 🔧 문제 해결

### 문제 1: Port 50013 is already in use

**원인:** 포트가 이미 사용 중입니다.

**해결방법:**
```bash
# Windows
netstat -ano | findstr :50013
taskkill /PID [프로세스ID] /F

# 또는 다른 포트 사용
# test-osc-receiver.js와 server.js의 포트를 동일하게 변경
```

### 문제 2: 메시지가 수신되지 않음

**체크리스트:**
1. ✅ 수신 서버가 실행 중인가?
2. ✅ 방화벽이 UDP 포트를 차단하고 있지 않은가?
3. ✅ IP 주소와 포트가 올바른가?
4. ✅ 같은 네트워크에 있는가?

**방화벽 설정 (Windows):**
```
제어판 > Windows Defender 방화벽 > 고급 설정
> 인바운드 규칙 > 새 규칙
> 포트 > UDP > 특정 로컬 포트: 50013
```

### 문제 3: 네트워크 간 통신 안됨

**로컬 테스트로 먼저 확인:**
```javascript
// test-osc-sender.js에서
const TARGET_HOST = '127.0.0.1';  // 로컬 테스트
```

**작동하면 실제 IP로 변경:**
```javascript
const TARGET_HOST = '192.168.0.13';  // 실제 TD 서버 IP
```

### 문제 4: OSC 메시지 형식 확인

**현재 전송되는 메시지:**
- `/goto_live` - 인자 없음 (게임 시작)
- `/goto_preset` - 인자 없음 (게임 종료)

**TD에서 다른 형식이 필요한 경우:**
```javascript
// server.js의 함수 수정
function triggerWinterRoadStart() {
    return sendOSCtoTD('/goto_live', [
        { type: 'i', value: 1 },      // 정수 추가
        { type: 's', value: 'start' }  // 문자열 추가
    ]);
}
```

---

## 📊 OSC 메시지 타입

| 타입 | 설명 | 예시 |
|------|------|------|
| `i` | 정수 (Integer) | `{ type: 'i', value: 42 }` |
| `f` | 실수 (Float) | `{ type: 'f', value: 3.14 }` |
| `s` | 문자열 (String) | `{ type: 's', value: 'hello' }` |
| `b` | Blob (Binary) | `{ type: 'b', value: buffer }` |
| `T` | True | `{ type: 'T' }` |
| `F` | False | `{ type: 'F' }` |

---

## 🎮 게임 서버 OSC 트리거 시점

| 이벤트 | OSC 메시지 | 발생 시점 |
|--------|-----------|----------|
| 게임 시작 | `/goto_live` | 첫 번째 플레이어가 대기방 입장 |
| 게임 종료 | `/goto_preset` | 게임 시간 종료 |

**server.js에서 트리거 코드 위치:**
```javascript
// Line 304: 첫 플레이어 입장 시
triggerWinterRoadStart();

// Line 949: 게임 종료 시
triggerWinterRoadEnd();
```

---

## 🚀 빠른 테스트 명령어

```bash
# Terminal 1: OSC 수신 서버
node test-osc-receiver.js

# Terminal 2: OSC 송신 테스트
node test-osc-sender.js

# 또는 게임 서버 실행
node server.js
```

---

## 📝 추가 참고사항

- OSC는 UDP 프로토콜을 사용하므로 메시지 전달이 보장되지 않습니다
- 중요한 메시지는 재전송 로직을 고려하세요
- 실시간 성능이 중요한 경우 메시지 크기를 최소화하세요
- TD 서버와의 동기화가 필요한 경우 응답 메시지를 설정하세요

---

## 💡 유용한 도구

- **Protokol** (Mac/Win): OSC 메시지 모니터링 GUI 도구
- **OSCulator** (Mac): OSC 메시지 라우팅 및 변환
- **Pure Data**: OSC 테스트 및 프로토타이핑







