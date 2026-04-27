# ✅ Winter Road Game Server 설정 체크리스트

## 📋 초기 설정 (최초 1회)

### 1. 의존성 설치
```bash
npm install
```
- [ ] npm install 완료

---

## 🎯 TD 서버 연결 설정

### 2. TD 서버 IP 확인

**TD 서버 컴퓨터에서 실행:**
```bash
ipconfig
```

**IPv4 주소 메모:**
```
예: 192.168.0.13
```
- [ ] TD 서버 IP 확인 완료

### 3. config.json 수정

**파일 위치**: `WinterRoadWebServer/config.json`

**수정할 부분:**
```json
{
  "osc": {
    "enabled": true,
    "host": "192.168.0.13",  // ← 여기를 TD 서버 IP로 변경
    "port": 50013            // ← TouchDesigner OSC 포트 확인
  }
}
```

- [ ] `osc.host`를 TD 서버 IP로 변경
- [ ] `osc.port`를 TouchDesigner 포트와 일치시킴
- [ ] `osc.enabled`가 `true`인지 확인

### 4. TD 서버 방화벽 설정

**TD 서버 컴퓨터에서 실행 (PowerShell 관리자 권한):**
```powershell
New-NetFirewallRule -DisplayName "OSC Winter Road" -Direction Inbound -Protocol UDP -LocalPort 50013 -Action Allow
```

- [ ] 방화벽 규칙 추가 완료

### 5. 연결 테스트

**게임 서버 컴퓨터에서 실행:**
```bash
node test-network-connection.js 192.168.0.13 50013
```

**확인 사항:**
- [ ] Ping 테스트 성공
- [ ] OSC 연결 테스트 성공

---

## 🧪 OSC 통신 확인 (선택사항)

### 6. OSC 수신 테스트

**TD 서버 컴퓨터에서 (또는 게임 서버에서):**
```bash
node test-osc-receiver.js
```

**게임 서버 실행 (새 터미널):**
```bash
node server.js
```

**게임 접속 후 확인:**
- [ ] 첫 플레이어 입장 시 `/goto_live` 메시지 수신 확인
- [ ] 게임 종료 시 `/goto_preset` 메시지 수신 확인

---

## 🚀 서버 실행

### 7. 게임 서버 시작

```bash
# Windows
start-server.bat

# 또는 직접 실행
node server.js
```

**서버 실행 확인:**
```
✅ TD Server OSC connection ready (192.168.0.13:50013)
🚀 Winter Road Game Server Started!
📍 Server Address: http://0.0.0.0:3000
```

- [ ] 서버 실행 성공
- [ ] OSC 연결 메시지 확인

### 8. Admin 대시보드 접속

**브라우저에서:**
```
http://localhost:3000
```

- [ ] Admin 대시보드 접속 확인
- [ ] QR 코드 표시 확인

### 9. 게임 접속 테스트

**모바일/Unity에서:**
```
http://[게임서버IP]:3000/unity/index.html
```

또는 QR 코드 스캔

- [ ] 게임 접속 성공
- [ ] 닉네임 입력 가능
- [ ] 대기방 입장 가능

---

## 🎮 전체 시나리오 테스트

### 10. 게임 플로우 테스트

1. **플레이어 입장**
   - [ ] 첫 번째 플레이어 입장 시 대기방 타이머 시작 (20초)
   - [ ] TD 서버에서 `/goto_live` 메시지 수신 확인

2. **게임 시작**
   - [ ] 20초 후 자동으로 게임 시작
   - [ ] 인트로 영상 재생 (59초)
   - [ ] 카운트다운 후 게임 시작

3. **게임 진행**
   - [ ] 버튼 클릭 시 점수 증가
   - [ ] 팀 점수 실시간 업데이트
   - [ ] Admin 대시보드에서 점수 확인

4. **게임 종료**
   - [ ] 게임 시간 종료
   - [ ] TD 서버에서 `/goto_preset` 메시지 수신 확인
   - [ ] 순위 화면 표시
   - [ ] 자동으로 대기방으로 리셋

---

## 🔧 문제 발생 시 체크

### OSC 메시지가 안 보일 때

1. **네트워크 연결 확인**
   ```bash
   ping 192.168.0.13
   ```
   - [ ] Ping 성공

2. **IP 주소 재확인**
   ```bash
   ipconfig
   ```
   - [ ] IP 주소가 config.json과 일치

3. **방화벽 상태 확인**
   ```powershell
   Get-NetFirewallRule -DisplayName "OSC Winter Road"
   ```
   - [ ] 방화벽 규칙 존재

4. **OSC 테스트**
   ```bash
   node test-network-connection.js 192.168.0.13 50013
   ```
   - [ ] 테스트 통과

### 게임 서버가 시작 안 될 때

1. **포트 충돌 확인**
   ```bash
   netstat -ano | findstr :3000
   ```
   - [ ] 포트 3000이 사용 가능

2. **config.json 문법 확인**
   - [ ] JSON 형식이 올바른지 확인
   - [ ] 큰따옴표 사용 확인
   - [ ] 마지막 쉼표 없는지 확인

---

## 📚 참고 문서

설정 중 막히는 부분이 있으면 다음 문서를 참고하세요:

- 📘 **CONFIG-GUIDE.md** - config.json 상세 설정 가이드
- 📗 **OSC-TEST-GUIDE.md** - OSC 테스트 상세 가이드
- 📙 **NETWORK-SETUP-GUIDE.md** - 네트워크 연결 설정 가이드
- 📕 **OSC-QUICK-START.md** - OSC 빠른 시작 가이드
- 📖 **README.md** - 프로젝트 전체 개요

---

## ✨ 설정 완료!

모든 체크리스트를 완료했다면 준비 완료입니다! 🎉

**다음 단계:**
1. TouchDesigner에서 OSC In CHOP 설정
2. 게임 플레이 테스트
3. TD 비주얼 연동 확인







