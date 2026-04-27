mergeInto(LibraryManager.library, {

  SaveToLocalStorageInternal: function (key, value) {
    var keyStr = UTF8ToString(key);
    var valueStr = UTF8ToString(value);
    
    try {
      localStorage.setItem(keyStr, valueStr);
      console.log('LocalStorage saved:', keyStr, '=', valueStr);
    } catch (e) {
      console.error('Failed to save to localStorage:', e);
    }
  },

  GetFromLocalStorageInternal: function (key) {
    var keyStr = UTF8ToString(key);
    
    try {
      var value = localStorage.getItem(keyStr);
      console.log('LocalStorage retrieved:', keyStr, '=', value);
      
      if (value === null) {
        value = "";
      }
      
      var bufferSize = lengthBytesUTF8(value) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(value, buffer, bufferSize);
      return buffer;
    } catch (e) {
      console.error('Failed to retrieve from localStorage:', e);
      var bufferSize = lengthBytesUTF8("") + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8("", buffer, bufferSize);
      return buffer;
    }
  },

  // 진동 기능 - 밀리초 단위
  VibrateInternal: function (milliseconds) {
    try {
      if (navigator.vibrate) {
        var result = navigator.vibrate(milliseconds);
        console.log('Vibration triggered:', milliseconds, 'ms, result:', result);
        return result ? 1 : 0;
      } else {
        console.warn('Vibration API not supported on this device/browser');
        return 0;
      }
    } catch (e) {
      console.error('Failed to trigger vibration:', e);
      return 0;
    }
  },

  // 진동 기능 - 초 단위 (밀리초로 변환)
  VibrateSecondsInternal: function (seconds) {
    var milliseconds = Math.round(seconds * 1000);
    try {
      if (navigator.vibrate) {
        var result = navigator.vibrate(milliseconds);
        console.log('Vibration triggered:', seconds, 'seconds (', milliseconds, 'ms), result:', result);
        return result ? 1 : 0;
      } else {
        console.warn('Vibration API not supported on this device/browser');
        return 0;
      }
    } catch (e) {
      console.error('Failed to trigger vibration:', e);
      return 0;
    }
  },

  // 진동 패턴 기능 - 진동과 멈춤을 번갈아 가며 (밀리초 배열)
  VibratePatternInternal: function (patternPtr, patternLength) {
    try {
      if (navigator.vibrate) {
        var pattern = [];
        for (var i = 0; i < patternLength; i++) {
          pattern.push(HEAP32[(patternPtr >> 2) + i]);
        }
        var result = navigator.vibrate(pattern);
        console.log('Vibration pattern triggered:', pattern, 'result:', result);
        return result ? 1 : 0;
      } else {
        console.warn('Vibration API not supported on this device/browser');
        return 0;
      }
    } catch (e) {
      console.error('Failed to trigger vibration pattern:', e);
      return 0;
    }
  },

  // 진동 중단
  StopVibrationInternal: function () {
    try {
      if (navigator.vibrate) {
        var result = navigator.vibrate(0);
        console.log('Vibration stopped, result:', result);
        return result ? 1 : 0;
      } else {
        console.warn('Vibration API not supported on this device/browser');
        return 0;
      }
    } catch (e) {
      console.error('Failed to stop vibration:', e);
      return 0;
    }
  },

  // 진동 API 지원 여부 확인
  IsVibrationSupportedInternal: function () {
    try {
      var supported = 'vibrate' in navigator;
      console.log('Vibration API supported:', supported);
      return supported ? 1 : 0;
    } catch (e) {
      console.error('Failed to check vibration support:', e);
      return 0;
    }
  },

  // 브라우저 URL에서 WebSocket 서버 주소 추출
  GetServerUrlFromBrowser: function () {
    try {
      var host = window.location.hostname || "localhost";
      var port = window.location.port || "3000";
      var protocol = (window.location.protocol === "https:") ? "wss:" : "ws:";
      var url = protocol + "//" + host + ":" + port;
      console.log("[WebGL] Server URL resolved from browser:", url);

      var bufferSize = lengthBytesUTF8(url) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(url, buffer, bufferSize);
      return buffer;
    } catch (e) {
      console.error("[WebGL] Failed to get server URL:", e);
      var fallback = "ws://localhost:3000";
      var bufferSize2 = lengthBytesUTF8(fallback) + 1;
      var buffer2 = _malloc(bufferSize2);
      stringToUTF8(fallback, buffer2, bufferSize2);
      return buffer2;
    }
  }

});

