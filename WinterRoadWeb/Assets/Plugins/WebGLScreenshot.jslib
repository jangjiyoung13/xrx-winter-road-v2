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
  // [iOS 전용 - Download Manager 방식]
  // 사용자 제스처 컨텍스트 안에서 즉시 호출되어야 함.
  // - Unity 캡처 버튼 onClick → C# → 이 함수까지 동기 체인 유지 시 동작
  // - <a> 태그 동적 생성 + click() + 즉시 제거
  // - 서버가 Content-Disposition: attachment 헤더로 응답하면
  //   iOS 다운로드 매니저가 파일 앱(Files)에 저장
  // ===========================================
  TriggerDownload_iOS: function(urlPtr) {
    try {
      var url = UTF8ToString(urlPtr);
      if (!url) {
        console.warn("⚠️ [iOS-DM] 다운로드 URL이 비어있습니다");
        return;
      }

      var link = document.createElement("a");
      link.href = url;
      link.rel = "noopener";
      // download 속성은 같은 origin일 때만 동작. 서버가 Content-Disposition을
      // 내려주므로 속성 없어도 OK이지만 보조적으로 명시.
      link.download = "";
      link.style.display = "none";

      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      console.log("✅ [iOS-DM] 다운로드 트리거:", url);
    } catch (error) {
      console.error("❌ [iOS-DM] 다운로드 트리거 실패:", error);
    }
  },

  // ===========================================
  // [iOS 전용 - 이미지 오버레이 방식]
  // 자동 캡처 + 서버 업로드 완료 시 호출.
  // - <img src=url>로 이미지를 표시 + 안내 문구
  // - 사용자가 이미지를 길게 누르면 iOS 컨텍스트 메뉴에서 "사진에 추가" 가능
  // ===========================================
  ShowImageOverlay_iOS: function(urlPtr, guidePtr) {
    try {
      var url = UTF8ToString(urlPtr);
      var guide = UTF8ToString(guidePtr);

      var overlay = document.getElementById("ios-image-overlay");
      var img = document.getElementById("ios-image-overlay-img");
      var guideEl = document.getElementById("ios-image-overlay-guide");

      if (!overlay || !img || !guideEl) {
        console.warn("⚠️ [iOS-Overlay] 오버레이 요소를 찾을 수 없습니다");
        return;
      }

      img.src = url;
      guideEl.textContent = guide || "";
      overlay.style.display = "flex";

      console.log("✅ [iOS-Overlay] 이미지 오버레이 표시:", url);
    } catch (error) {
      console.error("❌ [iOS-Overlay] 표시 실패:", error);
    }
  },

  // ===========================================
  // [iOS 전용 - 이미지 오버레이 숨김]
  // 결과창 닫힐 때 호출.
  // ===========================================
  HideImageOverlay_iOS: function() {
    try {
      var overlay = document.getElementById("ios-image-overlay");
      var img = document.getElementById("ios-image-overlay-img");

      if (overlay) {
        overlay.style.display = "none";
      }
      if (img) {
        img.src = "";
      }

      console.log("✅ [iOS-Overlay] 이미지 오버레이 숨김");
    } catch (error) {
      console.error("❌ [iOS-Overlay] 숨김 실패:", error);
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
