# 🚀 OSC 테스트 빠른 시작 가이드

## 📌 5분 안에 테스트하기

### 1️⃣ OSC 수신 서버 실행
```bash
start-osc-receiver.bat
```
또는
```bash
node test-osc-receiver.js
```

### 2️⃣ 새 터미널에서 송신 테스트 실행
```bash
start-osc-sender-test.bat
```
또는
```bash
node test-osc-sender.js
```

### 3️⃣ 결과 확인
첫 번째 터미널(수신 서버)에서 메시지가 출력되는지 확인하세요:
```
📨 OSC Message Received:
   ├─ Address: /goto_live
   🎮 [ACTION] Winter Road START triggered!
```

---

## 🎮 게임 서버와 함께 테스트하기

### 로컬 테스트 모드로 변경

`server.js` 파일의 **18-21번 줄**을 수정:

```javascript
// 변경 전 (실제 TD 서버)
const TD_SERVER = {
    host: '192.168.0.13',
    port: 50013
};

// 변경 후 (로컬 테스트)
const TD_SERVER = {
    host: '127.0.0.1',
    port: 50013
};
```

### 테스트 순서

1. **OSC 수신 서버 실행**
   ```bash
   node test-osc-receiver.js
   ```

2. **게임 서버 실행**
   ```bash
   node server.js
   ```

3. **브라우저에서 게임 접속**
   - `http://localhost:3000` 접속
   - 플레이어로 입장

4. **OSC 메시지 확인**
   - 첫 플레이어 입장 시 → `/goto_live` 수신
   - 게임 종료 시 → `/goto_preset` 수신

---

## 🔍 트러블슈팅

### "Port 50013 is already in use" 에러
```bash
# Windows에서 포트 사용 프로세스 확인
netstat -ano | findstr :50013

# 해당 프로세스 종료
taskkill /PID [프로세스번호] /F
```

### 메시지가 수신되지 않을 때
- ✅ 수신 서버가 먼저 실행되었는지 확인
- ✅ 방화벽 설정 확인
- ✅ IP와 포트 번호 확인

---

## 📚 더 자세한 정보

자세한 설명과 TD 서버 연결 방법은 `OSC-TEST-GUIDE.md`를 참고하세요.







