



# 지시서 (v3): WebView 회전 후 카메라가 다시 표시되지 않는 문제

> 범위는 **이 버그 하나**로 제한한다. 권한 구조 개선 등 다른 개선은 이번 작업에서 하지 않는다.

## 1. 환경과 문제

- Android 앱(**Java**, WebView) + 웹(`html5-qrcode` 또는 `zxing-wasm`으로 QR/바코드 스캔)
- 가로 ↔ 세로 회전 시 카메라가 다시 렌더링되면서 **화면이 표시되지 않는다.**

## 2. 먼저 할 일

기존 코드를 읽는다. 새 카메라 모듈을 만들거나 기존 구현을 바꾸지 않는다.
확인할 것: ① 스캔 화면을 가진 Activity ② `AndroidManifest.xml`의 `configChanges` ③ 웹의 카메라 시작/중지 함수 이름 ④ 사용 중인 라이브러리와 버전

## 3. 수정은 3가지만

### 작업 1. AndroidManifest.xml: 회전해도 Activity가 다시 만들어지지 않게

```xml
<activity
    android:name=".MainActivity"
        android:configChanges="orientation|screenSize|smallestScreenSize|screenLayout|keyboardHidden" />
        ```

        이미 있으면 빠진 값만 추가한다.

        ### 작업 2. Java: 회전하면 웹의 `restartCamera()`를 호출

        ```java
        @Override
        public void onConfigurationChanged(Configuration newConfig) {
                super.onConfigurationChanged(newConfig);
                    webView.post(() -> webView.evaluateJavascript(
                                "window.restartCamera && window.restartCamera()", null));
        }
        ```

        ### 작업 3. 웹(JS): `restartCamera()` 전역 함수

        카메라를 끄고 → 화면 크기가 안정되면 → 다시 켠다.
        재시작 중에 회전이 또 오면 **버리지 말고** 끝난 뒤 한 번 더 실행해서 마지막 방향에 맞춘다.

        ```javascript
        let running = false, pending = false;

        window.restartCamera = function () {
              if (running) { pending = true; return Promise.resolve(); }
                running = true;
                  return (async () => {
                        try {
                                  do {
                                            pending = false;
                                                    await stopCamera();
                                                            await waitForStableSize();
                                                                    await startCamera();            // 기존 시작 함수 (이름은 프로젝트에 맞게)
                                  } while (pending);
                        } catch (e) {
                                  console.error("카메라 재시작 실패", e);
                        } finally {
                                  running = false;
                        }
                  })();
        };

        function waitForStableSize(maxMs = 1500) {  // 고정 지연 대신 크기가 안정될 때까지
          return new Promise((resolve) => {
                const t0 = performance.now(); let last = "", same = 0;
                    (function check() {
                              const s = innerWidth + "x" + innerHeight;
                                    same = s === last ? same + 1 : 0; last = s;
                                          if (same >= 3 || performance.now() - t0 > maxMs) return resolve();
                                                requestAnimationFrame(check);
                    })();
          });
        }
        ```

        `stopCamera()`는 사용하는 라이브러리에 맞게 쓴다.

        - **html5-qrcode**: 스캔 중일 때만 `stop()`을 호출하고, 오류는 `console.error`로 기록한다. (빈 `catch` 금지)
          ```javascript
            async function stopCamera() {
                    try {
                              const st = html5QrCode.getState(); // 2=스캔 중, 3=일시정지 (버전에서 확인)
                                    if (st === 2 || st === 3) await html5QrCode.stop();
                    } catch (e) { console.error("카메라 중지 실패", e); }
            }
              ```
              - **zxing-wasm (getUserMedia 직접 사용)**: 인식 루프를 멈추고 → 스트림 트랙을 모두 `stop()` → `video.srcObject = null`. 시작할 때 `video.play()`가 성공했는지 확인한다.

              ## 4. 같이 확인할 것 (바꾸라는 뜻이 아님)

              - `<video>`에 `playsinline muted autoplay`가 있는지
              - `width`/`height`를 고정하지 않았는지 (필요하면 `ideal`만)
              - html5-qrcode의 `qrbox`가 고정값이면 함수로 바꾼다.
                ```javascript
                  qrbox: (w, h) => { const s = Math.floor(Math.min(w, h) * 0.7); return { width: s, height: s }; }
                    ```
                    - 현재 카메라 권한이 정상 동작하면 **권한 코드는 건드리지 않는다.**

                    ## 5. 그래도 안 될 때

                    스캔 화면만 세로 고정(`android:screenOrientation="portrait"`)한다. 먼저 이유를 보고한다.

                    ## 6. 규칙

                    - 오류를 빈 `catch`로 숨기지 않는다.
                    - 문제와 관계없는 파일은 수정하지 않는다.
                    - 같은 방법으로 두 번 실패하면 반복하지 말고 다른 원인으로 넘어간다.
                    - **GitHub에 반영하기 전에 변경 내용을 먼저 보여주고 확인받는다.**
                    - 실제 폰 테스트 없이 "해결 완료"라고 하지 않는다.

                    ## 7. 테스트 (실제 폰)

                    - [ ] 세로 → 가로 후 카메라 표시, QR 인식
                    - [ ] 가로 → 세로 후 카메라 표시, QR 인식
                    - [ ] 빠르게 여러 번 회전해도 마지막 방향 기준으로 정상
                    - [ ] 앱을 백그라운드로 보냈다가 돌아와도 카메라 정상

                    ## 8. 최종 보고

                    1. 확인된 원인
                    2. 수정한 파일과 이유
                    3. 확인한 것
                    4. 실제 폰에서만 확인되는 것
                    
                                  }
                        }
                  }
                    ))
        }