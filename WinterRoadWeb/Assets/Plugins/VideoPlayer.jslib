mergeInto(LibraryManager.library, {
    CreateVideo: function (id, urlPtr) {
        var url = UTF8ToString(urlPtr);

        // 기존 영상 제거
        var oldVideo = document.getElementById(id);
        if (oldVideo) {
            oldVideo.parentNode.removeChild(oldVideo);
        }

        // 새로운 video 태그 생성
        var video = document.createElement('video');
        video.id = id;
        video.src = url;
        video.autoplay = true;
        video.controls = true;
        video.muted = true; // 모바일 자동재생을 위해 mute 필요
        video.playsInline = true; // iOS용 인라인 재생
        video.style.position = "absolute";
        video.style.top = "0px";
        video.style.left = "0px";
        video.style.width = "100%";
        video.style.height = "100%";
        video.style.zIndex = "9999"; // Unity 캔버스 위로

        document.body.appendChild(video);
    },

    RemoveVideo: function (id) {
        var video = document.getElementById(UTF8ToString(id));
        if (video) {
            video.parentNode.removeChild(video);
        }
    }
});