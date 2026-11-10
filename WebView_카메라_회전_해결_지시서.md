# 지시서: Android WebView 카메라 회전 시 화면 미표시 문제 해결

## 1. 역할

너는 Android(WebView)와 웹(JavaScript)을 모두 다루는 시니어 개발자다.
아래 문제를 **기존 프로젝트 코드를 직접 확인한 뒤** 수정해라. 추측으로 코드를 새로 만들지 말고, 먼저 현재 코드를 읽어라.

## 2. 환경

- Android 앱 (WebView 기반)
- 웹 쪽 라이브러리: `html5-qrcode`, `zxing-wasm` (카메라로 QR/바코드 스캔)
- 개발 PC: Windows (명령어는 PowerShell 기준으로 안내)

## 3. 문제 현상

카메라 스캔 화면에서 **가로 → 세로** 또는 **세로 → 가로**로 회전하면, 카메라가 다시 렌더링되면서 **카메라 화면이 표시되지 않는다.**

## 4. 원인 가설 (확인 후 맞는 것만 고칠 것)

1. 회전 시 **Activity가 재생성**되어 WebView, 카메라 권한, 스트림이 초기화됨
2. 회전 후 `<video>` 크기가 바뀌는데 **기존 카메라 스트림이 멈추거나 새 크기와 안 맞음**
3. WebView에서는 웹의 `orientationchange` 이벤트가 **불안정**함
4. 카메라를 다시 켤 때 **권한 요청(`onPermissionRequest`)을 grant하지 않아** 회전 후에만 검은 화면이 됨
5. 시작 함수에서 `width`/`height`를 고정값으로 줘서 방향이 바뀌면 비율이 안 맞음
6. `html5-qrcode`의 `qrbox`가 고정값이라 회전 후 스캔 영역이 어긋남

## 5. 해야 할 작업

### 작업 1. AndroidManifest.xml: Activity 재생성 막기

스캔 화면을 담은 Activity에 아래 속성을 추가한다. (이미 있으면 빠진 값만 추가)

```xml
<activity
    android:name=".MainActivity"
    android:configChanges="orientation|screenSize|smallestScreenSize|screenLayout|keyboardHidden" />
```

### 작업 2. Kotlin(또는 Java): 회전하면 웹에 알려주기

회전 감지는 웹 이벤트에 의존하지 말고 앱이 직접 웹 함수를 호출한다.

```kotlin
override fun onConfigurationChanged(newConfig: Configuration) {
    super.onConfigurationChanged(newConfig)
    webView.postDelayed({
        webView.evaluateJavascript("window.restartCamera && window.restartCamera()", null)
    }, 400) // 회전 후 크기가 확정되기를 기다리는 시간
}
```

- 프로젝트가 Java라면 같은 로직을 Java로 작성한다.
- 400ms는 시작값이다. 안 되면 600~800ms로 늘려서 테스트한다.

### 작업 3. WebView 설정 확인

```kotlin
webView.settings.javaScriptEnabled = true
webView.settings.mediaPlaybackRequiresUserGesture = false

webView.webChromeClient = object : WebChromeClient() {
    override fun onPermissionRequest(request: PermissionRequest) {
        runOnUiThread { request.grant(request.resources) }
    }
}
```

- 이미 같은 설정이 있으면 **중복 추가하지 말고 그대로 둔다.**
- 앱 자체의 `CAMERA` 런타임 권한도 이미 허용돼 있는지 확인한다.

### 작업 4. 웹(JS): restartCamera 전역 함수 만들기

기존 카메라 시작 함수 이름을 코드에서 찾아서 그것을 호출하도록 연결한다. (아래 `startCamera`는 예시 이름)

**html5-qrcode 사용 시**

```javascript
let restarting = false;

window.restartCamera = async function () {
  if (restarting) return;          // 중복 실행 방지
  restarting = true;
  try {
    try {
      await html5QrCode.stop();
      html5QrCode.clear();
    } catch (e) {}
    await startCamera();           // 기존 카메라 시작 함수
  } finally {
    restarting = false;
  }
};
```

**zxing-wasm을 getUserMedia로 직접 쓰는 경우**

```javascript
window.restartCamera = async function () {
  // 스캔 루프 중단(프로젝트의 루프 중단 방식에 맞게 수정)
  if (stream) stream.getTracks().forEach(t => t.stop());
  video.srcObject = null;

  stream = await navigator.mediaDevices.getUserMedia({
    video: { facingMode: "environment" }   // width/height 고정 금지
  });
  video.srcObject = stream;
  await video.play();
  // 스캔 루프 재시작
};
```

**공통 주의사항**

- `window.restartCamera`처럼 **window에 붙여야** 앱(Kotlin)에서 호출할 수 있다.
- `width`/`height`를 고정값으로 주지 않는다. 필요하면 `ideal`만 사용한다.
- `<video>` 태그에 `playsinline muted autoplay`가 있는지 확인한다.
- `html5-qrcode`의 `qrbox`는 고정값 대신 함수로 바꾼다.

```javascript
qrbox: (w, h) => {
  const size = Math.floor(Math.min(w, h) * 0.7);
  return { width: size, height: size };
}
```

### 작업 5. (보조) 웹 쪽 이벤트도 같이 걸기

앱이 호출하지 않는 환경(일반 브라우저 테스트)에서도 동작하도록 보조로 추가한다.

```javascript
let rotateTimer = null;
function onRotate() {
  clearTimeout(rotateTimer);
  rotateTimer = setTimeout(() => window.restartCamera(), 400);
}
window.addEventListener("orientationchange", onRotate);
```

- `restartCamera` 안의 `restarting` 플래그가 중복 실행을 막아 주므로, 앱 호출과 웹 이벤트가 동시에 와도 안전하다.
- `resize` 이벤트는 키보드 등으로도 발생하므로 **넣지 않는다.**

## 6. 그래도 해결되지 않을 때 (최후 수단)

스캔 화면만 세로로 고정한다.

```xml
<activity android:name=".ScanActivity" android:screenOrientation="portrait" />
```

이 방법은 **먼저 1~5번을 적용해 보고도 실패했을 때만** 쓴다. 적용 전에 반드시 이유를 보고한다.

## 7. 지켜야 할 규칙

- 먼저 현재 코드를 읽고, **바꾸는 파일과 이유를 요약**한 뒤 수정한다.
- 이미 있는 코드는 **중복으로 추가하지 않는다.**
- 문제와 관련 없는 코드는 수정하지 않는다.
- 같은 방법으로 두 번 실패하면 반복하지 말고 **다른 원인 가설로 넘어가서** 그 이유를 설명한다.
- 코드를 GitHub에 올리기 전에는 **변경 내용을 먼저 보여주고 확인을 받는다.**

## 8. 테스트 체크리스트

실제 폰(에뮬레이터 말고)에서 아래를 확인한다.

- [ ] 세로에서 카메라 켜짐
- [ ] 세로 → 가로 회전 후 카메라가 다시 표시됨
- [ ] 가로 → 세로 회전 후 카메라가 다시 표시됨
- [ ] 회전을 빠르게 여러 번 반복해도 검은 화면이 안 됨
- [ ] 회전 후 QR/바코드가 정상 인식됨
- [ ] 회전 후 카메라 권한 팝업이 다시 뜨지 않거나, 떠도 정상 허용됨
- [ ] 앱을 백그라운드로 보냈다가 돌아와도 카메라가 켜짐

## 9. 최종 보고 형식

작업이 끝나면 아래 형식으로 보고한다.

1. **원인**: 실제로 확인된 원인 (가설 중 어느 것이었는지)
2. **수정한 파일**: 파일명과 바꾼 내용 한 줄 요약
3. **확인한 것**: 실제로 테스트해서 확인된 항목
4. **확인 못 한 것**: 실제 폰에서만 확인 가능한 항목 (사용자가 직접 확인해야 하는 것)
