# 지시서 (v2): Android WebView 카메라 회전 시 화면 미표시 문제 해결

> v2: 다른 LLM의 검토 의견 중 타당한 것만 반영해 보완했다.
> 우선순위: **[필수]** 반드시, **[권장]** 가능하면, **[선택]** 시간이 되면.

## 1. 역할

너는 Android(WebView)와 웹(JavaScript)을 모두 다루는 시니어 개발자다.
**기존 프로젝트 코드를 먼저 읽고** 수정해라. 코드를 새로 만들거나 기존 카메라 구현을 임의로 바꾸지 않는다.

## 2. 환경

- Android 앱 (WebView 기반)
- 웹 라이브러리: `html5-qrcode`, `zxing-wasm` (카메라로 QR/바코드 스캔)
- 개발 PC: Windows (명령어는 PowerShell 기준)

## 3. 문제 현상

카메라 스캔 화면에서 가로 ↔ 세로 회전 시, 카메라가 다시 렌더링되면서 **카메라 화면이 표시되지 않는다.**

## 4. 먼저 확인할 것 (원인 분석)

아래를 **코드와 로그로 확인한 뒤**, 실제로 맞는 원인만 고친다.

1. 스캔 화면을 관리하는 Activity가 무엇인지, `AndroidManifest.xml`에 `configChanges`가 이미 있는지
2. 회전 시 Activity가 실제로 재생성되는지 (`onCreate` 재호출 여부를 로그로 확인)
3. `WebChromeClient.onPermissionRequest`가 어떻게 처리되는지
4. 웹 카메라 시작/중지 코드, `html5-qrcode`와 `zxing-wasm`의 실제 사용 여부와 **버전**
5. `width`/`height` 고정값, `qrbox` 고정값, `<video>`의 `playsinline muted autoplay` 속성

## 5. 해야 할 작업

### [필수] 작업 1. 카메라 권한 처리 (보안)

**모든 요청을 무조건 허용(`request.grant(request.resources)`)하지 않는다.**

- 요청한 웹 출처가 **신뢰하는 주소**인지 확인한다.
- 요청에 `RESOURCE_VIDEO_CAPTURE`가 있을 때만, **카메라 권한만** 허용한다.
- 앱의 `CAMERA` 런타임 권한도 따로 확인한다. 없으면 먼저 요청한다.
- 사용자가 거부하면 오류 안내를 보여주고, 앱이 비정상 종료되지 않게 한다.
- 허용/거부 결과를 로그로 남긴다.

```kotlin
override fun onPermissionRequest(request: PermissionRequest) {
    runOnUiThread {
        val trusted = request.origin.toString().startsWith(TRUSTED_ORIGIN) // 프로젝트의 실제 주소로 교체
        val wantsCamera = request.resources.contains(PermissionRequest.RESOURCE_VIDEO_CAPTURE)
        if (!trusted || !wantsCamera) {
            Log.w("QR", "권한 거부: origin=${request.origin}")
            request.deny()
            return@runOnUiThread
        }
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA)
            == PackageManager.PERMISSION_GRANTED) {
            request.grant(arrayOf(PermissionRequest.RESOURCE_VIDEO_CAPTURE))
        } else {
            pendingRequest = request // CAMERA 권한 요청 결과에서 grant 또는 deny
            cameraPermissionLauncher.launch(Manifest.permission.CAMERA)
        }
    }
}
```

### [필수] 작업 2. 회전 요청 중복 실행 방지 (마지막 요청 보존)

재시작 중에 새 회전이 오면 **버리지 말고**, 끝난 뒤 한 번 더 실행해서 **최신 화면 상태**에 맞춘다.
앱(Kotlin)이 부르는 것과 웹 이벤트가 동시에 와도 안전해야 한다.

```javascript
let running = false;
let pending = false;

window.restartCamera = function () {
  if (running) { pending = true; return Promise.resolve(); } // 최신 요청 보존
  running = true;
  return (async () => {
    try {
      do {
        pending = false;
        await stopCamera();
        await waitForStableSize(); // 고정 지연 대신 크기 안정 대기 (작업 4)
        await startCamera();       // 기존 시작 함수
      } while (pending);           // 재시작 중 새 요청이 왔으면 한 번 더
    } catch (e) {
      console.error("카메라 재시작 실패", e); // 빈 catch 금지
    } finally {
      running = false;
    }
  })();
};
```

### [필수] 작업 3. 스캐너 상태 확인 후 중지 (html5-qrcode)

`stop()`을 무조건 부르지 않는다. 스캔 중일 때만 중지하고, **오류는 기록**한다(빈 `catch` 금지).

```javascript
async function stopCamera() {
  try {
    const state = html5QrCode.getState(); // 2=스캔 중, 3=일시정지 (사용 중인 버전에서 확인)
    if (state === 2 || state === 3) await html5QrCode.stop();
  } catch (e) {
    console.error("카메라 중지 실패", e);
  }
  // clear()는 프로젝트의 시작 방식상 필요할 때만 호출한다.
}
```

- 이전 카메라 작업이 **끝난 뒤** 새 카메라를 시작한다.
- 시작에 실패하면 오류를 기록하고 **재시도**할 수 있게 한다.
- `qrbox`는 고정값 대신 함수로 준다.

```javascript
qrbox: (w, h) => {
  const size = Math.floor(Math.min(w, h) * 0.7);
  return { width: size, height: size };
}
```

### [권장] 작업 4. 고정 지연 대신 크기 안정 대기

기기마다 화면 갱신 시간이 다르므로 `400ms` 같은 고정 지연에만 의존하지 않는다.
화면 크기가 안정된 뒤 재시작한다. (최대 대기 시간을 두어 무한 대기를 막는다.)

```javascript
function waitForStableSize(maxMs = 1500) {
  return new Promise((resolve) => {
    const start = performance.now();
    let last = "", stable = 0;
    (function check() {
      const size = innerWidth + "x" + innerHeight;
      stable = size === last ? stable + 1 : 0;
      last = size;
      if (stable >= 3 || performance.now() - start > maxMs) return resolve();
      requestAnimationFrame(check);
    })();
  });
}
```

### [권장] 작업 5. Activity 재생성 방지 + 앱에서 웹 호출

먼저 재생성 여부를 로그로 확인하되, 이 설정은 흔하고 안전하므로 **확인과 동시에 적용해도 된다.**

```xml
<activity
    android:name=".MainActivity"
    android:configChanges="orientation|screenSize|smallestScreenSize|screenLayout|keyboardHidden" />
```

```kotlin
override fun onConfigurationChanged(newConfig: Configuration) {
    super.onConfigurationChanged(newConfig)
    Log.d("QR", "회전 감지: ${newConfig.orientation}") // 실제 호출 여부 확인용
    webView.post {
        webView.evaluateJavascript("window.restartCamera && window.restartCamera()", null)
    }
}
```

- 웹 쪽 `orientationchange` 이벤트도 보조로 걸 수 있다. 중복 실행은 작업 2가 막아 준다.
- `resize` 이벤트는 키보드 등으로도 발생하므로 넣지 않는다.
- `WebView.settings`: `javaScriptEnabled = true`, `mediaPlaybackRequiresUserGesture = false`

### [권장] 작업 6. zxing-wasm을 getUserMedia로 직접 쓰는 경우

QR 인식과 카메라 스트림 관리를 나눠서 다룬다. 순서는 아래와 같다.

1. 기존 **QR 인식 루프를 중단**한다. 이전 인식 작업이 남아 중복 실행되지 않게 한다.
2. 기존 `MediaStream`의 트랙을 모두 `stop()` 하고, `video.srcObject = null`로 비운다.
3. 새 스트림을 얻는다. `width`/`height`는 고정하지 않고 필요하면 `ideal`만 쓴다.
4. `video.play()`가 **성공했는지 확인**한다.
5. QR 인식 루프를 다시 시작한다.

```javascript
video.setAttribute("playsinline", ""); video.muted = true;
stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } });
video.srcObject = stream;
await video.play(); // 실패하면 예외 → 로그 기록 후 재시도
```

## 6. 그래도 해결되지 않을 때 (최후 수단)

스캔 화면만 세로 고정(`android:screenOrientation="portrait"`)한다.
위 작업을 적용하고도 실패했을 때만 쓰고, **적용 전에 이유를 보고**한다.

## 7. 작업 순서

1. 프로젝트 구조, `AndroidManifest.xml`, Activity, WebView 설정 확인
2. 웹 카메라 코드와 라이브러리 버전 확인
3. 실제 원인 분석 (회전 시 Activity 재생성 여부, 권한 흐름을 로그로 확인)
4. **수정할 파일과 이유를 먼저 보고**
5. 수정 코드 제시 → **사용자 확인**
6. 확인 후 GitHub 반영
7. Android 빌드와 실제 폰 테스트

## 8. 작업 금지 사항

- 기존 프로젝트를 확인하지 않고 새 카메라 모듈을 만들지 않는다.
- 기존 카메라 구현을 임의로 교체하지 않는다.
- 권한을 무조건 허용하지 않는다.
- 오류를 **빈 `catch`**로 숨기지 않는다.
- 일정 시간 지연만으로 해결됐다고 판단하지 않는다.
- 실제 폰 테스트 없이 "해결 완료"라고 보고하지 않는다.
- 문제와 관계없는 파일은 수정하지 않는다.
- **사용자 확인 없이 GitHub에 최종 변경사항을 반영하지 않는다.**
- 같은 방법으로 두 번 실패하면 반복하지 말고 다른 원인 가설로 넘어가서 이유를 설명한다.

## 9. 테스트 체크리스트 (실제 폰에서)

| 항목 | 합격 기준 |
|---|---|
| 최초 카메라 실행 | 정상 표시 |
| 세로 → 가로 | 화면 정상 표시 및 QR 인식 |
| 가로 → 세로 | 화면 정상 표시 및 QR 인식 |
| 빠른 연속 회전 | 검은 화면과 중복 스트림 없음, **마지막 방향 기준으로 정상** |
| 권한 거부 | 오류 안내, 비정상 종료 없음 |
| 앱 백그라운드 이동 후 복귀 | 카메라 정상 복구 |
| 스캔 화면 재진입 | 이전 스트림 정리 후 정상 실행 |
| 카메라 재시작 실패 | 오류 로그가 남고 재시도 가능 |
| 반복 스캔 | 카메라 리소스 누수 없음 |

최종 목표는 회전 후 카메라가 다시 보이는 것만이 아니라, **방향 전환 뒤에도 QR/바코드 인식이 계속 정상 동작**하는 것이다.

## 10. 최종 보고 형식

1. **원인**: 실제로 확인된 원인
2. **수정한 파일**: 파일 목록과 수정 내용, 이유
3. **확인한 것**: 빌드 성공 여부, QR 인식과 회전 테스트 결과
4. **확인 못 한 것**: 실제 폰에서만 확인되는 항목
