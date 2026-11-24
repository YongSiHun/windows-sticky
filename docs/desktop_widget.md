# Desktop 모드 기술 검증

## 선택한 접근

일반 최상위 WPF 창을 유지하고 Shell 아이콘 호스트 직전의 Z-order에 메모 HWND만 배치합니다. `GetShellWindow`, `EnumWindows`, `FindWindowEx(SHELLDLL_DefView)`, `GetWindowThreadProcessId`로 Shell 및 아이콘 호스트를 탐색하고 프로세스 일치를 확인합니다. 호스트가 Progman인지 WorkerW인지 단정하지 않습니다. 유효한 핸들과 foreground WinEventHook을 확보하지 못하면 Normal로 전환합니다.

`SetParent`는 사용하지 않습니다. WPF 에디터를 다른 프로세스의 자식으로 만들면 키보드 포커스, 좌표계, DPI awareness, HWND 수명이 결합되기 때문입니다. `SetParent`가 WS_CHILD/WS_POPUP을 자동 변경하지 않는 점도 고려했습니다. Shell을 owner로 지정하는 방식도 사용하지 않습니다. owner 소멸 시 메모 HWND까지 소멸하는 위험을 피하기 위해 부모·소유자·스타일은 원래 WPF 상태를 유지합니다. 모드 전환은 Topmost와 우리 HWND의 Z-order만 변경하고 WPF의 실시간 WS_VISIBLE/투명도 스타일을 과거 스냅샷으로 덮어쓰지 않습니다.

Normal은 일반 WPF 창, Always On Top은 WPF `Topmost`입니다. Desktop은 아이콘 호스트보다 위, 일반 앱보다 뒤에 위치하는 것을 목표로 합니다. 메모 영역 밖에는 입력을 가로채는 투명 오버레이가 없습니다. 메모가 차지하는 영역에서는 메모가 입력을 받고 그 뒤에 놓인 아이콘을 직접 클릭할 수는 없습니다. 메모를 이동하여 아이콘을 드러내면 됩니다.

## 이벤트와 실패 정책

- `RequestedMode`와 `ActualMode`는 분리합니다. 저장 모델은 요청 모드만 담고 하단 상태줄은 실제 fallback을 알립니다.
- Desktop 창 활성화 시 Z-order를 다시 낮추며 WM_WINDOWPOSCHANGING에서 자동 전면 이동을 억제합니다. 키보드 포커스는 별도로 차단하지 않습니다.
- WinEvent의 `EVENT_SYSTEM_FOREGROUND`를 out-of-context로 구독합니다. 바탕화면 호스트가 활성화되면 메모를 비활성 표시하고 Z-order를 재적용합니다. 상주 폴링은 없습니다.
- `TaskbarCreated` 방송으로 Explorer 재시작 후 복구를 예약합니다. 2초 간격 최대 3회만 시도합니다. 이후에는 Normal로 계속 사용하고 수동 모드 재선택으로 다시 시도할 수 있습니다.
- 관련 메시지마다 핸들 유효성을 확인합니다. 실패는 로그에 남기고 Normal로 복귀합니다. 이벤트 훅, 메시지 훅, 타이머는 창 제거 시 해제합니다.
- Shell 창의 부모, 스타일 또는 Z-order를 바꾸지 않습니다. WorkerW 생성을 강제하는 비공식 메시지도 보내지 않습니다.

## 보장하지 못하는 부분

Shell 내부 창 구조와 Win+D 동작은 안정적인 공개 위젯 계약이 아닙니다. Shell 버전에 따라 메모가 숨겨지거나 재표시 시점이 다를 수 있습니다. foreground 이벤트에 의한 보완은 best effort입니다. **Win+D/Win+M 이후 지속 노출과 Explorer 재시작 복구를 실제 OS에서 검증하기 전에는 보장할 수 없습니다.** 일반 앱이 새로 생성/재배치된 창의 Z-order를 별도로 바꾸는 경우도 추가 실기 검증이 필요합니다. 이 경우 Normal 또는 Always On Top이 대체 동작입니다.

## 실제 결과 (2026-11-24)

개발 호스트에서 OS API 버전 `10.0.26300.0`을 관찰했습니다. 제한된 실행 표면에서 Shell 아이콘 호스트 탐색이 실패하여 Desktop → Normal fallback이 발생했고, `notes.json`의 요청 모드는 `desktop`으로 유지됐습니다. 저장과 프로세스 생존을 확인했습니다.

자동 회귀 테스트는 WPF 창 생성, Native 모드 전환, Topmost, 실제 HWND 표시 비트, 부모 관계 보존, 투명도 보존을 확인합니다. 이것은 실제 마우스·키보드 입력의 증거가 아닙니다. computer-use 창 목록에서 격리 실행 창을 찾을 수 없어 육안 UI/입력 검증은 완료하지 못했습니다. Windows 7/10/11 실사용 데스크톱에서의 Desktop 입력, 아이콘 접근, Win+D, Win+M, Explorer 재시작은 모두 **미검증**입니다. [수동 체크리스트](compatibility.md)를 사용하세요.

## 참고한 공식 문서

- [SetParent: 스타일 및 DPI 제한](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)
- [SetWindowPos: Z-order](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)
- [Window features: 소유 창의 수명](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)
- [SetWinEventHook: 이벤트 전달 및 스레드](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
