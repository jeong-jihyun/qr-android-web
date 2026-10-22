# qr-android-web

**QR·바코드 생성/스캔과 Zebra 장비(핸드 스캐너·라벨 프린터) 연동을 조사·개발하면서 만든 자료 보관함**입니다.
실행되는 하나의 앱이 아니라 가이드 문서, 예제 코드, 프로젝트 압축 파일, AI 에이전트 작업 규칙이 한곳에 모여 있습니다.
(바로 실행 가능한 코드는 [QRScannerApp](https://github.com/jeong-jihyun/QRScannerApp), [qr-scanner-project](https://github.com/jeong-jihyun/qr-scanner-project) 저장소에 따로 있습니다.)

## 파일 안내

### 1. 가이드·조사 문서

| 파일 | 내용 |
| --- | --- |
| `지브라핸드스캐너_프린터.md` | Zebra 핸드 스캐너·프린터 연동 개발 가이드 (스캐너 / 프린터가 서로 다른 SDK를 쓴다는 구분부터 설명) |
| `Zebra 핸드 스캐너·프린터 연동 개발 가이드.cs` | 위 가이드의 더 긴 판(34KB vs 28KB). 확장자만 `.cs`이고 내용은 일반 글 문서 |
| `스캐너_소스변경_적용가이드.md`, `_v2.md` | Zebra + HANIS-SM530 스캐너를 WebView 기반 앱에 함께 붙이는 소스 변경 가이드 (v2가 최신) |
| `GUIDE-java.md` | 블루투스 스캐너 연결 상태를 웹 화면에 전달하는 방법 (Java) |
| `실행_및_테스트_가이드.md` | QR/바코드 시스템 실행·테스트 방법 |
| `사양 명세서 및 사용자 작업 체크리스트.md` | 사양과 사용자가 직접 해야 할 작업 목록 |
| `QR관련_확인.md`, `작업사항.md` | 요구사항(QR/바코드 생성·스캔, 라벨 출력)과 작업 메모 |

### 2. 예제 코드

| 파일 | 내용 |
| --- | --- |
| `QrCameraControl.jsx` | `html5-qrcode`로 카메라 QR 스캔을 제어하는 React 컴포넌트 |
| `화면검수_QA에이전트/` | Playwright로 화면을 열어 스크린샷을 찍는 `qa-screenshot.mjs`와 QA 시나리오, Claude/OpenCode용 에이전트 정의 |

### 3. 압축 파일 (zip)

| 파일 | 크기 | 내용 |
| --- | --- | --- |
| `qr-barcode-project.zip` | 206KB | Android(WebView, `ZebraPrinterHelper`) + React 웹(QR·바코드 생성, 라벨 출력, 스캐너 입력) 프로젝트 (51개 파일) |
| `zebra-qr-project-second.zip` | 32KB | React 프론트엔드(QR/바코드 생성, 카메라 스캐너, Zebra Browser Print 연동) + Android 모듈 + 사양서 (40개 파일) |
| `qr-guide.zip` | 22KB | 카메라 QR 스캔 방식 3가지 비교 가이드: BarcodeDetector API / ZXing(WASM) / ML Kit(네이티브) |
| `zebra-linkos-mpsdk-pc-v2-15-5569.zip` | **36MB** | Zebra **Link-OS SDK**(PC용 v2.15.5569) 배포 파일 (395개 파일) |

### 4. AI 에이전트 작업 규칙

`AGENT.md`(공통 규칙), `planner.md`, `designer.md`, `developer.md`, `reviewer.md`, `tester.md`(역할별 에이전트 정의), `WORKFLOW_STATE.md`(진행 상태 기록 양식)는 기획 → 디자인 → 개발 → 검토 → 테스트 순서로 일을 나누는 Claude Code 에이전트용 규칙 문서입니다.

## 사용 방법

문서는 그대로 읽으면 되고, 프로젝트는 zip을 풀어서 각 안의 README를 따릅니다.

```bash
unzip qr-barcode-project.zip -d qr-barcode-project     # 안의 README.md 참고
unzip zebra-qr-project-second.zip -d zebra-qr-project  # 안의 README.md, SPEC.md 참고
```

## 점검 내역 (2026-10)

| 구분 | 내용 |
| --- | --- |
| README | 저장소 설명이 없어 파일 용도를 알 수 없었음 → 이 README 추가 |
| 확인한 것 | zip 3개(Link-OS SDK 제외)를 풀어 파일 목록과 비밀 값(비밀번호·키·토큰) 포함 여부를 확인 → 비밀 값 없음. 문서·예제 파일에도 없음 |

## 알아둘 점

- **Zebra Link-OS SDK(36MB)는 Zebra의 라이선스 배포물입니다.** `작업사항.md`에도 "Zebra의 라이선스 배포물이라 개발자 사이트에서 직접 내려받아야 한다"고 적혀 있습니다. 공개 저장소에 올려 두는 것이 라이선스에 맞는지 확인하고, 필요 없으면 저장소에서 빼는 것을 권장합니다. (이 작업에서는 임의로 지우지 않았습니다.)
- 가이드 `.md`와 `.cs` 두 파일은 서로 내용이 달라 어느 쪽이 최신인지 파일만으로는 알 수 없습니다.
- zip 안의 프로젝트(React/Android)는 빌드·실행해 보지 않았습니다. Android SDK와 외부 저장소 접속이 이 작업 환경에 없습니다.
