(function () {
    
    var loadComplete = false; // 로딩 완료 플래그 (키보드로 인한 화면 밀림 방지)
    
    //UNITY STUFF
    var buildUrl = "Build";
    var loaderUrl = buildUrl + "/UnityWebGLBuild.loader.js";
    var config = {
        dataUrl: buildUrl + "/UnityWebGLBuild.data",
        frameworkUrl: buildUrl + "/UnityWebGLBuild.framework.js",
        codeUrl: buildUrl + "/UnityWebGLBuild.wasm",
        streamingAssetsUrl: "StreamingAssets",
        companyName: "Le Space",
        productName: "Yetipang",
        productVersion: "1.21",
    };



    function iOS() {
        return [
            'iPad Simulator',
            'iPhone Simulator',
            'iPod Simulator',
            'iPad',
            'iPhone',
            'iPod'
        ].includes(navigator.platform)
        // iPad on iOS 13 detection
        || (navigator.userAgent.includes("Mac") && "ontouchend" in document)
    }
    function isFullscreen(){
        return document.fullscreenElement ||
        document.webkitFullscreenElement ||
        document.mozFullScreenElement ||
        document.msFullscreenElement;
    }
    var main_container = document.querySelector("#main-container");
    var container = document.querySelector("#unity-container");
    var canvas = document.querySelector("#unity-canvas");
    var loader= document.querySelector("#loader");
    var loaderFill= document.querySelector("#fill");
    var toggle_fullscreen=document.querySelector("#toggle_fullscreen");

    function onProgress(progress) {
        loaderFill.style.width = progress * 100 + "%";
    }

    function onComplete(unityInstance) {
        loadComplete = true; // 로딩 완료 플래그 설정
        loader.remove();
    }
    var resizeTimeOut;
    function onWindowResize() {
        var width = window.innerWidth
        || document.documentElement.clientWidth
        || document.body.clientWidth;

        var height = window.innerHeight
        || document.documentElement.clientHeight
        || document.body.clientHeight;

        canvas.height=height;
        canvas.width=width;
    }
    function onWindowResizeWithDelay(){
        if(loadComplete === true)
            return; // 로딩 완료 후에는 resize 무시 (키보드로 인한 화면 밀림 방지)
        clearTimeout(resizeTimeOut);
        resizeTimeOut = setTimeout(onWindowResize, 200);
    }


    var script = document.createElement("script");
    script.src = loaderUrl;
    script.onload = () => {
        createUnityInstance(canvas, config, onProgress)
            .then(onComplete)
            .catch((message) => {
                alert(message);
        });
    };
    document.body.appendChild(script);

    window.addEventListener('resize', onWindowResizeWithDelay);
    onWindowResizeWithDelay();


    document.onfullscreenchange = function ( event ) {
        if(iOS()){
            return;
        }

        setTimeout(() => {
            canvas.width=1000;
            onWindowResizeWithDelay();
        }, 200);
    };

    if(iOS()){
        toggle_fullscreen.style.display="none";
    }
    else{
        onfullscreenchange();
    }

})();
