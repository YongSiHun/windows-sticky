# 호환성 및 수동 테스트

## 목표와 검증 상태

| 환경 | 빌드 대상 | 실기 실행/입력/Shell 검증 |
|---|---|---|
| Windows 7 SP1 x86 | .NET Framework 4.8 / x86 | 미검증 |
| Windows 7 SP1 x64 | .NET Framework 4.8 / x86, x64 | 미검증 |
| Windows 10 x86 | .NET Framework 4.8 / x86 | 미검증 |
| Windows 10 x64 | .NET Framework 4.8 / x86, x64 | 미검증 |
| Windows 11 x64 | .NET Framework 4.8 / x86, x64 | 일반 사용자 데스크톱 미검증 |

개발 호스트 API 버전은 `10.0.26300.0`입니다. 설치된 Framework 어셈블리를 이용한 제한적 컴파일 및 프로세스 테스트를 수행했습니다. x86/x64 로컬 Release 빌드와 자동 회귀 테스트가 있으며, 최종 실행 기록은 [validation.md](validation.md)에 기록합니다. GitHub CI는 아직 실행하지 않았습니다. CI 성공은 Windows 7 실행 호환성 검증이 아닙니다.

## Framework와 DPI

Windows 7 SP1에서 .NET Framework 4.8을 먼저 설치해야 합니다. OS 업데이트/인증서/SHA-2 지원 등의 선행 요구조건은 Microsoft 설치 안내를 따르세요. 앱 실행 및 HKCU 자동 실행 등록에 관리자 권한은 필요하지 않습니다. Framework 자체 설치 권한은 별도입니다.

manifest는 Windows 7에서도 이해할 수 있는 **system DPI aware**를 사용합니다. 최신 DPI API를 동적으로라도 호출하지 않습니다. WPF DIP로 위치와 크기를 보관하고, `CompositionTarget.TransformFromDevice`를 통해 모니터 작업 영역 좌표를 같은 DIP 공간으로 변환합니다. 음수 좌표와 작업 영역 교집합을 지원하고 해상도/디스플레이 변경, 절전 복귀 및 창 재생성 시 화면 안으로 보정합니다.

서로 다른 DPI 모니터에서도 system DPI 모델을 일관되게 유지합니다. Windows가 보조 모니터에서 비트맵 확대할 수 있어 텍스트가 다소 흐려질 수 있습니다. per-monitor v2의 선명도나 실행 중 시스템 DPI 변경 즉시 재레이아웃은 보장하지 않습니다. 시스템 DPI를 바꾼 후 앱 재시작(운영체제에 따라 로그아웃)이 필요할 수 있습니다. 서로 다른 DPI/모니터 탈착 실기는 아직 검증하지 않았습니다.

P/Invoke 포인터/핸들은 IntPtr이며 Get/SetWindowLong은 포인터 크기에 따라 32/64 엔트리 포인트로 분기합니다. HWND가 무효화되어도 요청 모드를 보존하고 Normal fallback을 시도합니다. Windows 7에 없는 WebView/현대 .NET/최신 DPI API는 사용하지 않습니다.

## OS별 체크리스트

각 대상 OS/아키텍처별로 **별도의 사본**에 날짜, OS 빌드, Framework 버전, 배율, 모니터, 앱 버전 및 결과를 기록합니다. 아래는 모두 미실행 상태입니다. 실패는 재현 순서와 내용 없는 로그로 남깁니다. 저장 강제 종료 실험은 테스트 계정/백업 사본에서만 수행합니다.

- [ ] 일반 권한으로 압축 해제 및 최초 실행, 트레이 확인
- [ ] 새 메모, 한글/영문/IME, 줄바꿈/자동 줄바꿈, 복사/붙여넣기/실행 취소
- [ ] 제목 변경, 제목 드래그 이동, 모든 가장자리 크기 변경, 최소 크기
- [ ] 복제 시 새 ID/동일 내용, 삭제 확인의 기본값 아니오, 취소 및 실제 삭제
- [ ] ×/Alt+F4가 숨기기이고 트레이 유지, 관리에서 재표시
- [ ] 제목/본문 검색, 숨긴 메모 목록, 전체 표시/숨기기
- [ ] 기본 프리셋 7종, 사용자 색상, 글자 색/크기/투명도 유지
- [ ] Normal: 일반 창에 가려짐, 재활성화 가능
- [ ] Always On Top: 일반 앱 위, Normal로 되돌릴 때 투명도/표시 상태 유지
- [ ] Desktop: 일반 앱 뒤/아이콘 호스트 위, 입력과 IME 편집 및 드래그
- [ ] Desktop 영역 밖 아이콘 클릭과 드래그, 메모를 이동해 가린 아이콘 접근
- [ ] Win+D 두 번, Win+M 후 복귀, 모드별 실제 노출/포커스 기록
- [ ] Explorer 재시작 시 프로세스/메모 HWND 생존, 3회 이하 복구 또는 Normal fallback
- [ ] Shell 없는 환경/유효하지 않은 호스트에서 fallback과 요청 모드 보존
- [ ] 해상도/주 모니터 변경, 음수 좌표 모니터, 모니터 탈착 후 작업 영역 재배치
- [ ] 100%/125%/150% 및 서로 다른 DPI의 모니터 간 이동
- [ ] 절전 복귀, Windows 재부팅 후 데이터/위치/표시 모드 유지
- [ ] 자동 실행 켜기/끄기, 공백 경로 EXE, 새 경로 실행 후 Run 값 갱신
- [ ] 재실행 시 두 번째 프로세스 종료 및 기존 관리 화면 표시
- [ ] 다른 사용자/세션의 Mutex/Pipe와 분리, 현재 사용자만 Pipe 접근
- [ ] 트레이 메뉴 모든 항목, 트레이 종료 후 아이콘/프로세스 소멸
- [ ] JSON 내보내기/복원, 복원 직전 자동 백업, 잘못된/미래 스키마 거부
- [ ] notes/settings JSON 손상 후 정상 .bak 복구와 손상본 보존
- [ ] 데이터/백업 모두 손상 시 자동 덮어쓰기 차단, 정상 백업으로 복원
- [ ] 앱 강제 종료 후 마지막 저장본 유지 (미저장 500ms 구간 유실 허용)
- [ ] 저장 중 강제 종료 시 원본/정상 백업 중 하나 읽기 가능
- [ ] 복원 중 강제 종료 후 pending journal로 두 파일 재적용
- [ ] 디스크 부족/파일 잠금/권한 오류 후 기존 파일 유지 및 사용자 알림
- [ ] 정상 종료 저장 실패 시 앱으로 돌아가 별도 백업 가능
- [ ] 메모 1/10/50개 유휴 CPU·메모리 관찰, 반복 숨기기/표시 후 누수 관찰
- [ ] 한국어/영어 재시작 후 모든 메뉴/대화상자 문자열 확인

## 공식 참고

- [.NET Framework system requirements](https://learn.microsoft.com/en-us/dotnet/framework/get-started/system-requirements)
- [.NET Framework 설치 안내](https://learn.microsoft.com/en-us/dotnet/framework/install/)
- [WPF DPI 모델](https://learn.microsoft.com/en-us/windows/win32/hidpi/declaring-managed-apps-dpi-aware)
- [DPI awareness 지원 범위](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
