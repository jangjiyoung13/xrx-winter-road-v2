# 🌐 네트워크 환경별 OSC 연결 가이드

## 📌 네트워크 상황별 정리

### ✅ 같은 로컬 네트워크 (LAN) - **방화벽만 설정하면 OK**

```
[게임 서버]          [공유기/스위치]          [TD 서버]
192.168.0.10  ←─────  LAN  ─────→  192.168.0.13
                   UDP 50013
```

**조건:**
- 같은 공유기/스위치에 연결
- 같은 IP 대역 (예: 192.168.0.x)
- 방화벽 설정만 필요

---

## 🚀 빠른 테스트 방법

### 1️⃣ 자동 연결 테스트
```bash
test-td-connection.bat
```
또는
```bash
node test-network-connection.js 192.168.0.13 50013
```

### 2️⃣ 결과 확인

**✅ 성공 시:**
```
✅ Connection Test Summary:
✅ OSC messages can be sent to 192.168.0.13:50013
✅ Network connection is working!
```

**❌ 실패 시:**
```
❌ Failed to connect to 192.168.0.13:50013
🔍 Possible Issues:
1. TD server is not running or not on this IP
2. Firewall is blocking UDP port 50013
```

---

## 🔥 방화벽 설정 (TD 서버 컴퓨터)

### 방법 1: PowerShell 명령어 (빠름) ⚡

**관리자 권한으로 PowerShell 실행:**

```powershell
New-NetFirewallRule -DisplayName "OSC Winter Road" -Direction Inbound -Protocol UDP -LocalPort 50013 -Action Allow
```

**확인:**
```powershell
Get-NetFirewallRule -DisplayName "OSC Winter Road"
```

### 방법 2: GUI 설정 (자세함) 🖱️

1. **Windows 방화벽 열기:**
   - `Win + R` → `wf.msc` 입력
   - 또는 제어판 → Windows Defender 방화벽 → 고급 설정

2. **인바운드 규칙 추가:**
   - 좌측 "인바운드 규칙" 클릭
   - 우측 "새 규칙..." 클릭

3. **규칙 설정:**
   - **규칙 종류**: 포트
   - **프로토콜**: UDP
   - **포트**: 특정 로컬 포트 → `50013`
   - **작업**: 연결 허용
   - **프로필**: 모두 선택 (도메인, 개인, 공용)
   - **이름**: `OSC Winter Road`

4. **완료 및 활성화**

---

## 📍 IP 주소 확인 방법

### TD 서버 컴퓨터에서 IP 확인

**Windows:**
```bash
ipconfig
```

**출력 예시:**
```
이더넷 어댑터 이더넷:
   IPv4 주소 . . . . . . . . . : 192.168.0.13
   서브넷 마스크 . . . . . . . : 255.255.255.0
   기본 게이트웨이 . . . . . . : 192.168.0.1
```

→ **`192.168.0.13`이 TD 서버의 IP**

### 게임 서버 컴퓨터에서 IP 확인

```bash
node test-network-connection.js
```

출력:
```
📍 Local IP Addresses:
   - 이더넷: 192.168.0.10
```

---

## 🧪 단계별 테스트 절차

### Step 1: IP 확인 ✅

**TD 서버 컴퓨터:**
```bash
ipconfig
```
→ IP 메모 (예: `192.168.0.13`)

**게임 서버 컴퓨터:**
```bash
node test-network-connection.js
```
→ 같은 네트워크인지 확인 (192.168.0.x)

### Step 2: Ping 테스트 ✅

**게임 서버에서:**
```bash
ping 192.168.0.13
```

**성공 예시:**
```
192.168.0.13에 Ping을 보내고 있습니다.
32바이트 데이터 사용:
192.168.0.13의 응답: 바이트=32 시간<1ms TTL=128
```

**실패 시:**
- IP 주소 재확인
- 네트워크 케이블 확인
- 같은 공유기에 연결되어 있는지 확인

### Step 3: 방화벽 설정 ✅

**TD 서버 컴퓨터에서:**
```powershell
# 관리자 PowerShell
New-NetFirewallRule -DisplayName "OSC Winter Road" -Direction Inbound -Protocol UDP -LocalPort 50013 -Action Allow
```

### Step 4: OSC 수신 서버 실행 ✅

**TD 서버 컴퓨터에서 (테스트용):**
```bash
node test-osc-receiver.js
```

### Step 5: 연결 테스트 ✅

**게임 서버 컴퓨터에서:**
```bash
node test-network-connection.js 192.168.0.13 50013
```

### Step 6: TD 서버에서 메시지 확인 ✅

**TD 서버의 test-osc-receiver.js에서 출력 확인:**
```
📨 OSC Message Received:
   ├─ Address: /test/connection
   ├─ Args: [ 'Connection Test', 1699999999999 ]
   └─ From: 192.168.0.10:xxxxx
```

**✅ 이 메시지가 보이면 성공!**

---

## 🎮 게임 서버 설정

### config.json 수정

**로컬 테스트 모드:**
```json
{
  "osc": {
    "enabled": true,
    "localTest": {
      "enabled": true,
      "host": "127.0.0.1",
      "port": 50013
    }
  }
}
```

**실제 TD 서버 연결:**
```json
{
  "osc": {
    "enabled": true,
    "tdServer": {
      "host": "192.168.0.13",
      "port": 50013
    },
    "localTest": {
      "enabled": false
    }
  }
}
```

### 게임 서버 실행 및 확인

```bash
node server.js
```

**출력 확인:**
```
🔌 Initializing OSC connection...
🎯 OSC connection ready [PRODUCTION]
   Target: 192.168.0.13:50013
```

---

## 🔍 문제 해결

### 문제 1: "Host is unreachable"

**원인:**
- 다른 네트워크에 있음
- 네트워크 연결 끊김
- IP 주소 오타

**해결:**
```bash
# IP 재확인
ipconfig

# Ping 테스트
ping 192.168.0.13

# 같은 공유기에 연결되어 있는지 확인
```

### 문제 2: "Connection timeout"

**원인:**
- 방화벽이 차단 중
- TD 서버가 실행되지 않음
- 포트 번호 불일치

**해결:**
```powershell
# 방화벽 규칙 재확인
Get-NetFirewallRule -DisplayName "OSC Winter Road"

# 포트 사용 확인 (TD 서버에서)
netstat -an | findstr :50013
```

### 문제 3: 메시지가 안 보임 (Ping은 됨)

**원인:**
- UDP 포트가 차단됨
- 방화벽 인바운드 규칙 누락

**해결:**
```powershell
# 방화벽 규칙 삭제 후 재생성
Remove-NetFirewallRule -DisplayName "OSC Winter Road"
New-NetFirewallRule -DisplayName "OSC Winter Road" -Direction Inbound -Protocol UDP -LocalPort 50013 -Action Allow

# Windows Defender 방화벽 상태 확인
Get-NetFirewallProfile | Select-Object Name, Enabled
```

### 문제 4: 간헐적으로 메시지 누락

**원인:**
- UDP는 전달 보장 없음 (정상)
- 네트워크 혼잡

**해결:**
- 중요 메시지는 재전송 로직 추가
- 또는 TCP 기반 OSC 고려

---

## 📊 네트워크 환경별 비교

| 환경 | 설정 난이도 | 추가 비용 | 지연시간 | 권장도 |
|------|-------------|-----------|----------|--------|
| **같은 LAN** | ⭐ 쉬움 | 없음 | < 1ms | ⭐⭐⭐⭐⭐ |
| 다른 LAN (VPN) | ⭐⭐⭐ 어려움 | 있음 | 10-50ms | ⭐⭐ |
| 인터넷 공개 | ⭐⭐⭐⭐ 매우 어려움 | 있음 | 50-200ms | ⭐ |

**결론: 같은 LAN 환경이 가장 이상적입니다!**

---

## ✅ 체크리스트

설정 완료 전에 확인하세요:

- [ ] 두 컴퓨터가 같은 네트워크에 연결됨
- [ ] TD 서버의 IP 주소 확인 완료
- [ ] Ping 테스트 성공
- [ ] TD 서버 방화벽 규칙 추가
- [ ] `test-network-connection.js` 테스트 성공
- [ ] TD 서버에서 테스트 메시지 수신 확인
- [ ] `config.json`에 올바른 IP 설정
- [ ] 게임 서버 실행 시 OSC 연결 성공 확인

---

## 🎯 최종 테스트

### 전체 시나리오 테스트

1. **TD 서버 컴퓨터에서:**
   ```bash
   node test-osc-receiver.js
   ```

2. **게임 서버 컴퓨터에서:**
   ```bash
   node server.js
   ```

3. **브라우저에서 게임 접속:**
   - `http://localhost:3000`
   - 플레이어로 입장

4. **TD 서버에서 메시지 확인:**
   ```
   📨 OSC Message Received:
      ├─ Address: /goto_live
      🎮 [ACTION] Winter Road START triggered!
   ```

**✅ 이 메시지가 보이면 완벽하게 설정 완료!**

---

## 💡 추가 팁

### TouchDesigner OSC In 설정

1. **OSC In CHOP 추가**
2. **설정:**
   - Protocol: `UDP`
   - Network Port: `50013`
   - Active: `✓`

3. **메시지 확인:**
   - Table DAT 연결
   - `/goto_live`, `/goto_preset` 확인

### 보안 고려사항

- 프로덕션 환경에서는 방화벽 규칙을 특정 IP로 제한
- 불필요한 포트는 닫기
- 네트워크 모니터링 권장

---

## 📚 관련 문서

- `OSC-TEST-GUIDE.md` - OSC 테스트 상세 가이드
- `OSC-QUICK-START.md` - 빠른 시작 가이드
- `test-network-connection.js` - 네트워크 테스트 스크립트







