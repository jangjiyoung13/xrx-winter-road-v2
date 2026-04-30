# iOS Download Manager 기반 스크린샷 저장 — 구현 가이드

**작성**: 2026-04-30
**대상**: 터미널 환경에서 직접 코드 변경 작업하는 개발자
**관련 파일**: 4개 (server.js · WebGLScreenshot.jslib · ResultPanel.cs · index.html)

---

## 1. 개요

### 1.1 배경

기존 iOS Safari 분기는 다음 흐름이었음:

```
캡처 → Blob URL → HTML <a> 링크 → 사용자 탭 → Safari 새 탭 → 꾹 누르기 → 사진 앱 저장
```

문제점: 마지막 "꾹 누르기" 단계가 사용자에게 번거로움.

### 1.2 새 접근: iOS Download Manager

iOS 13+ Safari 내장 다운로드 매니저를 활용:

```
캡처 → 서버 업로드 → 다운로드 URL 발급 → HTML <a> 링크 → 사용자 탭
    → 서버가 Content-Disposition: attachment 헤더로 응답
    → iOS 다운로드 매니저가 파일 앱(Files)으로 자동 저장
```

**저장 위치**: 사진 앱(Photos)이 아니라 **파일 앱(Files)** 의 다운로드 폴더.

### 1.3 적용 범위

- **iOS 분기에만 적용**. Android/Desktop은 기존 `<a download>` 즉시 다운로드 방식 유지.
- 서버는 이미 운영 중인 `WinterRoadWebServer/server.js`에 엔드포인트 2개 추가.

---

## 2. 아키텍처

```
[Unity WebGL]
   ↓ ① 캡처 → Base64 인코딩
[ResultPanel.cs]
   ↓ ② UnityWebRequest.Post 로 서버에 업로드
[server.js: POST /api/upload-screenshot]
   ↓ ③ 메모리에 Buffer 저장 (10분 후 자동 삭제)
   ← ④ JSON 응답: { url: "/api/download/<id>.png" }
[ResultPanel.cs]
   ↓ ⑤ jslib 호출하여 HTML 링크 href 설정
[WebGLScreenshot.jslib: SetDownloadLink_iOS]
   ↓ ⑥ #ios-screenshot-save-link 의 href 갱신 + display:flex
[사용자가 링크 탭]
   ↓ ⑦ Safari가 GET /api/download/<id>.png 요청
[server.js: GET /api/download/:filename]
   ↓ ⑧ Content-Disposition: attachment 헤더로 응답
[iOS 다운로드 매니저]
   ↓ ⑨ Files 앱 다운로드 폴더에 저장
[완료]
```

---

## 3. 작업 순서

체크리스트 형식. 위에서 아래로 순서대로 진행 권장.

- [ ] **3.1** server.js — 임시 저장소 + 업로드/다운로드 엔드포인트 추가
- [ ] **3.2** WebGLScreenshot.jslib — `SetDownloadLink_iOS` 함수 추가
- [ ] **3.3** ResultPanel.cs — iOS 캡처 코루틴을 서버 업로드 방식으로 교체
- [ ] **3.4** index.html — (이미 존재하는) `#ios-screenshot-save-link` 확인 (변경 불필요할 가능성 높음)
- [ ] **3.5** 메모리/CORS 설정 검토
- [ ] **3.6** WebGL 빌드 + 동작 테스트

---

## 4. 코드 변경 상세

### 4.1 server.js — 엔드포인트 추가

**위치**: `WinterRoadWebServer/server.js`

기존 코드를 어느 위치에 추가할지 명확히 하기 위해, **express 미들웨어 등록 직후 + WebSocket 설정 이전** 위치 권장.

```javascript
// ========================================
// 스크린샷 임시 저장 (iOS Download Manager 용)
// ========================================
const screenshotStore = new Map();
const SCREENSHOT_TTL_MS = 10 * 60 * 1000; // 10분 후 자동 삭제
const SCREENSHOT_MAX_SIZE = 15 * 1024 * 1024; // 15MB 한도

// 업로드: Base64 받아서 임시 저장
app.post('/api/upload-screenshot',
  express.json({ limit: '20mb' }),
  (req, res) => {
    try {
      if (!req.body || !req.body.image) {
        return res.status(400).json({ error: 'image field required' });
      }

      const buffer = Buffer.from(req.body.image, 'base64');

      if (buffer.length > SCREENSHOT_MAX_SIZE) {
        return res.status(413).json({ error: 'image too large' });
      }

      const id = uuidv4();
      screenshotStore.set(id, {
        buffer,
        createdAt: Date.now()
      });

      console.log(`[Screenshot] Uploaded id=${id}, size=${buffer.length}B, total=${screenshotStore.size}`);

      res.json({ url: `/api/download/${id}.png` });
    } catch (err) {
      console.error('[Screenshot] Upload error:', err.message);
      res.status(500).json({ error: 'upload failed' });
    }
  }
);

// 다운로드: Content-Disposition: attachment 로 응답 (iOS 다운로드 매니저 트리거)
app.get('/api/download/:filename', (req, res) => {
  const id = req.params.filename.replace(/\.png$/, '');
  const entry = screenshotStore.get(id);

  if (!entry) {
    return res.status(404).send('Screenshot expired or not found');
  }

  const downloadName = `WinterRoad_${Date.now()}.png`;

  res.setHeader('Content-Type', 'image/png');
  res.setHeader('Content-Disposition', `attachment; filename="${downloadName}"`);
  res.setHeader('Content-Length', entry.buffer.length);
  res.send(entry.buffer);

  console.log(`[Screenshot] Downloaded id=${id}, name=${downloadName}`);
});

// 주기적 정리 — 10분 초과 항목 삭제
setInterval(() => {
  const now = Date.now();
  let removed = 0;
  for (const [id, entry] of screenshotStore) {
    if (now - entry.createdAt > SCREENSHOT_TTL_MS) {
      screenshotStore.delete(id);
      removed++;
    }
  }
  if (removed > 0) {
    console.log(`[Screenshot] Cleaned up ${removed} expired entries, remaining=${screenshotStore.size}`);
  }
}, 60 * 1000);
```

> **주의**: `express.json({ limit: '20mb' })`는 이 라우트에만 적용됨. 전역 미들웨어로 설정되어 있다면 충돌 가능 — `app.use(express.json(...))` 가 다른 곳에 있는지 확인 후 한 곳으로 통일.

### 4.2 WebGLScreenshot.jslib — 함수 추가

**위치**: `WinterRoadWeb/Assets/Plugins/WebGLScreenshot.jslib`

기존 jslib에 새 함수 추가 (다른 함수 옆에 나란히):

```javascript
// ===========================================
// [iOS 전용 - Download Manager 방식]
// 서버에서 발급된 다운로드 URL을 HTML 링크에 바인딩
// ===========================================
SetDownloadLink_iOS: function(urlPtr) {
  try {
    var url = UTF8ToString(urlPtr);
    var link = document.getElementById("ios-screenshot-save-link");

    if (link) {
      link.href = url;
      link.style.display = "flex";
      console.log("[iOS] Download Manager 링크 활성화:", url);
    } else {
      console.warn("[iOS] #ios-screenshot-save-link 요소를 찾을 수 없습니다");
    }
  } catch (error) {
    console.error("[iOS] 다운로드 링크 설정 실패:", error);
  }
}
```

> 기존 `PrepareScreenshotURL_iOS`(Blob URL 방식)는 **남겨두거나 삭제** — 두 방식 중 어느 것을 쓸지에 따라.
> 권장: 두 함수 모두 남겨두고 ResultPanel.cs 에서 어느 것을 호출할지 분기.

### 4.3 ResultPanel.cs — 코루틴 교체

**위치**: `WinterRoadWeb/Assets/Scripts/ResultPanel.cs`

#### DllImport 추가

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
[DllImport("__Internal")]
private static extern void SetDownloadLink_iOS(string url);
#endif
```

#### 새 코루틴 추가

기존 `PrepareIOSScreenshotCoroutine` 옆에 새 코루틴 추가:

```csharp
/// <summary>
/// [iOS - Download Manager 방식]
/// 캡처 → 서버 업로드 → 발급된 URL을 HTML 링크에 바인딩
/// 사용자가 링크 탭하면 iOS 다운로드 매니저가 Files 앱에 저장
/// </summary>
private IEnumerator PrepareIOSScreenshotCoroutine_DownloadManager()
{
    yield return new WaitForSeconds(1.5f);
    yield return new WaitForEndOfFrame();

    Debug.Log("[iOS-DM] 캡처 시작");

    Texture2D screenshot = null;
    try
    {
        screenshot = ScreenCapture.CaptureScreenshotAsTexture();
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[iOS-DM] 캡처 실패: {e.Message}");
        iosCaptureCoroutine = null;
        yield break;
    }

    if (screenshot == null)
    {
        iosCaptureCoroutine = null;
        yield break;
    }

    byte[] pngData = screenshot.EncodeToPNG();
    string base64 = System.Convert.ToBase64String(pngData);
    Destroy(screenshot);

    // 서버에 업로드
    string jsonPayload = "{\"image\":\"" + base64 + "\"}";
    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

    using (UnityWebRequest www = new UnityWebRequest("/api/upload-screenshot", "POST"))
    {
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[iOS-DM] 업로드 실패: {www.error}");
            iosCaptureCoroutine = null;
            yield break;
        }

        // 응답 파싱: {"url":"/api/download/abc123.png"}
        string response = www.downloadHandler.text;
        var parsed = JsonUtility.FromJson<ScreenshotUploadResponse>(response);

        if (string.IsNullOrEmpty(parsed.url))
        {
            Debug.LogError($"[iOS-DM] 응답 파싱 실패: {response}");
            iosCaptureCoroutine = null;
            yield break;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SetDownloadLink_iOS(parsed.url);
            Debug.Log($"[iOS-DM] 다운로드 링크 활성화: {parsed.url}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[iOS-DM] 링크 설정 오류: {e.Message}");
        }
#endif
    }

    iosCaptureCoroutine = null;
}

[System.Serializable]
private class ScreenshotUploadResponse
{
    public string url;
}
```

#### ShowResults 분기 변경

기존 iOS 분기에서 호출하는 코루틴을 새 것으로 교체:

```csharp
if (IsRuntimeIOS())
{
    captureToScreenButton.gameObject.SetActive(false);

    if (iosCaptureCoroutine != null)
        StopCoroutine(iosCaptureCoroutine);

    // 변경 전: PrepareIOSScreenshotCoroutine() (Blob URL 방식)
    // 변경 후: PrepareIOSScreenshotCoroutine_DownloadManager() (서버 업로드 방식)
    iosCaptureCoroutine = StartCoroutine(PrepareIOSScreenshotCoroutine_DownloadManager());
}
```

#### 정리 로직

`CleanupIOSScreenshotLink()` 는 **그대로 사용 가능**. 기존 `HideScreenshotLink_iOS` 가 단순히 링크를 숨기는 함수이므로 두 방식 모두 호환됨.

### 4.4 index.html — 변경 불필요

이미 `#ios-screenshot-save-link` 요소가 존재하면 그대로 재사용. 변경할 필요 없음.

확인 명령:

```bash
grep -n "ios-screenshot-save-link" \
  WinterRoadWeb/Assets/WebGLTemplates/Ambiens/index.html
```

요소가 없으면 이전 가이드(operation-manual에 없음 — 별도 작업 기록)에서 추가 필요.

### 4.5 CORS 및 서버 경로 주의사항

#### CORS
- WebGL 빌드가 같은 서버에서 호스팅되면 CORS 문제 없음
- 다른 도메인에서 호스팅하면 server.js의 `cors()` 미들웨어가 이미 적용 중 — OK

#### 상대 경로 vs 절대 경로
- ResultPanel.cs 의 `"/api/upload-screenshot"` 는 **상대 경로**
- WebGL 빌드가 같은 서버에서 호스팅되므로 자동으로 같은 origin으로 요청
- 다른 도메인이면 절대 경로 (`https://yetipang.freeddns.org/api/upload-screenshot`) 사용

---

## 5. 테스트 체크리스트

### 5.1 로컬 (Desktop 브라우저로 1차 검증)

- [ ] `cd WinterRoadWebServer && npm start` 로 서버 실행
- [ ] 콘솔에 에러 없이 시작되는지 확인
- [ ] WebGL 빌드 후 브라우저에서 결과 화면까지 진입
- [ ] DevTools Network 탭에서 `/api/upload-screenshot` POST 요청 확인 (200 응답)
- [ ] `/api/download/<id>.png` 응답 헤더에 `Content-Disposition: attachment` 포함 확인

### 5.2 iPhone Safari 실기기 (또는 BrowserStack)

- [ ] 결과 화면 진입 후 1.5초 뒤 "📸 이미지 저장" 링크 표시 확인
- [ ] 링크 탭 → iOS 우상단에 다운로드 진행 표시 나타남
- [ ] 다운로드 완료 후 Files 앱 → 다운로드 폴더에 `WinterRoad_*.png` 존재
- [ ] 이미지가 깨지지 않고 정상 표시

### 5.3 정리 동작

- [ ] 결과창 닫고 다시 열어도 새 ID로 업로드되는지
- [ ] 10분 이상 지난 ID 로 다운로드 시 404 응답
- [ ] 서버 콘솔 로그: `[Screenshot] Cleaned up N expired entries`

---

## 6. 운영 시 고려사항

### 6.1 메모리 사용

- 1장당 1~3MB (FHD PNG 기준)
- 동시 50명이 결과 화면 들어와서 캡처해도 약 150MB 이내
- 10분 TTL 이면 큰 문제 없음

> 메모리 압박이 우려되면 디스크 저장으로 변경 가능 (`fs.writeFile` + 임시 폴더 + 주기적 cleanup).

### 6.2 동시 다운로드

- 같은 ID 로 여러 번 GET 가능 (TTL 안에서)
- 서버 재시작 시 메모리 초기화 → 진행 중이던 다운로드 무효화 (Map → 디스크 변경 시 보존 가능)

### 6.3 보안

- ID는 UUID v4 (추측 불가)
- 직접 다운로드 URL을 SNS에 공유해도 10분 후 만료
- 추가 인증 불필요 (게임 결과 사진이라 민감도 낮음)

---

## 7. 롤백 시나리오

문제 발생 시 **파일 단위 롤백**:

| 파일 | 롤백 방법 |
|---|---|
| server.js | 추가한 엔드포인트 블록 제거 (스크린샷 관련 주석 블록 단위) |
| WebGLScreenshot.jslib | `SetDownloadLink_iOS` 함수만 제거 |
| ResultPanel.cs | `iosCaptureCoroutine` 호출을 기존 `PrepareIOSScreenshotCoroutine` 으로 되돌림 |
| index.html | 변경 없음 (롤백 불필요) |

WebGL은 **재빌드 필수** (jslib + C# 변경 반영).

---

## 8. 다음 단계 (선택)

구현 후 검토 가능한 추가 개선:

1. **공유 옵션 병행 제공**: 사진 앱 저장(꾹 누르기) 링크 + 다운로드 매니저 링크 둘 다 표시
2. **HTTPS 도메인 사용**: `yetipang.freeddns.org` 로 접속 시 보안 컨텍스트 확보
3. **디스크 임시 저장**: 메모리 → 파일 시스템 (`os.tmpdir()`) 으로 이동
4. **다운로드 진행률 UI**: Unity 측에서 진행 상태 표시

---

## 9. 참고 자료

- [iOS Safari Download Manager 동작 (WebKit 공식 블로그)](https://webkit.org/blog/9670/new-webkit-features-in-safari-13/)
- [MDN: Content-Disposition 헤더](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Disposition)
- 프로젝트 내 관련 파일:
  - `WinterRoadWebServer/server.js`
  - `WinterRoadWeb/Assets/Plugins/WebGLScreenshot.jslib`
  - `WinterRoadWeb/Assets/Scripts/ResultPanel.cs`
  - `WinterRoadWeb/Assets/WebGLTemplates/Ambiens/index.html`
