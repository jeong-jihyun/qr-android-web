import { useEffect, useRef, useState, useCallback } from "react";
import { Html5Qrcode } from "html5-qrcode";

const READER_ID = "qr-reader";

// <video>가 화면에 붙고 스트림이 연결될 때까지 잠깐 기다림
const waitForTrack = async () => {
  for (let i = 0; i < 30; i++) {
    const video = document.querySelector(`#${READER_ID} video`);
    const stream = video && video.srcObject;
    if (stream && stream.getVideoTracks().length) return stream.getVideoTracks()[0];
    await new Promise((r) => setTimeout(r, 100));
  }
  return null;
};

export default function QrCameraControl({ onScan }) {
  const scannerRef = useRef(null);
  const trackRef = useRef(null);
  const capsRef = useRef({});

  const [cameras, setCameras] = useState([]);
  const [cameraId, setCameraId] = useState("");
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState("");
  const [error, setError] = useState("");

  const [caps, setCaps] = useState({});
  const [zoom, setZoom] = useState(1);
  const [focusMode, setFocusMode] = useState("");
  const [focusDistance, setFocusDistance] = useState(0);
  const [torchOn, setTorchOn] = useState(false);

  // 1) 스캐너 생성 + 카메라 목록 불러오기
  useEffect(() => {
    scannerRef.current = new Html5Qrcode(READER_ID);

    Html5Qrcode.getCameras()
      .then((list) => {
        setCameras(list);
        const back = list.find((c) => /back|rear|environment|후면/i.test(c.label));
        setCameraId((back || list[0] || {}).id || "");
      })
      .catch((e) => setError("카메라 권한이 없거나 HTTPS가 아닙니다: " + e));

    // 화면을 벗어나면 카메라 끄기
    return () => {
      const s = scannerRef.current;
      scannerRef.current = null;
      trackRef.current = null;
      if (s && s.isScanning) {
        s.stop().then(() => s.clear()).catch(() => {});
      }
    };
  }, []);

  // 2) 카메라 설정 적용 (실패해도 앱은 계속 동작)
  const apply = useCallback(async (constraint) => {
    const track = trackRef.current;
    if (!track) return;
    try {
      await track.applyConstraints({ advanced: [constraint] });
    } catch (e) {
      console.warn("적용 실패:", e);
    }
  }, []);

  // 3) 스캔 시작
  const start = async () => {
    setError("");
    const scanner = scannerRef.current;
    if (!scanner) return;
    try {
      await scanner.start(
        cameraId || { facingMode: "environment" },
        { fps: 10, qrbox: { width: 250, height: 250 } },
        (text) => {
          setResult(text);
          onScan && onScan(text);
        },
        () => {}
      );
      setRunning(true);

      const track = await waitForTrack();
      if (!track) return;
      trackRef.current = track;

      const c = track.getCapabilities ? track.getCapabilities() : {};
      const s = track.getSettings ? track.getSettings() : {};
      capsRef.current = c;
      setCaps(c);

      if (c.zoom) setZoom(s.zoom ?? c.zoom.min);
      if (c.focusMode && c.focusMode.length) setFocusMode(s.focusMode || c.focusMode[0]);
      if (c.focusDistance) setFocusDistance(s.focusDistance ?? c.focusDistance.min);
    } catch (e) {
      setError("시작 실패: " + e);
    }
  };

  // 4) 스캔 중지
  const stop = async () => {
    const scanner = scannerRef.current;
    if (scanner && scanner.isScanning) {
      await scanner.stop();
      scanner.clear();
    }
    trackRef.current = null;
    setRunning(false);
    setTorchOn(false);
  };

  // 5) 컨트롤 핸들러
  const onZoom = (e) => {
    const v = Number(e.target.value);
    setZoom(v);
    apply({ zoom: v });
  };

  const onFocusMode = (e) => {
    setFocusMode(e.target.value);
    apply({ focusMode: e.target.value });
  };

  const onFocusDistance = (e) => {
    const v = Number(e.target.value);
    setFocusDistance(v);
    setFocusMode("manual");
    apply({ focusMode: "manual", focusDistance: v });
  };

  const toggleTorch = () => {
    if (!caps.torch) return alert("이 카메라는 플래시를 지원하지 않습니다.");
    const next = !torchOn;
    setTorchOn(next);
    apply({ torch: next });
  };

  // 6) 화면 터치 → 해당 위치로 초점 (지원 기기만), 잠시 후 연속 초점 복귀
  const onTapFocus = async (e) => {
    if (!running) return;
    const c = capsRef.current;
    const rect = e.currentTarget.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;
    const y = (e.clientY - rect.top) / rect.height;

    if (c.pointsOfInterest) {
      await apply({ pointsOfInterest: [{ x, y }], focusMode: "single-shot" });
    } else if ((c.focusMode || []).includes("single-shot")) {
      await apply({ focusMode: "single-shot" });
    }
    setTimeout(() => {
      if ((capsRef.current.focusMode || []).includes("continuous")) {
        apply({ focusMode: "continuous" });
        setFocusMode("continuous");
      }
    }, 1500);
  };

  const hasZoom = !!caps.zoom;
  const hasFocusMode = !!(caps.focusMode && caps.focusMode.length);
  const hasFocusDistance = !!caps.focusDistance;

  return (
    <div style={{ maxWidth: 480, margin: "0 auto", padding: 12, fontFamily: "sans-serif" }}>
      <h3>QR 카메라 컨트롤</h3>

      <select
        value={cameraId}
        onChange={(e) => setCameraId(e.target.value)}
        disabled={running}
        style={{ width: "100%", padding: 8 }}
      >
        {cameras.map((c, i) => (
          <option key={c.id} value={c.id}>
            {c.label || `카메라 ${i + 1}`}
          </option>
        ))}
      </select>

      <div style={{ display: "flex", gap: 8, margin: "8px 0" }}>
        <button onClick={start} disabled={running} style={{ flex: 1, padding: 8 }}>
          스캔 시작
        </button>
        <button onClick={stop} disabled={!running} style={{ flex: 1, padding: 8 }}>
          중지
        </button>
      </div>

      {/* html5-qrcode가 이 div 안에 video를 만듭니다 */}
      <div id={READER_ID} onClick={onTapFocus} style={{ width: "100%" }} />

      {running && (
        <div style={{ marginTop: 10 }}>
          {hasZoom && (
            <div style={{ margin: "10px 0" }}>
              <label>확대: {Number(zoom).toFixed(1)}x</label>
              <input
                type="range"
                min={caps.zoom.min}
                max={caps.zoom.max}
                step={caps.zoom.step || 0.1}
                value={zoom}
                onChange={onZoom}
                style={{ width: "100%" }}
              />
            </div>
          )}

          {hasFocusMode && (
            <div style={{ margin: "10px 0" }}>
              <label>초점 모드</label>
              <select value={focusMode} onChange={onFocusMode} style={{ width: "100%", padding: 8 }}>
                {caps.focusMode.map((m) => (
                  <option key={m} value={m}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          )}

          {hasFocusDistance && (
            <div style={{ margin: "10px 0", opacity: focusMode === "manual" ? 1 : 0.5 }}>
              <label>초점 거리(수동): {Number(focusDistance).toFixed(2)}</label>
              <input
                type="range"
                min={caps.focusDistance.min}
                max={caps.focusDistance.max}
                step={caps.focusDistance.step || 0.01}
                value={focusDistance}
                onChange={onFocusDistance}
                style={{ width: "100%" }}
              />
            </div>
          )}

          {caps.torch && (
            <button onClick={toggleTorch} style={{ width: "100%", padding: 8 }}>
              플래시 {torchOn ? "끄기" : "켜기"}
            </button>
          )}

          {!hasZoom && !hasFocusMode && !hasFocusDistance && (
            <p style={{ fontSize: 13, color: "#a33" }}>
              이 기기/브라우저는 줌·초점 제어를 지원하지 않습니다.
            </p>
          )}
        </div>
      )}

      {error && <p style={{ color: "red", fontSize: 13 }}>{error}</p>}

      <h4>스캔 결과</h4>
      <div style={{ padding: 10, background: "#eef", borderRadius: 6, wordBreak: "break-all", minHeight: 20 }}>
        {result || "-"}
      </div>

      {running && (
        <pre style={{ fontSize: 11, background: "#f6f6f6", padding: 8, borderRadius: 6, whiteSpace: "pre-wrap" }}>
          {JSON.stringify(
            {
              zoom: caps.zoom || "미지원",
              focusMode: caps.focusMode || "미지원",
              focusDistance: caps.focusDistance || "미지원",
              torch: caps.torch ? "지원" : "미지원",
              pointsOfInterest: caps.pointsOfInterest ? "지원" : "미지원",
            },
            null,
            2
          )}
        </pre>
      )}
    </div>
  );
}
