Zebra 핸드 스캐너·프린터 연동 개발 가이드
이 문서는 "Zebra 장비를 어떻게 코드로 다뤄야 하는지" 감이 안 잡힐 때 보는 문서입니다. 순서대로 읽으면 됩니다. 코드부터 던지지 않고, 왜 이 메소드가 필요한지 → 뭘 리턴하는지 → 헷갈리는 비슷한 메소드는 뭔지 순서로 설명합니다.
전체 그림 먼저 이해하기
프로젝트에서 다루는 건 두 가지입니다. 이 둘은 완전히 다른 SDK를 씁니다. 헷갈리지 않게 먼저 구분하세요.
구분
무엇을 하는가
쓰는 도구
어디서 동작
① 스캐너 (입력)
안드로이드 기기가 바코드/QR을 읽어서 내 앱에 텍스트로 전달
DataWedge (Zebra 기기에 기본 설치된 서비스)
안드로이드
② 프린터 (출력)
내 앱(React 웹 포함)이 라벨 데이터를 프린터에 보내서 인쇄
Link-OS SDK
PC(React) / 안드로이드 둘 다
즉 "스캐너는 DataWedge, 프린터는 Link-OS" 이 한 문장만 기억하면 이후 내용이 훨씬 쉬워집니다.
목차
[스캐너] DataWedge란 무엇이고 왜 필요한가
[스캐너] 스캔 시작/중지 메소드 — Soft Scan Trigger
[스캐너] 연결된 스캐너 목록 확인 — Enumerate Scanners
[스캐너] 스캔 데이터 받기 — Intent Output
[스캐너] 설정값 읽기/쓰기 — Get Config / Set Config
[스캐너] 상태 실시간 감지 — Register Notification
[스캐너] 버전 확인 — Get Version Info
[프린터] Link-OS SDK 연결하기 — Connection
[프린터] 프린터 언어 확인하기 — getPrinterControlLanguage
[프린터] 라벨 실제로 인쇄하기 — write / sendCommand
[프린터] 프린터 상태 확인하기 — getCurrentStatus
자주 막히는 지점 정리
다양한 예제 모음
화면(UI)에서 값이 실제로 어떻게 넘어가는지 — 전체 흐름
1. [스캐너] DataWedge란 무엇이고 왜 필요한가
용도 Zebra 안드로이드 기기(TC 시리즈 등)에는 "DataWedge"라는 백그라운드 서비스가 이미 깔려 있습니다. 카메라나 하드웨어 스캐너 API를 직접 건드릴 필요 없이, 이 서비스가 바코드를 읽어서 문자열로 변환한 뒤 내 앱에 던져줍니다. 개발자는 "스캔값을 받는 창구(Intent)"만 하나 만들면 됩니다.
생각의 흐름
사용자가 트리거 버튼을 누르거나 화면 버튼을 누른다
DataWedge가 바코드를 인식해서 텍스트로 바꾼다
DataWedge가 그 텍스트를 안드로이드 Intent에 담아 내 앱으로 "쏜다"
내 앱은 그 Intent를 받는 리시버만 만들어두면 값을 받을 수 있다
예상 결과값 직접 호출하는 메소드가 아니라 "설정"입니다. DataWedge 앱(APK) 안에서 프로필(Profile)을 만들고, Input(스캐너)과 Output(Intent)을 지정하면 끝입니다.
유사한 개념과 비교
Camera API로 직접 QR을 인식하는 방식과 다릅니다. DataWedge는 하드웨어 스캐너까지 이미 제어해주므로 훨씬 간단합니다.
"DataWedge 프로필"은 앱마다 따로 만들 수 있어서, 여러 앱이 있어도 각자 다른 설정으로 스캔값을 받을 수 있습니다.
2. [스캐너] 스캔 시작/중지 — Soft Scan Trigger
용도 화면에 "스캔" 버튼을 만들어서 눌렀을 때 스캐너 빔을 켜고 끄고 싶을 때 씁니다. 물리 트리거 버튼이 없거나, 화면 UI로도 스캔을 제어하고 싶을 때 사용합니다.
어떻게 호출하는가 (핵심 코드)
Intent i = new Intent();
i.setAction("com.symbol.datawedge.api.ACTION");
i.putExtra("com.symbol.datawedge.api.SOFT_SCAN_TRIGGER", "START_SCANNING");
sendBroadcast(i);
파라미터 자리에는 START_SCANNING, STOP_SCANNING, TOGGLE_SCANNING 세 가지 중 하나가 들어갑니다.
예상 결과값 반환값이 있는 함수가 아니라 "명령을 던지는" 방식입니다(Broadcast). 성공하면 스캐너 LED/빔이 켜지고, 스캔이 되면 4번 항목(Intent Output)으로 결과가 옵니다. 스캐너가 이미 다른 작업 중이면 명령이 무시될 수 있습니다.
유사한 메소드와 차이
Soft RFID Trigger: 바코드가 아니라 RFID 태그를 읽을 때 쓰는 버전입니다. 이름만 비슷하고 용도가 다릅니다.
Soft Trigger: 음성 입력용입니다. 우리 프로젝트(바코드)와는 무관합니다.
물리 트리거 버튼과는 별개의 채널입니다. 즉 이 API는 "소프트웨어로 트리거를 흉내내는 것"이고, 실제 하드웨어 버튼과 경쟁하지 않습니다.
3. [스캐너] 연결된 스캐너 목록 확인 — Enumerate Scanners
용도 기기마다 내장 스캐너, 카메라, 블루투스 스캐너 등 종류가 다를 수 있습니다. "지금 이 기기에서 쓸 수 있는 스캐너가 뭐가 있는지" 미리 확인하고 싶을 때 씁니다. 특히 여러 기종을 지원해야 하는 앱이라면 필수입니다.
예상 결과값 스캐너 이름들의 목록이 Broadcast Intent로 돌아옵니다. 예: 내장 이미저, USB 스캐너, 블루투스 스캐너(RS6000 등) 이름 리스트.
유사한 메소드와 차이
Get Config: 스캐너 목록이 아니라 "지금 설정된 파라미터 값"을 가져옵니다. 목록 확인용이 아니라 설정 확인용입니다.
여러 스캐너를 쓸 경우, 이후 Soft Scan Trigger를 호출할 때 scanner_selection_by_identifier 라는 값으로 "어떤 스캐너를 쓸지" 지정해야 합니다. 그 값의 후보를 얻는 게 바로 이 메소드입니다.
4. [스캐너] 스캔 데이터 받기 — Intent Output
용도 스캔이 완료된 값(바코드 텍스트)을 실제로 내 앱 코드에서 받는 부분입니다. 지금까지는 "명령을 보내는" 쪽이었다면, 이건 "결과를 받는" 쪽입니다.
생각의 흐름
DataWedge 설정(Profile)에서 Output을 "Intent"로 지정
Action / Category 값을 내가 정한다 (예: com.내패키지.ACTION)
내 앱 AndroidManifest.xml에 같은 Action/Category를 가진 리시버를 등록
스캔되면 그 리시버가 실행되면서 데이터가 담긴 Intent를 받는다
받는 코드 예시
String barcode = intent.getStringExtra("com.symbol.datawedge.data_string");
String symbology = intent.getStringExtra("com.symbol.datawedge.label_type");
예상 결과값
data_string: 스캔된 실제 텍스트 (예: "8801234567890")
label_type: 바코드 종류 (예: EAN13, QRCODE, CODE128 등)
source: 어떤 입력 소스에서 왔는지(스캐너인지 MSR인지 등)
유사한 메소드와 차이
Raw 모드 출력도 있는데, 이건 바이트 스트림(decode_data)으로 받는 방식이라 커스텀 인코더가 필요할 때만 씁니다. 일반적인 문자열 바코드라면 위의 data_string 방식이면 충분합니다.
Keystroke 출력 방식도 있는데(스캔값이 마치 키보드 입력처럼 들어가는 방식), 이건 우리처럼 "값을 코드로 가공"해야 하는 프로젝트에는 안 맞습니다. Intent 방식이 정석입니다.
5. [스캐너] 설정값 읽기/쓰기 — Get Config / Set Config
용도 "어떤 바코드 종류(QR, EAN13...)를 인식할지", "스캔 후 진동을 줄지" 같은 세부 설정을 코드로 직접 바꾸고 싶을 때 씁니다. 보통은 DataWedge 앱 화면에서 수동으로 설정하지만, 앱 배포 시 자동 설정을 넣고 싶다면 이 API를 씁니다.
예상 결과값
Get Config: 현재 프로필의 설정값 묶음(Bundle)을 돌려줍니다.
Set Config: 반환값은 없고, 설정이 적용됩니다. 성공 여부는 Notification(6번 항목)으로 확인하는 걸 권장합니다.
유사한 메소드와 차이
Switch Scanner Params: 프로필 자체를 바꾸지 않고 "일시적으로" 파라미터만 바꿀 때 씁니다. 예를 들어 특정 화면에서만 잠깐 스캔 범위를 좁히고 싶을 때 유용합니다. Set Config는 프로필에 영구 반영, Switch Scanner Params는 임시 변경이라고 구분하면 됩니다.
6. [스캐너] 상태 실시간 감지 — Register Notification
용도 스캐너가 지금 "대기중(IDLE)"인지 "스캔중(SCANNING)"인지 실시간으로 알고 싶을 때 씁니다. 예를 들어 "스캔 중에는 버튼을 비활성화" 같은 UX를 만들 때 필요합니다.
예상 결과값 등록해두면 상태가 바뀔 때마다 Broadcast Intent가 날아옵니다. 값은 WAITING, SCANNING, IDLE, DISABLED 등입니다.
유사한 메소드와 차이
Get Version Info(7번)와 헷갈리지 마세요. 하나는 "지금 상태"를 실시간으로 알려주는 것(Notification)이고, 다른 하나는 "버전 정보"를 1회성으로 조회하는 것입니다.
실무 팁: Zebra 공식 문서에서도 Soft Scan Trigger를 호출하기 전에 이 Notification으로 "지금 IDLE 상태인지" 먼저 확인하고 호출하는 걸 권장합니다. 스캐너가 바쁠 때 명령을 보내면 무시되기 때문입니다.
7. [스캐너] 버전 확인 — Get Version Info
용도 내가 쓰려는 기능(예: 특정 신규 파라미터)이 이 기기의 DataWedge 버전에서 지원되는지 미리 확인할 때 씁니다. 여러 기종/여러 OS 버전 기기에 배포해야 하는 회사 프로젝트라면 꼭 필요한 방어 코드입니다.
예상 결과값 DataWedge 버전 문자열이 돌아옵니다.
유사한 메소드와 차이
이건 "기능 지원 여부 사전 체크용"입니다. 실제 스캔 동작과는 관계없고, 앱 시작 시 한 번 확인하고 넘어가는 용도로 씁니다.
8. [프린터] Link-OS SDK 연결하기 — Connection
여기서부터는 스캐너가 아니라 프린터 쪽입니다. 완전히 다른 SDK(Link-OS)라는 걸 다시 한번 기억하세요.
용도 프린터에 데이터를 보내려면 먼저 "연결 통로"를 열어야 합니다. 이 통로의 종류가 Connection 클래스입니다. 프린터가 USB로 붙어있는지, 네트워크(TCP/IP)로 붙어있는지, 블루투스인지에 따라 쓰는 클래스가 다릅니다.
코드 예시 (네트워크 프린터 기준)
Connection connection = new TcpConnection("192.168.1.100", TcpConnection.DEFAULT_ZPL_TCP_PORT);
connection.open();
예상 결과값 open()이 성공하면 프린터와 통신 가능한 상태가 됩니다. 실패하면 ConnectionException이 발생합니다(예: 프린터가 꺼져있거나 IP가 틀림).
유사한 메소드와 차이
TcpConnection: 네트워크(Wi-Fi/이더넷) 프린터용. React 웹에서 붙일 때 가장 흔한 방식입니다.
UsbConnection: PC나 안드로이드에 USB로 직결된 프린터용.
BluetoothConnection: 모바일 핸드헬드 프린터용.
세 개 다 "연결"이라는 역할은 같고, 물리적으로 어떻게 붙어있느냐만 다릅니다. 회사 프로젝트가 PC(React)와 모바일을 둘 다 지원해야 하니, 실제로는 이 중 최소 두 가지(Tcp + Bluetooth 또는 Usb)를 상황별로 골라 쓰게 될 가능성이 높습니다.
9. [프린터] 프린터 언어 확인하기 — getPrinterControlLanguage
용도 Zebra 프린터는 "ZPL"이라는 명령어 언어를 쓰는 게 보통이지만, 구형 기종은 "EPL"을 쓰기도 합니다. 내가 보낼 라벨 명령어 포맷을 어떤 언어로 만들어야 하는지 미리 확인할 때 씁니다.
코드 예시
ZebraPrinter printer = ZebraPrinterFactory.getInstance(connection);
PrinterLanguage lang = printer.getPrinterControlLanguage();
예상 결과값 ZPL 또는 LINE_PRINT(EPL 계열) 같은 열거값(enum)이 돌아옵니다.
유사한 메소드와 차이
ZebraPrinterFactory.getInstance(connection) 자체는 "연결 객체를 프린터 제어 객체로 감싸주는" 팩토리 메소드입니다. 이 자체가 프린터 언어를 자동 판별해줍니다. 그래서 이 값을 직접 조회하는 getPrinterControlLanguage()는 "확인용"이고, 실제 판별/처리는 팩토리가 내부적으로 이미 하고 있다고 이해하면 됩니다.
10. [프린터] 라벨 실제로 인쇄하기 — write / sendCommand
용도 확인한 언어(보통 ZPL)에 맞는 라벨 명령어 문자열을 만들어서 프린터로 실제로 전송(=인쇄)하는 단계입니다.
생각의 흐름
React 쪽에서 QR/바코드 값을 텍스트로 준비 (예: 주문번호)
그 값을 ZPL 명령어 문자열로 감싼다 (예: ^XA^FO50,50^BQN,2,5^FDQA,주문번호^FS^XZ)
연결된 프린터로 그 문자열을 그대로 전송
코드 예시 (개념)
String zpl = "^XA^FO50,50^BQN,2,5^FDQA,ORDER1234^FS^XZ";
connection.write(zpl.getBytes());
예상 결과값 반환값은 따로 없고, 정상 전송되면 프린터가 바로 인쇄를 시작합니다. 전송은 됐는데 인쇄가 안 되면 11번(상태 확인)으로 원인을 찾아야 합니다.
유사한 메소드와 차이
connection.write(): 저수준(low-level) 방식. ZPL 문자열을 그대로 바이트로 밀어넣습니다. 세밀하게 제어하고 싶을 때 씁니다.
printer.sendCommand() / 고수준 헬퍼 메소드들: SDK가 조금 더 편의 기능을 제공하는 방식(예: 이미지 자동 변환 후 전송 등). 처음 시작할 땐 write() 방식으로 ZPL을 직접 보내는 게 원리를 이해하기 더 쉽습니다.
11. [프린터] 프린터 상태 확인하기 — getCurrentStatus
용도 "라벨을 보냈는데 왜 안 나오지?"를 코드로 진단할 때 씁니다. 용지가 없는지, 헤드가 열려있는지, 일시정지 상태인지 등을 확인합니다.
코드 예시
PrinterStatus status = printer.getCurrentStatus();
if (status.isReadyToPrint) {
    // 인쇄 가능
} else if (status.isPaperOut) {
    // 용지 없음
} else if (status.isHeadOpen) {
    // 프린터 덮개 열림
} else if (status.isPaused) {
    // 일시정지 상태
}
예상 결과값 PrinterStatus 객체가 돌아오고, 그 안에 isReadyToPrint, isPaperOut, isHeadOpen, isPaused 같은 boolean 필드들이 들어있습니다.
유사한 메소드와 차이
getPrinterControlLanguage()(9번)와 헷갈리지 마세요. 하나는 "지금 인쇄 가능한 물리적 상태"를 확인하는 것이고, 다른 하나는 "어떤 명령어 언어를 쓰는 프린터인지" 확인하는 것입니다. 용도가 완전히 다릅니다.
실무 팁: 인쇄 실패 문의가 들어오면 대부분 이 메소드로 원인이 바로 나옵니다(용지 없음 / 덮개 열림이 8~90%). 에러 로그에 이 상태값을 같이 남겨두면 나중에 디버깅이 훨씬 편합니다.
12. 자주 막히는 지점 정리
증상
원인일 가능성이 높은 것
확인할 메소드
스캔은 되는데 앱이 값을 못 받음
DataWedge 프로필의 Output Intent Action/Category가 앱의 리시버와 안 맞음
4번 Intent Output 설정 재확인
스캔 버튼을 눌러도 반응 없음
스캐너가 이미 다른 상태(busy)이거나 프로필이 이 앱에 연결 안 됨
6번 Notification으로 상태 확인
프린터 연결은 되는데 인쇄가 안 됨
용지/덮개/일시정지 문제, 혹은 ZPL 문법 오류
11번 getCurrentStatus
특정 기기에서만 스캔이 안 됨
기기별로 내장 스캐너 종류가 다름
3번 Enumerate Scanners
React(PC)에서는 되는데 모바일에서 안 됨
Connection 클래스를 잘못 선택 (Tcp vs Bluetooth vs Usb)
8번 Connection 종류 재확인
13. 다양한 예제 모음
지금까지는 메소드 하나씩 개념만 봤다면, 여기서는 상황별로 실제 쓸 법한 코드를 모아뒀습니다. 우리 프로젝트가 "React(PC) + 안드로이드 모바일"을 둘 다 다루니, 두 환경 예제를 분리해서 정리했습니다.
13-1. [스캐너] 여러 바코드 종류를 구분해서 처리하기
상황: QR코드는 "상품 상세 페이지 이동"으로, 일반 바코드(EAN13 등)는 "재고 조회"로 다르게 동작시키고 싶을 때.
@Override
public void onReceive(Context context, Intent intent) {
    String barcode = intent.getStringExtra("com.symbol.datawedge.data_string");
    String symbology = intent.getStringExtra("com.symbol.datawedge.label_type");

    if (symbology == null || barcode == null) return;

    switch (symbology) {
        case "LABEL-TYPE-QRCODE":
            // QR코드 → 상세 페이지 이동
            openProductDetail(barcode);
            break;
        case "LABEL-TYPE-EAN13":
        case "LABEL-TYPE-CODE128":
            // 일반 바코드 → 재고 조회
            lookupInventory(barcode);
            break;
        default:
            Log.w("Scan", "처리 안 하는 심볼로지: " + symbology);
    }
}
포인트: label_type 값으로 분기하면, 하나의 리시버로 QR과 일반 바코드를 동시에 지원할 수 있습니다. 프로젝트에서 "QR 생성 + 바코드 스캔"을 같이 요구하니 이 패턴이 바로 쓰일 가능성이 높습니다.
13-2. [스캐너] 블루투스 핸드 스캐너를 지정해서 트리거하기
상황: 모바일 기기 여러 대에 블루투스 스캐너(RS6000 등)를 페어링해서 쓰는 경우, 특정 스캐너로만 트리거를 보내고 싶을 때.
Intent i = new Intent();
i.setAction("com.symbol.datawedge.api.ACTION");
i.putExtra("scanner_selection_by_identifier", "BLUETOOTH_RS6000");
i.putExtra("com.symbol.datawedge.api.SOFT_SCAN_TRIGGER", "TOGGLE_SCANNING");
sendBroadcast(i);
포인트: 이 식별자 값은 3번 항목(Enumerate Scanners)으로 미리 조회해서 리스트를 뽑아놓고, 사용자가 설정 화면에서 고르게 하는 식으로 많이 씁니다.
13-3. [스캐너] Kotlin으로 스캔 결과 받기
Java 예제만 있으면 실무에서 바로 못 쓰는 경우가 많아 Kotlin 버전도 같이 둡니다.
class ScanReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val barcode = intent.getStringExtra("com.symbol.datawedge.data_string") ?: return
        val symbology = intent.getStringExtra("com.symbol.datawedge.label_type")
        Log.d("Scan", "값: $barcode / 종류: $symbology")
    }
}
13-4. [프린터] 연결 방식 3종 비교 예제
상황별로 어떤 Connection을 쓸지 그대로 복사해서 바꿔 쓸 수 있게 정리했습니다.
// ① 네트워크(Wi-Fi/이더넷) 프린터 — 사무실/창고 고정형 프린터에 흔함
Connection conn = new TcpConnection("192.168.1.100", TcpConnection.DEFAULT_ZPL_TCP_PORT);

// ② USB 직결 프린터 — PC 옆에 유선으로 붙여둔 라벨 프린터
Connection conn = new UsbConnection(usbDevice, usbManager);

// ③ 블루투스 프린터 — 모바일에서 들고 다니며 즉석 출력할 때
Connection conn = new BluetoothConnection("AA:BB:CC:DD:EE:FF"); // 프린터 MAC 주소

// 셋 다 이후 사용법은 동일합니다
conn.open();
ZebraPrinter printer = ZebraPrinterFactory.getInstance(conn);
포인트: 연결을 여는 방법(생성자)만 다르고, 그 다음부터는 ZebraPrinterFactory.getInstance()부터 완전히 동일한 코드를 씁니다. 즉 "연결 방식 선택 로직"만 분리해두면 나머지 인쇄 코드는 재사용 가능합니다.
13-5. [프린터] ZPL 라벨 다양하게 만들어보기
ZPL은 처음 보면 암호처럼 보이지만, 패턴이 몇 개 없습니다. 자주 쓰는 조합만 모았습니다.
① 텍스트만 인쇄
^XA
^FO50,50^A0N,40,40^FDHello Zebra^FS
^XZ

② QR코드 인쇄 (재고 관리에 가장 많이 씀)
^XA
^FO50,50^BQN,2,7^FDQA,ORDER-1234^FS
^XZ

③ 1D 바코드(CODE128) + 사람이 읽을 텍스트 같이 인쇄
^XA
^FO50,50^BY2
^BCN,100,Y,N,N
^FD8801234567890^FS
^XZ

④ 텍스트 + QR코드 조합 (라벨에 흔히 쓰는 형태)
^XA
^FO50,30^A0N,30,30^FD상품명: 무선 이어폰^FS
^FO50,80^A0N,25,25^FD주문번호: ORDER-1234^FS
^FO50,130^BQN,2,6^FDQA,ORDER-1234^FS
^XZ
읽는 법 팁:
^XA ~ ^XZ: 라벨 시작/끝 (모든 ZPL은 이 안에 들어감)
^FO50,50: 시작 위치(가로50, 세로50 지점부터)
^A0N,40,40: 폰트 크기 지정
^BQ: QR코드, ^BC: CODE128 바코드
^FD ... ^FS: 실제로 찍을 데이터(Field Data)
13-6. [프린터] React(PC)에서 브라우저로 바로 인쇄하기 — Browser Print SDK
지금까지는 Java/Kotlin(Link-OS SDK) 기준이었는데, PC의 React 화면에서 별도 백엔드 없이 바로 프린터로 쏘고 싶다면 Zebra가 제공하는 "Browser Print"라는 별도 도구를 씁니다. 사용자 PC에 Browser Print 에이전트 프로그램을 한 번 설치해두면, 웹페이지의 JS 코드가 로컬 네트워크의 프린터와 직접 통신할 수 있게 해줍니다.
// npm i zebra-browser-print-wrapper
import ZebraBrowserPrintWrapper from "zebra-browser-print-wrapper";

async function printOrderLabel(orderId) {
  const browserPrint = new ZebraBrowserPrintWrapper();

  // 1. 기본 프린터 가져오기
  const defaultPrinter = await browserPrint.getDefaultPrinter();
  browserPrint.setPrinter(defaultPrinter);

  // 2. 인쇄 가능한 상태인지 확인
  const status = await browserPrint.checkPrinterStatus();
  if (!status.isReadyToPrint) {
    console.error("인쇄 불가:", status.errors); // 예: "Paper out", "Media Door Open"
    return;
  }

  // 3. ZPL 라벨 문자열 만들어서 전송
  const zpl = `
^XA
^FO50,50^A0N,30,30^FD주문번호: ${orderId}^FS
^FO50,100^BQN,2,6^FDQA,${orderId}^FS
^XZ`;
  browserPrint.print(zpl);
}
포인트: 이 방식은 "PC 쪽 React 화면에서 QR코드를 즉석으로 만들어 바로 인쇄"하는 흐름에 정확히 맞습니다. checkPrinterStatus()가 11번 항목(getCurrentStatus)의 웹 버전이라고 생각하면 됩니다.
13-7. [프린터] 인쇄 전 상태 체크 → 에러 메시지까지 사용자에게 보여주기
상황: 그냥 "인쇄 실패"라고만 뜨면 현장 사용자가 뭘 고쳐야 할지 모릅니다. 원인을 구체적으로 안내하는 패턴입니다.
PrinterStatus status = printer.getCurrentStatus();

String userMessage;
if (status.isReadyToPrint) {
    userMessage = null; // 정상, 바로 인쇄 진행
} else if (status.isPaperOut) {
    userMessage = "용지가 없습니다. 라벨 용지를 채워주세요.";
} else if (status.isHeadOpen) {
    userMessage = "프린터 덮개가 열려있습니다. 덮개를 닫아주세요.";
} else if (status.isPaused) {
    userMessage = "프린터가 일시정지 상태입니다. PAUSE 버튼을 다시 눌러주세요.";
} else {
    userMessage = "알 수 없는 오류입니다. 프린터를 재시작해주세요.";
}

if (userMessage != null) {
    showToast(userMessage); // 현장 작업자에게 바로 안내
} else {
    connection.write(zpl.getBytes());
}
포인트: 실무에서 가장 많이 받는 클레임이 "인쇄가 왜 안 되냐"는 문의입니다. 이 패턴을 넣어두면 문의 자체가 크게 줄어듭니다.
14. 화면(UI)에서 값이 실제로 어떻게 넘어가는지 — 전체 흐름
지금까지는 메소드 하나하나가 "어떻게 생겼는지"였다면, 여기서는 **"사용자가 화면에서 뭔가를 입력하거나 누르면, 그 값이 어떤 경로로 메소드까지 흘러가는지"**를 처음부터 끝까지 이어서 보여드립니다. 이미지(로고) 전달도 포함합니다.
14-1. [스캐너] 화면 ↔ DataWedge 연결 지점 3곳
스캔값이 화면에 뜨기까지는 사실 연결 지점이 3군데 있습니다. 이 3개가 서로 맞아야 값이 넘어옵니다. 하나라도 다르면 아예 안 넘어옵니다.
① AndroidManifest.xml — "이 값이 오면 나를 깨워라"라고 등록하는 곳
<activity android:name=".ScanActivity"
    android:launchMode="singleTop">
    <intent-filter>
        <action android:name="com.beyondf.zebra.SCAN_ACTION" />
        <category android:name="android.intent.category.DEFAULT" />
    </intent-filter>
</activity>
② DataWedge 프로필 설정 — 위 값과 똑같이 맞춰서 "여기로 보내라"고 지정 DataWedge 앱 → 프로필 생성 → Intent Output 항목에서
Intent Action: com.beyondf.zebra.SCAN_ACTION (①과 반드시 문자 그대로 동일)
Intent Category: android.intent.category.DEFAULT
Intent Delivery: Broadcast Intent (또는 Start Activity)
③ 화면(Activity/Fragment) 코드 — 실제로 받아서 화면에 뿌리는 곳
public class ScanActivity extends AppCompatActivity {

    private EditText etScanResult; // 화면에 값 보여줄 입력창

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_scan);
        etScanResult = findViewById(R.id.etScanResult);
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        handleScan(intent);
    }

    private void handleScan(Intent intent) {
        String barcode = intent.getStringExtra("com.symbol.datawedge.data_string");
        if (barcode != null) {
            etScanResult.setText(barcode);       // ← 화면 입력창에 값 표시
            lookupInventory(barcode);             // ← 그 값으로 재고 조회 API 호출
        }
    }
}
요약: ① Manifest에 적은 Action 문자열, ② DataWedge 프로필에 적은 Action 문자열, 둘이 토씨 하나까지 똑같아야 합니다. 스캔은 되는데 화면에 값이 안 뜬다고 할 때 원인의 90%가 이 둘의 오타/불일치입니다.
14-2. [스캐너] 화면 버튼으로 스캔 시작하기 (풀 코드)
"화면에 스캔 버튼을 두고, 누르면 스캐너가 켜지는" 가장 흔한 화면 구성입니다.
<!-- activity_scan.xml -->
<Button
    android:id="@+id/btnScan"
    android:text="스캔 시작" />
<EditText
    android:id="@+id/etScanResult"
    android:hint="스캔 결과가 여기 표시됩니다" />
Button btnScan = findViewById(R.id.btnScan);
btnScan.setOnClickListener(v -> {
    Intent i = new Intent();
    i.setAction("com.symbol.datawedge.api.ACTION");
    i.putExtra("com.symbol.datawedge.api.SOFT_SCAN_TRIGGER", "TOGGLE_SCANNING");
    sendBroadcast(i);
    // 이후 결과는 위 14-1의 handleScan()으로 별도로 들어옵니다.
    // (이 버튼 클릭 자체는 결과를 안 주고, "스캔을 켜라"는 명령만 보냅니다)
});
포인트: 버튼 클릭과 스캔 결과 수신은 완전히 분리된 두 개의 이벤트입니다. 버튼을 누르는 순간 값이 오는 게 아니라, "스캔해라"라는 명령만 보내고, 실제 바코드가 인식되면 그때서야 onNewIntent(14-1)로 값이 도착합니다. 이 두 단계를 하나로 착각해서 헷갈리는 경우가 많습니다.
14-3. [프린터] 화면 입력값(상품명·수량)으로 라벨 만들어 인쇄하기 (Android, 풀 코드)
사용자가 화면에서 상품명과 수량을 입력하고 "인쇄" 버튼을 누르면, 그 값이 그대로 라벨에 찍히는 흐름입니다.
<EditText android:id="@+id/etProductName" android:hint="상품명" />
<EditText android:id="@+id/etQty" android:hint="수량" android:inputType="number" />
<Button android:id="@+id/btnPrint" android:text="라벨 인쇄" />
EditText etProductName = findViewById(R.id.etProductName);
EditText etQty = findViewById(R.id.etQty);
Button btnPrint = findViewById(R.id.btnPrint);

btnPrint.setOnClickListener(v -> {
    // 1. 화면 입력값 꺼내기
    String productName = etProductName.getText().toString();
    String qty = etQty.getText().toString();
    String orderId = "ORDER-" + System.currentTimeMillis();

    // 2. 꺼낸 값을 ZPL 문자열 "안에" 그대로 끼워넣기
    String zpl = String.format(
        "^XA\n" +
        "^FO50,30^A0N,30,30^FD상품명: %s^FS\n" +
        "^FO50,80^A0N,25,25^FD수량: %s^FS\n" +
        "^FO50,130^BQN,2,6^FDQA,%s^FS\n" +
        "^XZ",
        productName, qty, orderId);

    // 3. 백그라운드 스레드에서 실제 전송 (네트워크/블루투스 작업은 메인스레드 금지)
    new Thread(() -> {
        try {
            Connection connection = new TcpConnection("192.168.1.100", TcpConnection.DEFAULT_ZPL_TCP_PORT);
            connection.open();
            connection.write(zpl.getBytes());
            connection.close();
            runOnUiThread(() -> Toast.makeText(this, "인쇄 완료", Toast.LENGTH_SHORT).show());
        } catch (Exception e) {
            runOnUiThread(() -> Toast.makeText(this, "인쇄 실패: " + e.getMessage(), Toast.LENGTH_SHORT).show());
        }
    }).start();
});
포인트: 화면 값 → 문자열 조합(String.format) → connection.write(), 딱 이 3단계가 전부입니다. ZPL은 결국 "문자열"이기 때문에, 어떤 값이든 그 자리에 텍스트로 끼워넣기만 하면 됩니다.
14-4. [프린터] 로고 이미지를 라벨에 함께 인쇄하기 (Android)
텍스트/QR과 달리 이미지(로고)는 ZPL 문자열 안에 바로 못 넣습니다. 이미지 따로, 텍스트 따로 전송한 뒤 "한 라벨 안에 합쳐서 찍어라"라고 지시하는 방식입니다.
// 1. 이미지를 Bitmap으로 준비 (앱 리소스에 있는 로고 예시)
Bitmap logoBitmap = BitmapFactory.decodeResource(getResources(), R.drawable.company_logo);

new Thread(() -> {
    try {
        Connection connection = new TcpConnection("192.168.1.100", TcpConnection.DEFAULT_ZPL_TCP_PORT);
        connection.open();
        ZebraPrinter printer = ZebraPrinterFactory.getInstance(connection);

        // 2. 라벨의 앞부분(로고가 들어갈 자리까지) 전송
        String firstHalf = "^XA^FO20,20";
        connection.write(firstHalf.getBytes());

        // 3. 이미지 전송 — insideFormat=true 로 줘야 "같은 라벨 안"에 합쳐짐
        //    (x=1, y=1, 폭/높이는 이미지 크기에 맞춰 조절)
        printer.printImage(new ZebraImageAndroid(logoBitmap), 1, 1, 200, 100, true);

        // 4. 나머지 텍스트/QR 등 라벨 뒷부분 전송
        String secondHalf = "^FO20,150^A0N,30,30^FD상품명: 무선 이어폰^FS^XZ";
        connection.write(secondHalf.getBytes());

        connection.close();
    } catch (Exception e) {
        Log.e("Print", "이미지 인쇄 실패", e);
    }
}).start();
포인트: printImage()의 마지막 파라미터(insideFormat)를 반드시 true로 줘야 이미지가 로고처럼 같은 라벨 한 장 안에 합쳐집니다. false로 주면 이미지만 따로 한 장이 더 뽑혀 나옵니다(실무에서 가장 많이 하는 실수).
14-5. [프린터] React 화면 — 폼 입력부터 인쇄 버튼까지 (풀 컴포넌트)
PC(React) 쪽은 13-6에서 본 Browser Print 래퍼를 화면 컴포넌트에 완전히 끼워넣은 형태입니다.
import { useState } from "react";
import ZebraBrowserPrintWrapper from "zebra-browser-print-wrapper";

function LabelPrintForm() {
  const [productName, setProductName] = useState("");
  const [qty, setQty] = useState("");
  const [printing, setPrinting] = useState(false);

  const handlePrint = async () => {
    setPrinting(true);
    try {
      const browserPrint = new ZebraBrowserPrintWrapper();
      const printer = await browserPrint.getDefaultPrinter();
      browserPrint.setPrinter(printer);

      const status = await browserPrint.checkPrinterStatus();
      if (!status.isReadyToPrint) {
        alert("인쇄 불가: " + status.errors);
        return;
      }

      const orderId = "ORDER-" + Date.now();
      // 화면 입력값(productName, qty)을 ZPL 문자열 안에 그대로 끼워넣기
      const zpl = `
^XA
^FO50,30^A0N,30,30^FD상품명: ${productName}^FS
^FO50,80^A0N,25,25^FD수량: ${qty}^FS
^FO50,130^BQN,2,6^FDQA,${orderId}^FS
^XZ`;

      browserPrint.print(zpl);
    } finally {
      setPrinting(false);
    }
  };

  return (
    <div>
      <input
        placeholder="상품명"
        value={productName}
        onChange={(e) => setProductName(e.target.value)}
      />
      <input
        placeholder="수량"
        value={qty}
        onChange={(e) => setQty(e.target.value)}
      />
      <button onClick={handlePrint} disabled={printing}>
        {printing ? "인쇄 중..." : "라벨 인쇄"}
      </button>
    </div>
  );
}
흐름 요약: <input>의 onChange → React state(productName, qty) → 버튼 클릭(onClick) → state 값을 ZPL 문자열에 꽂아넣기 → browserPrint.print(zpl). Android 예제(14-3)와 구조가 완전히 동일하고, "값을 어디서 꺼내느냐"만 EditText.getText() vs React state로 다를 뿐입니다.
14-6. [프린터] React 화면에 로고 이미지 넣기
웹에서는 이미지를 Bitmap 대신 Base64 문자열이나 파일로 다루는데, ZPL이 이해할 수 있는 이미지 포맷(^GFA 그래픽 명령)으로 변환해야 합니다. 이 변환 자체는 직접 짜기보다, 아래처럼 로고를 미리 한 번 변환해서 프린터에 저장해두고, 그 다음부터는 이름으로 불러 쓰는 방식이 실무에서 훨씬 편합니다.
// 로고는 미리 한 번만 프린터 메모리에 저장해둔다고 가정 (예: "LOGO.PNG"라는 이름으로 저장됨)
// 그 다음부터는 매 인쇄마다 이미지를 다시 보낼 필요 없이 이름만 불러오면 됩니다.
const zpl = `
^XA
^FO20,20^XGR:LOGO.PNG,1,1^FS
^FO20,150^A0N,30,30^FD상품명: ${productName}^FS
^XZ`;
browserPrint.print(zpl);
포인트: ^XG는 "프린터에 이미 저장된 이미지를 불러와서 찍어라"는 명령입니다. 로고처럼 매번 안 바뀌는 이미지는 이 방식이 훨씬 빠르고 간단합니다(이미지 데이터를 매번 통째로 안 보내도 됨). 저장 자체는 Zebra의 "Zebra Setup Utilities" 같은 도구로 미리 한 번 해두면 됩니다.
참고한 공식 자료 (더 깊이 볼 때)
DataWedge 전체 API 목록: https://techdocs.zebra.com/datawedge/latest/guide/api/
DataWedge 기본 예제(안드로이드 스튜디오 프로젝트): https://techdocs.zebra.com/datawedge/7-3/guide/samples/basicintent1/
Link-OS SDK 다운로드(PC/모바일 공통): https://www.zebra.com/kr/ko/support-downloads/software/printer-software/link-os-multiplatform-sdk.html
Link-OS Android API 문서(ZebraPrinter 클래스): https://techdocs.zebra.com/link-os/2-14/android/content/com/zebra/sdk/printer/zebraprinter
React용 Browser Print 래퍼 패키지: https://www.npmjs.com/package/zebra-browser-print-wrapper