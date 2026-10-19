# 블루투스 스캐너 연결 상태를 웹 화면에 전달하기 (Java 버전)

대상 스캐너

- **Zebra DS8178** : HID 방식
- **하디스 SM530** : HOG 방식 (저전력 블루투스)

> 이 가이드는 **새 프로젝트를 만들지 않습니다.**
> 이미 있는 안드로이드 앱(WebView가 있는 Activity)에 **코드를 하나씩 붙여 넣는 순서**로 구성했습니다.

---

## 0. 먼저 알아두기 (생각의 흐름)

두 스캐너는 안드로이드 입장에서 **"블루투스 키보드"** 입니다.

1. 스캐너가 연결되면 → 안드로이드의 **입력 장치 목록에 키보드가 하나 생깁니다.**
2. 스캐너가 끊어지면 → 그 키보드가 **목록에서 사라집니다.**
3. 그래서 앱이 "입력 장치가 생기고 사라지는 것"을 지켜보면 연결 상태를 알 수 있습니다.
4. 블루투스 연결/해제 알림도 같이 받아서 한 번 더 확인합니다. (보조 신호)
5. 상태가 바뀌면 앱이 **웹 화면(JS)으로 값을 보내 줍니다.**

```
[스캐너] ──블루투스──▶ [안드로이드 앱(Java)] ──JS 이벤트──▶ [웹 화면]
                          상태 감지                         화면에 표시
```

웹 화면(JS)은 블루투스를 직접 볼 수 없습니다. **반드시 앱이 대신 알려줘야 합니다.**

### 이 가이드의 한계 (꼭 읽어 주세요)

- 제가 실제 단말에서 실행해 보지 못했습니다. **문법과 구조는 검토했지만, 단말 테스트는 직접 하셔야 합니다.**
- 스캐너가 단말에 **어떤 이름으로 보이는지는 제가 알 수 없습니다.** 그래서 7단계에서 이름을 직접 확인하는 과정이 있습니다.
- SM530(HOG)은 절전 때문에 **연결이 잠깐 끊겼다 다시 붙을 수 있습니다.** 그 대비로 "3초 기다렸다가 끊김으로 판단"하는 설정을 넣었습니다. 실제로 그런지는 테스트해서 확인이 필요합니다.

---

## 준비물 확인

- Android Studio 에 열려 있는 **기존 앱 프로젝트** (WebView 가 있는 Activity)
- 이 가이드에서는 Activity 이름을 `MainActivity`, WebView 변수 이름을 `webView` 라고 가정합니다.
  → 본인 코드와 이름이 다르면 **본인 이름으로 바꿔서** 붙여 넣으세요.
- DS8178, SM530 이 단말에 **이미 페어링(등록)** 되어 있으면 좋습니다.

---

## 1단계. 블루투스 권한 추가하기

1. Android Studio 왼쪽 **Project** 창에서 `app` → `manifests` → **`AndroidManifest.xml`** 을 더블클릭합니다.
2. `<manifest ...>` 로 시작하는 줄 **바로 아래**, `<application` 줄 **위**에 아래 내용을 붙여 넣습니다.

```xml
<!-- 안드로이드 11 이하 -->
<uses-permission android:name="android.permission.BLUETOOTH" android:maxSdkVersion="30" />

<!-- 안드로이드 12 이상: 연결된 장치 이름/상태 확인용 -->
<uses-permission android:name="android.permission.BLUETOOTH_CONNECT" />
```

**확인:** 붙여 넣은 줄에 빨간 밑줄이 없으면 성공입니다.

---

## 2단계. 연결 상태 감지 클래스 만들기

### 2-1. 새 Java 파일 만들기

1. 왼쪽 **Project** 창에서 `app` → `java` 를 펼칩니다.
2. 본인 패키지 이름(예: `com.example.myapp`)에 **마우스 오른쪽 버튼**을 누릅니다.
   - 패키지가 여러 개 보이면 `(androidTest)`, `(test)` 라고 적히지 않은 것을 고르세요.
3. **New** → **Java Class** 를 누릅니다.
4. 이름 칸에 `ScannerConnectionMonitor` 를 입력하고 **Enter** 를 칩니다.

### 2-2. 코드 붙여 넣기

1. 열린 파일에서 **Ctrl + A** (전체 선택) 후 아래 코드로 교체합니다.
2. **맨 윗줄의 `package ...;` 는 본인 패키지 이름으로 바꿔야 합니다.** (기존 파일에 있던 줄을 그대로 쓰면 됩니다)

```java
package com.example.myapp; // ★ 본인 패키지 이름으로 바꾸세요 (새 파일 첫 줄 그대로 두면 됩니다)

import android.annotation.SuppressLint;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothManager;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.hardware.input.InputManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.view.InputDevice;

import androidx.core.content.ContextCompat;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * HID / HOG 방식 블루투스 스캐너의 연결 상태를 감지합니다.
 *
 * 1) 스캐너가 연결되면 안드로이드 "입력 장치(키보드)" 목록에 나타나고, 끊어지면 사라집니다.
 * 2) 블루투스 연결/해제 알림(ACL)도 보조 신호로 받습니다.
 * 3) 둘 중 하나라도 "연결됨"이면 연결됨으로 판단합니다.
 * 4) 상태가 바뀔 때만 Listener 로 JSON 문자열을 알려 줍니다.
 */
public class ScannerConnectionMonitor {

    /** 감시할 스캐너 한 대 */
    public static class Target {
        final String key;         // 웹에서 구분할 이름 (예: "DS8178")
        final String label;       // 화면에 보여줄 이름
        final String nameKeyword; // 장치 이름에 이 글자가 들어 있으면 같은 장치로 봄
        final long graceMs;       // 끊김으로 판단하기 전에 기다릴 시간(ms). 0이면 바로 판단

        public Target(String key, String label, String nameKeyword, long graceMs) {
            this.key = key;
            this.label = label;
            this.nameKeyword = nameKeyword;
            this.graceMs = graceMs;
        }
    }

    /** 상태가 바뀌었을 때 받을 곳 */
    public interface Listener {
        void onChanged(String json);
    }

    private final Context context;
    private final List<Target> targets;
    private final Listener listener;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final InputManager inputManager;
    private final BluetoothAdapter adapter;

    private final Map<String, String> states = new HashMap<>();       // key -> connected / disconnected
    private final Map<String, Boolean> paired = new HashMap<>();       // key -> 페어링 여부
    private final Map<String, Boolean> aclConnected = new HashMap<>(); // key -> 블루투스 알림 기준 연결 여부
    private final Set<String> holding = new HashSet<>();               // 끊김 유예 중인 장치
    private boolean started = false;
    private volatile String lastJson = "{}";

    public ScannerConnectionMonitor(Context context, List<Target> targets, Listener listener) {
        this.context = context;
        this.targets = targets;
        this.listener = listener;
        this.inputManager = (InputManager) context.getSystemService(Context.INPUT_SERVICE);
        BluetoothManager bm = (BluetoothManager) context.getSystemService(Context.BLUETOOTH_SERVICE);
        this.adapter = (bm != null) ? bm.getAdapter() : null;
    }

    /** 가장 최근 상태 (웹이 "지금 상태"를 물어볼 때 사용) */
    public String getLastJson() {
        return lastJson;
    }

    // ---------- 입력 장치가 생기고 사라지는 것을 지켜보는 부분 ----------
    private final InputManager.InputDeviceListener inputListener =
            new InputManager.InputDeviceListener() {
                @Override public void onInputDeviceAdded(int deviceId) { refresh(null, false); }
                @Override public void onInputDeviceRemoved(int deviceId) { refresh(null, false); }
                @Override public void onInputDeviceChanged(int deviceId) { refresh(null, false); }
            };

    // ---------- 블루투스 연결/해제 알림을 받는 부분 ----------
    private final BroadcastReceiver aclReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context c, Intent intent) {
            // 블루투스를 켜거나 끌 때: 상태를 바로 다시 계산해서 알림
            if (BluetoothAdapter.ACTION_STATE_CHANGED.equals(intent.getAction())) {
                refresh(null, true);
                return;
            }
            String name = safeName(extractDevice(intent));
            boolean connected = BluetoothDevice.ACTION_ACL_CONNECTED.equals(intent.getAction());
            if (name != null) {
                for (Target t : targets) {
                    if (matches(name, t)) aclConnected.put(t.key, connected);
                }
            }
            refresh(null, false);
        }
    };

    public boolean hasBtPermission() {
        return Build.VERSION.SDK_INT < 31
                || ContextCompat.checkSelfPermission(context, "android.permission.BLUETOOTH_CONNECT")
                == PackageManager.PERMISSION_GRANTED;
    }

    public void start() {
        if (started) return;
        started = true;
        inputManager.registerInputDeviceListener(inputListener, handler);

        IntentFilter filter = new IntentFilter();
        filter.addAction(BluetoothDevice.ACTION_ACL_CONNECTED);
        filter.addAction(BluetoothDevice.ACTION_ACL_DISCONNECTED);
        filter.addAction(BluetoothAdapter.ACTION_STATE_CHANGED); // 블루투스 켜짐/꺼짐
        ContextCompat.registerReceiver(context, aclReceiver, filter, ContextCompat.RECEIVER_EXPORTED);

        forceRefresh();
    }

    public void stop() {
        if (!started) return;
        started = false;
        inputManager.unregisterInputDeviceListener(inputListener);
        try {
            context.unregisterReceiver(aclReceiver);
        } catch (IllegalArgumentException ignored) {
        }
        handler.removeCallbacksAndMessages(null);
    }

    /** 바뀐 게 없어도 현재 상태를 다시 한 번 알려 줍니다. */
    public void forceRefresh() {
        refresh(null, true);
    }

    // ---------- 상태 계산 ----------
    private void refresh(String forceKey, boolean emitAlways) {
        List<String> inputNames = inputDeviceNames();
        List<String> bondedNames = bondedDeviceNames();
        boolean changed = emitAlways;

        for (final Target t : targets) {
            // 1) 지금 "연결된 것처럼 보이는지"
            boolean raw = Boolean.TRUE.equals(aclConnected.get(t.key));
            if (!raw) {
                for (String n : inputNames) {
                    if (matches(n, t)) { raw = true; break; }
                }
            }

            // 2) 유예 시간을 반영해서 최종 상태 결정
            String prev = states.get(t.key);
            String next;
            if (raw) {
                holding.remove(t.key);
                next = "connected";
            } else if ("connected".equals(prev) && t.graceMs > 0 && !t.key.equals(forceKey)) {
                // 방금 끊긴 것처럼 보이면 graceMs 동안은 "연결됨"으로 두고 다시 확인
                if (holding.add(t.key)) {
                    handler.postDelayed(new Runnable() {
                        @Override public void run() {
                            holding.remove(t.key);
                            refresh(t.key, false);
                        }
                    }, t.graceMs);
                }
                next = "connected";
            } else {
                holding.remove(t.key);
                next = "disconnected";
            }

            // 3) 바뀐 게 있는지 비교
            boolean nowPaired = false;
            for (String n : bondedNames) {
                if (matches(n, t)) { nowPaired = true; break; }
            }
            Boolean prevPaired = paired.get(t.key);
            if (!next.equals(prev) || prevPaired == null || prevPaired != nowPaired) changed = true;

            states.put(t.key, next);
            paired.put(t.key, nowPaired);
        }

        if (changed) {
            lastJson = buildJson();
            listener.onChanged(lastJson);
        }
    }

    private String buildJson() {
        try {
            JSONArray arr = new JSONArray();
            for (Target t : targets) {
                JSONObject o = new JSONObject();
                o.put("key", t.key);
                o.put("label", t.label);
                o.put("state", states.containsKey(t.key) ? states.get(t.key) : "unknown");
                o.put("paired", hasBtPermission()
                        ? (Object) Boolean.TRUE.equals(paired.get(t.key))
                        : JSONObject.NULL);
                arr.put(o);
            }
            JSONObject root = new JSONObject();
            root.put("updatedAt", System.currentTimeMillis());
            root.put("btPermission", hasBtPermission());
            root.put("bluetoothEnabled", adapter != null && adapter.isEnabled());
            root.put("scanners", arr);
            return root.toString();
        } catch (JSONException e) {
            return "{}";
        }
    }

    /**
     * 장치 이름 확인용.
     * 지금 단말에 보이는 입력 장치와 페어링된 장치 이름을 모두 보여 줍니다.
     * 스캐너 이름이 예상과 다를 때 nameKeyword 를 정하는 데 씁니다.
     */
    public String dumpDevices() {
        try {
            JSONArray inputs = new JSONArray();
            for (int id : InputDevice.getDeviceIds()) {
                InputDevice d = InputDevice.getDevice(id);
                if (d == null) continue;
                JSONObject o = new JSONObject();
                o.put("name", d.getName());
                o.put("vendorId", d.getVendorId());
                o.put("productId", d.getProductId());
                inputs.put(o);
            }
            JSONArray bonded = new JSONArray();
            for (String n : bondedDeviceNames()) bonded.put(n);

            JSONObject root = new JSONObject();
            root.put("btPermission", hasBtPermission());
            root.put("inputDevices", inputs);
            root.put("bondedDevices", bonded);
            return root.toString();
        } catch (JSONException e) {
            return "{}";
        }
    }

    // ---------- 도우미 ----------
    private boolean matches(String name, Target t) {
        return name != null && name.toLowerCase().contains(t.nameKeyword.toLowerCase());
    }

    private List<String> inputDeviceNames() {
        List<String> list = new ArrayList<>();
        for (int id : InputDevice.getDeviceIds()) {
            InputDevice d = InputDevice.getDevice(id);
            if (d != null && d.getName() != null) list.add(d.getName());
        }
        return list;
    }

    @SuppressLint("MissingPermission")
    private List<String> bondedDeviceNames() {
        List<String> list = new ArrayList<>();
        try {
            if (!hasBtPermission() || adapter == null) return list;
            Set<BluetoothDevice> set = adapter.getBondedDevices();
            if (set != null) {
                for (BluetoothDevice d : set) {
                    String n = d.getName();
                    if (n != null) list.add(n);
                }
            }
        } catch (SecurityException ignored) {
        }
        return list;
    }

    @SuppressLint("MissingPermission")
    private String safeName(BluetoothDevice d) {
        try {
            return (hasBtPermission() && d != null) ? d.getName() : null;
        } catch (SecurityException e) {
            return null;
        }
    }

    @SuppressWarnings("deprecation")
    private BluetoothDevice extractDevice(Intent intent) {
        if (Build.VERSION.SDK_INT >= 33) {
            return intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE, BluetoothDevice.class);
        }
        return intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE);
    }
}
```

**확인:** 파일에 빨간 글씨가 없어야 합니다.
빨간 글씨가 있다면 해당 단어에 커서를 두고 **Alt + Enter** → **Import class** 를 누르세요.

> `androidx.core` 관련 빨간 글씨가 계속 나오면 `app/build.gradle` 의 dependencies 에
> `implementation 'androidx.core:core:1.12.0'` 한 줄을 추가하고 상단의 **Sync Now** 를 누르세요.
> (이미 기본으로 들어 있는 경우가 많습니다.)

---

## 3단계. Activity(MainActivity)에 코드 붙여 넣기

`MainActivity.java` 를 엽니다. 아래 **3-1 ~ 3-6** 을 순서대로 하나씩 하세요.

### 3-1. 맨 위 import 추가

기존 `import ...;` 줄들 아래에 **없는 것만** 추가합니다.

```java
import android.Manifest;
import android.content.Intent;
import android.os.Build;
import android.provider.Settings;
import android.webkit.JavascriptInterface;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;

import org.json.JSONObject;

import java.util.Arrays;
import java.util.List;
```

### 3-2. 클래스 안에 변수 추가

`public class MainActivity ... {` 바로 아래에 붙여 넣습니다.

```java
    // 스캐너 연결 상태 감지기
    private ScannerConnectionMonitor monitor;

    // 블루투스 권한 요청 창 (안드로이드 12 이상)
    private final ActivityResultLauncher<String> btPermissionLauncher =
            registerForActivityResult(new ActivityResultContracts.RequestPermission(), granted -> {
                // 허용/거절 결과와 상관없이 상태를 다시 계산해서 웹에 알림
                if (monitor != null) monitor.forceRefresh();
            });
```

### 3-3. onCreate 안에 감지 시작 코드 추가

`onCreate` 안에서 **webView 를 만들고 설정하는 코드 뒤**에 붙여 넣습니다.
(`webView.loadUrl(...)` 이 있다면 **그 줄 위**에 넣으세요.)

```java
        // --- 스캐너 연결 상태 감지 시작 ---
        webView.getSettings().setJavaScriptEnabled(true);
        webView.addJavascriptInterface(new ScannerBridge(), "AndroidScanner");

        // ★ nameKeyword 는 장치 이름에 들어 있는 글자입니다. 7단계에서 확인 후 필요하면 바꾸세요.
        List<ScannerConnectionMonitor.Target> targets = Arrays.asList(
                new ScannerConnectionMonitor.Target("DS8178", "Zebra DS8178 (HID)", "DS8178", 0),
                new ScannerConnectionMonitor.Target("SM530", "하디스 SM530 (HOG)", "SM530", 3000)
        );

        monitor = new ScannerConnectionMonitor(this, targets, json -> pushToWeb(json));

        // 안드로이드 12 이상은 "근처 기기" 권한을 물어봅니다
        if (Build.VERSION.SDK_INT >= 31 && !monitor.hasBtPermission()) {
            btPermissionLauncher.launch(Manifest.permission.BLUETOOTH_CONNECT);
        }
        monitor.start();
```

> 마지막 숫자(`0`, `3000`)는 "끊김으로 판단하기 전에 기다릴 시간(ms)"입니다.
> DS8178 은 0(바로 판단), SM530 은 3000(3초 기다림)으로 했습니다. 테스트 후 조절하세요.

### 3-4. 웹 페이지가 다 열린 뒤 현재 상태 한 번 더 보내기

웹 페이지가 열리기 **전에** 보낸 값은 웹이 놓칠 수 있습니다. 그래서 페이지가 다 열린 직후에 한 번 더 보냅니다.

**이미 `WebViewClient` 가 있는 경우:** 그 안의 `onPageFinished` 에 아래 한 줄만 추가하세요.

```java
if (monitor != null) pushToWeb(monitor.getLastJson());
```

**`WebViewClient` 가 없는 경우:** `onCreate` 안, `webView.loadUrl(...)` 위에 아래를 붙여 넣으세요.

```java
        webView.setWebViewClient(new android.webkit.WebViewClient() {
            @Override
            public void onPageFinished(android.webkit.WebView view, String url) {
                if (monitor != null) pushToWeb(monitor.getLastJson());
            }
        });
```

### 3-5. 클래스 안에 함수 3개 추가 (onCreate 밖)

`onCreate` 가 끝나는 `}` **아래**, 클래스 안에 붙여 넣습니다.

```java
    @Override
    protected void onResume() {
        super.onResume();
        if (monitor != null) monitor.forceRefresh(); // 앱으로 돌아왔을 때 상태 다시 확인
    }

    @Override
    protected void onDestroy() {
        if (monitor != null) monitor.stop();
        super.onDestroy();
    }

    /** 앱 -> 웹 : 상태를 웹으로 보냅니다. */
    private void pushToWeb(final String json) {
        final String js = "(function(){"
                + "window.__scannerStatus = JSON.parse(" + JSONObject.quote(json) + ");"
                + "window.dispatchEvent(new CustomEvent('scanner-status',{detail:window.__scannerStatus}));"
                + "})();";
        webView.post(new Runnable() {
            @Override public void run() {
                webView.evaluateJavascript(js, null);
            }
        });
    }
```

> `onResume`, `onDestroy` 가 **이미 있다면** 새로 만들지 말고, 기존 함수 안에 `monitor` 관련 줄만 넣으세요.
> (`super.onResume();` 같은 기존 줄은 그대로 두어야 합니다.)

### 3-6. 웹이 앱에 물어볼 수 있는 통로 추가

3-5 의 코드 **아래**, 클래스가 끝나기 전에 붙여 넣습니다.

```java
    /**
     * 웹에서 window.AndroidScanner.xxx() 로 부를 수 있는 함수들.
     * 주의: 반드시 "내가 만든 믿을 수 있는 웹 주소"에서만 쓰세요.
     *       모르는 사이트가 열리는 웹뷰에는 사용하면 안 됩니다.
     */
    private class ScannerBridge {
        @JavascriptInterface
        public String getScannerStatus() {
            return monitor != null ? monitor.getLastJson() : "{}";
        }

        @JavascriptInterface
        public void refresh() {
            runOnUiThread(new Runnable() {
                @Override public void run() {
                    if (monitor != null) monitor.forceRefresh();
                }
            });
        }

        @JavascriptInterface
        public String dumpDevices() {
            return monitor != null ? monitor.dumpDevices() : "{}";
        }

        /** 웹 -> 앱 : 안드로이드 설정의 "블루투스" 화면을 엽니다. */
        @JavascriptInterface
        public void openBluetoothSettings() {
            runOnUiThread(new Runnable() {
                @Override public void run() {
                    try {
                        // 블루투스 설정 화면
                        startActivity(new Intent(Settings.ACTION_BLUETOOTH_SETTINGS));
                    } catch (Exception e) {
                        try {
                            // 안 열리면 일반 설정 화면으로 대체
                            startActivity(new Intent(Settings.ACTION_SETTINGS));
                        } catch (Exception ignored) {
                            // 단말 정책으로 설정 앱이 막혀 있으면 여기서 끝
                        }
                    }
                }
            });
        }
    }
```

**확인:** 앱을 실행(▶)했을 때 빨간 글씨 없이 빌드되면 안드로이드 쪽은 끝입니다.
앱 실행 후 "근처 기기" 권한 창이 뜨면 **허용**을 누르세요. (안드로이드 12 이상)

---

## 4단계. 웹 화면에서 값 받기 (기본 JS)

앱이 보내는 데이터 모양은 이렇습니다.

```json
{
  "updatedAt": 1760850000000,
  "btPermission": true,
  "bluetoothEnabled": true,
  "scanners": [
    { "key": "DS8178", "label": "Zebra DS8178 (HID)", "state": "connected",    "paired": true },
    { "key": "SM530",  "label": "하디스 SM530 (HOG)", "state": "disconnected", "paired": true }
  ]
}
```

- `state` : `connected`(연결됨) / `disconnected`(끊김) / `unknown`(아직 모름)
- `paired` : 단말에 등록(페어링)되어 있는지. 권한이 없으면 `null`
- `btPermission` : `false` 이면 블루투스 권한이 거절된 상태
- `bluetoothEnabled` : 단말의 블루투스가 켜져 있는지 (`false` 이면 꺼진 상태)

웹 페이지의 `<script>` 안에 아래를 붙여 넣으면 됩니다.

```html
<div id="status">대기 중...</div>

<script>
  // 화면 그리기
  function render(data) {
    if (!data || !data.scanners) return;
    var html = data.scanners.map(function (s) {
      var on = s.state === "connected";
      return "<div>" + s.label + " : " + (on ? "🟢 연결됨" : "🔴 끊김") + "</div>";
    }).join("");
    if (data.btPermission === false) {
      html += "<div style='color:red'>블루투스 권한이 없습니다. 앱 설정에서 '근처 기기'를 허용해 주세요.</div>";
    }
    document.getElementById("status").innerHTML = html;
  }

  // 1) 앱이 상태가 바뀔 때마다 보내는 이벤트 받기
  window.addEventListener("scanner-status", function (e) { render(e.detail); });

  // 2) 페이지가 열린 직후, 이미 받아 둔 값이 있으면 사용
  if (window.__scannerStatus) {
    render(window.__scannerStatus);
  } else if (window.AndroidScanner) {
    // 3) 없으면 앱에게 직접 물어보기
    try { render(JSON.parse(window.AndroidScanner.getScannerStatus())); } catch (e) {}
  }
</script>
```

---

## 4-2단계. 웹 화면에서 "안드로이드 설정 > 블루투스" 열기

스캐너가 끊겼거나 블루투스가 꺼져 있을 때, 웹 화면의 버튼을 누르면 **안드로이드 설정의 블루투스 화면**이 열리게 합니다.

### 동작 순서 (생각의 흐름)

1. 웹 화면의 **[블루투스 설정 열기]** 버튼을 누릅니다.
2. 웹이 앱에게 `AndroidScanner.openBluetoothSettings()` 를 호출합니다.
3. 앱이 안드로이드 **설정 > 블루투스** 화면을 엽니다.
4. 사용자가 거기서 블루투스를 켜거나 스캐너를 연결/해제합니다.
5. **뒤로 가기**로 앱에 돌아오면 `onResume` 이 실행되어 **상태를 자동으로 다시 확인**합니다.
6. 블루투스 켜짐/꺼짐이 바뀌면 앱이 알림을 받아 웹에 새 상태를 보냅니다.

웹이 설정 화면을 직접 열 수는 없습니다. **반드시 앱이 대신 열어 줘야 합니다.**

### 앱 쪽에 필요한 것 (3단계에 이미 포함됨)

| 위치 | 추가된 내용 |
|---|---|
| 3-1 import | `android.content.Intent`, `android.provider.Settings` 2줄 |
| 3-6 ScannerBridge | `openBluetoothSettings()` 함수 |
| 2단계 `ScannerConnectionMonitor` | 블루투스 켜짐/꺼짐 감지, JSON 의 `bluetoothEnabled` 값 |

> **이미 이전 버전 코드를 붙여 넣으셨다면** 위 표의 3가지만 추가하면 됩니다.
> 2단계 파일은 아래 3군데만 고치세요.
>
> 1. `start()` 안, `ACTION_ACL_DISCONNECTED` 줄 아래에
>    `filter.addAction(BluetoothAdapter.ACTION_STATE_CHANGED);`
> 2. `aclReceiver` 의 `onReceive` 맨 처음에
>    `if (BluetoothAdapter.ACTION_STATE_CHANGED.equals(intent.getAction())) { refresh(null, true); return; }`
> 3. `buildJson()` 안, `root.put("btPermission", ...)` 줄 아래에
>    `root.put("bluetoothEnabled", adapter != null && adapter.isEnabled());`

### 웹 화면 코드 (기본 JS)

**(1) 버튼을 하나 추가합니다.** (4단계 HTML 의 `<div id="status">` 아래)

```html
<button id="btnBt" onclick="openBtSettings()" style="display:none; width:100%; padding:12px;">
  블루투스 설정 열기
</button>
```

**(2) 4단계 `<script>` 안에 함수를 추가합니다.**

```js
// 안드로이드 설정 > 블루투스 열기
function openBtSettings() {
  if (window.AndroidScanner && AndroidScanner.openBluetoothSettings) {
    AndroidScanner.openBluetoothSettings();
  } else {
    alert("안드로이드 앱 안에서만 열 수 있습니다.");
  }
}
```

**(3) 4단계 `render` 함수 안, 마지막 `innerHTML` 넣기 전에 아래를 추가합니다.**

```js
// 블루투스가 꺼져 있거나, 하나라도 끊겨 있으면 설정 버튼 보이기
var needBt = data.bluetoothEnabled === false ||
             data.scanners.some(function (s) { return s.state !== "connected"; });
document.getElementById("btnBt").style.display = needBt ? "block" : "none";

if (data.bluetoothEnabled === false) {
  html += "<div style='color:red'>단말의 블루투스가 꺼져 있습니다.</div>";
}
```

### React 를 쓰는 경우

5단계 컴포넌트에 아래를 추가하세요.

```jsx
const openBtSettings = () => {
  if (window.AndroidScanner && window.AndroidScanner.openBluetoothSettings) {
    window.AndroidScanner.openBluetoothSettings();
  } else {
    alert("안드로이드 앱 안에서만 열 수 있습니다.");
  }
};

const needBt =
  status.bluetoothEnabled === false ||
  status.scanners.some((s) => s.state !== "connected");

// return 안에 추가
{status.bluetoothEnabled === false && (
  <p style={{ color: "red" }}>단말의 블루투스가 꺼져 있습니다.</p>
)}
{needBt && <button onClick={openBtSettings}>블루투스 설정 열기</button>}
```

### 사용자가 설정 화면에서 할 일 (처음 연결할 때)

1. 열린 **블루투스** 화면에서 블루투스가 **켜짐**인지 확인합니다. 꺼져 있으면 스위치를 눌러 켭니다.
2. 스캐너를 **페어링 모드**로 만듭니다. (스캐너마다 방법이 다릅니다. 제품 설명서를 확인하세요.)
3. 목록에 스캐너 이름이 나타나면 눌러서 **연결**합니다.
4. **뒤로 가기**로 앱에 돌아옵니다. 웹 화면의 상태가 🟢 로 바뀌는지 확인합니다.

### 알아둘 점

- 제가 실제 단말에서 확인하지 못했습니다. Zebra 같은 업무용 단말은 **관리 정책(MDM)이나 키오스크 모드로 설정 앱이 막혀 있을 수 있습니다.** 이 경우 버튼을 눌러도 아무 반응이 없을 수 있어서, 코드에 "블루투스 설정이 안 열리면 일반 설정으로 대체"하는 시도만 넣었습니다.
- 안드로이드는 버전에 따라 **앱이 블루투스를 직접 켜고 끄는 것**을 제한합니다. 그래서 이 방식은 "설정 화면을 열어 주고 사용자가 직접 켜는" 방식으로 구성했습니다.

---

## 5단계. React 를 쓰는 경우 (선택)

웹 화면이 React 라면 아래 훅을 만들어 쓰세요.

**`src/useScannerStatus.js`**

```jsx
import { useEffect, useState } from "react";

export default function useScannerStatus() {
  const [status, setStatus] = useState(null);
  const inWebView = typeof window !== "undefined" && !!window.AndroidScanner;

  useEffect(() => {
    const handler = (e) => setStatus(e.detail);
    window.addEventListener("scanner-status", handler);

    // 이미 받은 값이 있으면 사용, 없으면 앱에 물어보기
    if (window.__scannerStatus) {
      setStatus(window.__scannerStatus);
    } else if (window.AndroidScanner) {
      try {
        setStatus(JSON.parse(window.AndroidScanner.getScannerStatus()));
      } catch (e) {}
    }
    return () => window.removeEventListener("scanner-status", handler);
  }, []);

  return { status, inWebView };
}
```

**사용 예시**

```jsx
import useScannerStatus from "./useScannerStatus";

export default function ScannerStatus() {
  const { status, inWebView } = useScannerStatus();

  if (!inWebView) return <p>안드로이드 앱 안에서만 확인할 수 있습니다.</p>;
  if (!status || !status.scanners) return <p>상태 확인 중...</p>;

  return (
    <div>
      {status.scanners.map((s) => (
        <div key={s.key}>
          {s.label} : {s.state === "connected" ? "🟢 연결됨" : "🔴 끊김"}
        </div>
      ))}
      {status.btPermission === false && (
        <p style={{ color: "red" }}>블루투스 권한이 필요합니다.</p>
      )}
    </div>
  );
}
```

> PC 브라우저에서는 앱이 없으므로 `inWebView` 가 `false` 가 됩니다. 정상입니다.

---

## 6단계. (선택) 내 웹 페이지가 아직 없을 때: 테스트용 페이지 만들기

웹 페이지가 아직 없다면, 앱 안에 **테스트용 HTML 파일**을 넣어서 확인할 수 있습니다.

### 6-1. assets 폴더 만들기

1. 왼쪽 **Project** 창에서 `app` 에 **마우스 오른쪽 버튼**
2. **New** → **Folder** → **Assets Folder**
3. 나온 창에서 **Finish**

### 6-2. 테스트 HTML 만들기

1. 방금 생긴 `assets` 폴더에 **마우스 오른쪽 버튼**
2. **New** → **File** → 이름에 `index.html` 입력 → Enter
3. 아래 내용을 붙여 넣습니다.

```html
<!DOCTYPE html>
<html lang="ko">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>스캐너 연결 상태</title>
  <style>
    body { font-family: sans-serif; padding: 16px; }
    .row { padding: 12px; margin: 8px 0; border-radius: 8px; background: #f3f3f3; font-size: 18px; }
    .on  { background: #d7f5dd; }
    .off { background: #fbe0e0; }
    button { width: 100%; padding: 12px; margin-top: 8px; font-size: 16px; }
    pre { background: #222; color: #0f0; padding: 8px; font-size: 12px; white-space: pre-wrap; word-break: break-all; }
  </style>
</head>
<body>
  <h3>스캐너 연결 상태</h3>
  <div id="list">대기 중...</div>
  <div id="warn" style="color:red"></div>

  <button onclick="window.AndroidScanner && AndroidScanner.refresh()">상태 새로고침</button>
  <button onclick="window.AndroidScanner && AndroidScanner.openBluetoothSettings()">블루투스 설정 열기</button>
  <button onclick="showDevices()">기기 이름 확인</button>
  <pre id="dump"></pre>

  <script>
    function render(data) {
      if (!data || !data.scanners) return;
      document.getElementById("list").innerHTML = data.scanners.map(function (s) {
        var on = s.state === "connected";
        return "<div class='row " + (on ? "on" : "off") + "'>" +
               s.label + " : " + (on ? "🟢 연결됨" : "🔴 끊김") +
               (s.paired === false ? " (페어링 안 됨)" : "") + "</div>";
      }).join("");
      document.getElementById("warn").textContent =
        data.btPermission === false ? "블루투스 권한이 없습니다. '근처 기기'를 허용해 주세요." : "";
    }

    function showDevices() {
      if (!window.AndroidScanner) { alert("앱 안에서만 동작합니다."); return; }
      var d = JSON.parse(AndroidScanner.dumpDevices());
      document.getElementById("dump").textContent = JSON.stringify(d, null, 2);
    }

    window.addEventListener("scanner-status", function (e) { render(e.detail); });
    if (window.__scannerStatus) render(window.__scannerStatus);
    else if (window.AndroidScanner) {
      try { render(JSON.parse(AndroidScanner.getScannerStatus())); } catch (e) {}
    }
  </script>
</body>
</html>
```

### 6-3. 앱이 이 파일을 열게 하기

`MainActivity` 의 `webView.loadUrl(...)` 줄을 아래로 바꿉니다.

```java
webView.loadUrl("file:///android_asset/index.html");
```

---

## 7단계. 스캐너 이름 확인하고 맞추기 (가장 중요)

코드는 장치 **이름에 `DS8178` 또는 `SM530` 이 들어 있으면** 같은 장치로 봅니다.
그런데 실제 단말에서 이름이 어떻게 보이는지는 제가 확인하지 못했습니다. 반드시 직접 확인하세요.

### 7-1. 스캐너 연결하기

1. 단말의 **설정** → **연결**(또는 **블루투스**) 을 엽니다.
2. DS8178 과 SM530 을 각각 페어링해서 **연결됨** 상태로 만듭니다.

### 7-2. 이름 보기

1. 앱을 실행하고 6단계의 테스트 화면에서 **[기기 이름 확인]** 버튼을 누릅니다.
2. 아래처럼 나옵니다.

```json
{
  "btPermission": true,
  "inputDevices": [
    { "name": "gpio-keys", "vendorId": 0, "productId": 0 },
    { "name": "여기에 스캐너 이름이 보입니다", "vendorId": 1234, "productId": 5678 }
  ],
  "bondedDevices": ["여기에 페어링된 장치 이름"]
}
```

3. 다음을 확인합니다.
   - `inputDevices` 에 DS8178, SM530 이 **연결된 상태에서** 나타나는지
   - 그 이름에 `DS8178`, `SM530` 글자가 들어 있는지

### 7-3. 이름이 다르면 nameKeyword 바꾸기

3-3 의 `Target` 안에는 값이 4개 있습니다. 그중 **세 번째 값(따옴표 글자)** 이 이름 찾기용 글자입니다. 이것을 실제 이름의 일부로 바꿉니다.

```java
// 예: 실제 이름이 "Zebra DS8178-HC" 로 보인다면 그대로 "DS8178" 이면 충분합니다.
// 예: 실제 이름이 "HADIS-SM-530" 처럼 다르게 보인다면
new ScannerConnectionMonitor.Target("SM530", "하디스 SM530 (HOG)", "SM-530", 3000)
```

> 이름은 **대소문자를 구분하지 않고**, 포함만 되면 됩니다.
> 두 스캐너 이름이 서로 겹치지 않게 정해 주세요.

---

## 8단계. 동작 확인 체크리스트

앱을 실행한 상태에서 하나씩 해 보세요.

| 순서 | 할 일 | 기대하는 결과 | 확인 |
|---|---|---|---|
| 1 | 두 스캐너를 켜서 연결 | 화면에 둘 다 🟢 연결됨 | ☐ |
| 2 | DS8178 전원 끄기 | DS8178 만 🔴 끊김 | ☐ |
| 3 | DS8178 전원 켜기 | 다시 🟢 연결됨 | ☐ |
| 4 | SM530 전원 끄기 | 약 3초 뒤 🔴 끊김 | ☐ |
| 5 | SM530 전원 켜기 | 🟢 연결됨 | ☐ |
| 6 | 앱을 완전히 종료 후 다시 실행 | 시작하자마자 현재 상태가 맞게 표시 | ☐ |
| 7 | 단말 블루투스 자체를 껐다 켜기 | 둘 다 끊김 → 다시 연결됨 | ☐ |
| 8 | 웹의 **[블루투스 설정 열기]** 버튼 누르기 | 안드로이드 설정의 블루투스 화면이 열림 | ☐ |
| 9 | 설정 화면에서 블루투스를 끄고 뒤로 가기 | 웹에 "블루투스가 꺼져 있습니다" 표시 | ☐ |

---

## 문제 해결

| 증상 | 원인 | 해결 |
|---|---|---|
| 화면에 "대기 중..." 에서 안 바뀜 | JS 로 값이 안 넘어옴 | 3-4 의 `onPageFinished` 줄이 들어갔는지 확인, 앱 재실행 |
| 둘 다 항상 "끊김" | 이름이 안 맞음 | 7단계로 실제 이름 확인 후 `nameKeyword` 수정 |
| "블루투스 권한이 없습니다" 표시 | 안드로이드 12 이상에서 권한 거절 | 단말 설정 → 앱 → (내 앱) → 권한 → **근처 기기** 허용 |
| SM530 이 자꾸 끊겼다 붙었다 함 | 저전력 방식의 절전 동작 | `Target` 의 `3000` 을 `5000` 처럼 늘리기 |
| 끊어도 "연결됨"으로 남음 | 블루투스 알림만 오고 입력 장치는 이름이 다름 | 7단계로 입력 장치 이름 확인 |
| 블루투스 설정 열기 버튼을 눌러도 반응 없음 | 단말 정책(MDM/키오스크)으로 설정 앱이 막힘, 또는 `openBluetoothSettings` 함수 누락 | 3-6 코드 확인 후, 관리자에게 설정 앱 허용 문의 |
| 블루투스를 꺼도 웹에 "꺼짐" 표시가 안 뜸 | 4-2단계의 `ACTION_STATE_CHANGED` 3군데 수정이 빠짐 | 4-2단계의 "이미 붙여 넣으셨다면" 안내 확인 |
| `AndroidScanner is not defined` | `addJavascriptInterface` 줄이 빠짐 | 3-3 코드 확인 |
| 웹 코드에서 `window.AndroidScanner` 가 없음 | PC 브라우저에서 연 경우 | 앱 안(웹뷰)에서만 동작하는 것이 정상 |

### 로그를 보고 싶다면 (선택)

PC 크롬 주소창에 `chrome://inspect` 를 입력하고, 폰을 USB 로 연결하면 웹뷰 화면의 콘솔 에러를 볼 수 있습니다.
(`WebView.setWebContentsDebuggingEnabled(true);` 를 `onCreate` 에 넣어야 목록에 보입니다. 개발 중에만 쓰세요.)

---

## 보안 주의

- `addJavascriptInterface` 는 웹 페이지가 앱 기능을 부를 수 있게 하는 통로입니다.
  **내가 만든, 신뢰할 수 있는 주소의 페이지에서만** 사용하세요.
- 외부 사이트로 이동할 수 있는 웹뷰라면, 이 통로를 쓰는 화면은 별도의 웹뷰로 분리하는 것이 안전합니다.
