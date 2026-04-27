# Winter Road Game Server 🎮

Unity WebGL 빌드를 서빙하고 TouchDesigner와 OSC 통신하는 게임 서버입니다.

## 빠른 시작

### 1. 의존성 설치
```bash
npm install
```

### 2. TD 서버 설정 (중요!)
`config.json` 파일을 열고 TD 서버 IP 설정:

```json
{
  "osc": {
    "enabled": true,
    "host": "192.168.0.13",  // ← TD 서버의 실제 IP로 변경
    "port": 50013            // ← TouchDesigner OSC 포트
  }
}
```

**TD 서버 IP 확인 방법:**
```bash
# TD 서버 컴퓨터에서 실행
ipconfig
```

### 3. 서버 실행
```bash
# Windows
start-server.bat

# 또는 직접 실행
node server.js
```

## 접속 방법

서버가 실행되면 다음 주소로 접속할 수 있습니다:

- **메인 페이지**: `http://0.0.0.0:3000`
- **Unity 게임**: `http://0.0.0.0:3000/unity/index.html`
- **서버 상태**: `http://0.0.0.0:3000/status`

## 프로젝트 구조

```
WinterRoadWebServer/
├── UnityWebGLBuild/           # Unity WebGL 빌드 파일들
├── server.js                  # Express & WebSocket 서버
├── config.json                # 서버 설정 파일 ⭐
├── test-osc-receiver.js       # OSC 수신 테스트 서버
├── test-osc-sender.js         # OSC 송신 테스트
├── test-network-connection.js # 네트워크 연결 테스트
├── package.json               # 프로젝트 설정
├── CONFIG-GUIDE.md            # 설정 가이드
├── OSC-TEST-GUIDE.md          # OSC 테스트 가이드
└── NETWORK-SETUP-GUIDE.md     # 네트워크 설정 가이드
```

## 주요 기능

- ✅ Unity WebGL 빌드 정적 파일 서빙
- ✅ WebSocket 기반 실시간 멀티플레이어 게임
- ✅ 팀 시스템 (Red vs Blue)
- ✅ 대기방 & 자동 게임 시작
- ✅ TouchDesigner OSC 통신
- ✅ 봇 자동 생성 (팀 밸런스)
- ✅ Admin 대시보드
- ✅ QR 코드 생성

## config.json 설정

### 기본 설정
```json
{
  "server": {
    "host": "0.0.0.0",
    "port": 3000
  },
  "game": {
    "gameDuration": 30,        // 게임 시간 (초)
    "pressRateLimit": 10       // 초당 버튼 누름 제한
  },
  "osc": {
    "enabled": true,           // OSC 사용 여부
    "host": "192.168.0.13",    // TD 서버 IP ⭐
    "port": 50013              // TD OSC 포트 ⭐
  }
}
```

**자세한 설정 방법**: `CONFIG-GUIDE.md` 참고

## OSC 통신 테스트

### 로컬 테스트 (TD 서버 없이)

1. **config.json 수정** - 로컬 테스트 모드:
   ```json
   {
     "osc": {
       "enabled": true,
       "host": "127.0.0.1",
       "port": 50013
     }
   }
   ```

2. **OSC 수신 서버 실행**:
   ```bash
   node test-osc-receiver.js
   ```

3. **게임 서버 실행** (새 터미널):
   ```bash
   node server.js
   ```

4. **게임 플레이** → OSC 메시지 확인

### 실제 TD 서버 연결 테스트

```bash
# TD 서버 연결 테스트
node test-network-connection.js 192.168.0.13 50013

# 또는 배치 파일 실행
test-td-connection.bat
```

**자세한 내용**: `OSC-TEST-GUIDE.md` 참고

## 문제 해결

### 1. Unity 게임이 로드되지 않는 경우
- Unity WebGL 빌드 파일들이 `UnityWebGLBuild` 폴더에 있는지 확인
- 브라우저 콘솔에서 에러 메시지 확인

### 2. 포트가 이미 사용 중인 경우
```bash
# Windows
netstat -ano | findstr :3000
taskkill /PID [프로세스ID] /F

# 또는 다른 포트 사용
PORT=8080 node server.js
```

### 3. OSC 메시지가 전송되지 않는 경우
```bash
# 1. 네트워크 연결 확인
ping 192.168.0.13

# 2. OSC 연결 테스트
node test-network-connection.js 192.168.0.13 50013

# 3. TD 서버 방화벽 설정 (TD 서버 컴퓨터에서)
# PowerShell 관리자 권한:
New-NetFirewallRule -DisplayName "OSC Winter Road" -Direction Inbound -Protocol UDP -LocalPort 50013 -Action Allow
```

**자세한 해결 방법**: `NETWORK-SETUP-GUIDE.md` 참고

4. **⚠️ Windows에서 서버가 멈추는 현상 (엔터 키를 눌러야 계속 실행됨)**
   
   **원인**: Windows 콘솔의 QuickEdit Mode 때문에 발생합니다. 콘솔 창을 클릭하면 프로세스가 일시 중지됩니다.
   
   **해결 방법**:
   
   - **방법 1** (권장): 관리자 권한으로 `disable-quickedit.bat` 실행
     ```
     우클릭 → "관리자 권한으로 실행"
     ```
   
   - **방법 2**: 수동으로 QuickEdit Mode 비활성화
     1. PowerShell 창 상단 클릭 → "속성"
     2. "빠른 편집 모드" 체크 해제
     3. "기본값으로 저장" 선택
   
   - **방법 3**: 콘솔 창 클릭하지 않기
     - `start-server.bat`에 경고 메시지가 표시됩니다
     - 콘솔 창을 클릭하지 마세요!
     - 만약 클릭했다면 **엔터 키** 또는 **ESC 키**를 누르세요

## 개발 팁

- `npm run dev`로 실행하면 파일 변경 시 자동으로 서버가 재시작됩니다
- Unity WebGL 빌드를 업데이트한 후에는 브라우저 캐시를 삭제하세요
