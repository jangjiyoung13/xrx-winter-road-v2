mergeInto(LibraryManager.library, {

  // ===========================================
  // 공통: iOS 플랫폼 감지 (Unity C#에서 호출)
  // 반환값: 1 = iOS, 0 = 그 외
  // ===========================================
  IsIOSPlatform: function() {
    var isIOS = ['iPad Simulator', 'iPhone Simulator', 'iPod Simulator',
                 'iPad', 'iPhone', 'iPod'].includes(navigator.platform)
      || (navigator.userAgent.includes("Mac") && "ontouchend" in document);
    return isIOS ? 1 : 0;
  },

  // ===========================================
  // [iOS 전용] 스크린샷 URL 사전 생성 및 HTML 링크에 바인딩
  // - Unity 버튼이 아닌 HTML <a> 태그로 Safari의 사용자 제스처 차단 회피
  // - 결과창 표시 시 자동으로 호출되어 저장 링크를 준비
  // ===========================================
  PrepareScreenshotURL_iOS: function(base64Ptr) {
    try {
      var base64 = UTF8ToString(base64Ptr);

      var byteCharacters = atob(base64);
      var byteNumbers = new Array(byteCharacters.length);
      for (var i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
      }
      var byteArray = new Uint8Array(byteNumbers);
      var blob = new Blob([byteArray], { type: "image/png" });

      // 이전 Blob URL 정리 (메모리 누수 방지)
      if (window._wrScreenshotUrl) {
        URL.revokeObjectURL(window._wrScreenshotUrl);
      }

      var url = URL.createObjectURL(blob);
      window._wrScreenshotUrl = url;

      // HTML 오버레이 링크의 href 업데이트 및 표시
      var link = document.getElementById("ios-screenshot-save-link");
      if (link) {
        link.href = url;
        link.style.display = "flex";
        console.log("✅ [iOS] 스크린샷 저장 링크 활성화");
      } else {
        console.warn("⚠️ [iOS] #ios-screenshot-save-link 요소를 찾을 수 없습니다. index.html을 확인하세요.");
      }
    } catch (error) {
      console.error("❌ [iOS] 스크린샷 URL 준비 실패:", error);
    }
  },

  // ===========================================
  // [iOS 전용] 저장 링크 숨기기 및 Blob URL 정리
  // - 결과창 닫을 때 호출
  // ===========================================
  HideScreenshotLink_iOS: function() {
    try {
      var link = document.getElementById("ios-screenshot-save-link");
      if (link) {
        link.style.display = "none";
        link.href = "";
      }
      if (window._wrScreenshotUrl) {
        URL.revokeObjectURL(window._wrScreenshotUrl);
        window._wrScreenshotUrl = null;
      }
      console.log("🔒 [iOS] 저장 링크 숨김 및 URL 정리 완료");
    } catch (error) {
      console.error("❌ [iOS] 저장 링크 정리 실패:", error);
    }
  },

  // ===========================================
  // [Android / Desktop 전용] <a download> 방식 (기존 로직 유지)
  // - 버튼 클릭 즉시 파일 다운로드
  // ===========================================
  DownloadScreenshot: function(base64Ptr) {
    try {
      var base64 = UTF8ToString(base64Ptr);

      // Base64 → Blob 변환
      var byteCharacters = atob(base64);
      var byteNumbers = new Array(byteCharacters.length);
      for (var i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
      }
      var byteArray = new Uint8Array(byteNumbers);
      var blob = new Blob([byteArray], { type: "image/png" });

      // 타임스탬프를 포함한 파일명 생성
      var timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);
      var fileName = "WinterRoad_" + timestamp + ".png";

      console.log("📱 [Android/Desktop] <a download> 방식으로 저장 시작");

      var url = URL.createObjectURL(blob);
      var link = document.createElement("a");
      link.href = url;
      link.download = fileName;

      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      // 메모리 정리
      setTimeout(function() {
        URL.revokeObjectURL(url);
      }, 100);

      console.log("✅ [Android/Desktop] 스크린샷 다운로드 완료");
    } catch (error) {
      console.error("❌ [Android/Desktop] 스크린샷 다운로드 실패:", error);
    }
  }

});
