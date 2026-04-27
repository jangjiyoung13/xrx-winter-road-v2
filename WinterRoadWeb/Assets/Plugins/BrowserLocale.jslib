mergeInto(LibraryManager.library, {

  // 브라우저의 기본 언어 코드를 반환 (예: "ko", "en", "ja", "zh-CN", "zh-TW")
  GetBrowserLanguageInternal: function () {
    try {
      var lang = navigator.language || navigator.userLanguage || "en";
      console.log("[WebGL] Browser language detected:", lang);

      var bufferSize = lengthBytesUTF8(lang) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(lang, buffer, bufferSize);
      return buffer;
    } catch (e) {
      console.error("[WebGL] Failed to detect browser language:", e);
      var fallback = "en";
      var bufferSize2 = lengthBytesUTF8(fallback) + 1;
      var buffer2 = _malloc(bufferSize2);
      stringToUTF8(fallback, buffer2, bufferSize2);
      return buffer2;
    }
  }

});
