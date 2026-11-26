# 호환성 및 수동 테스트

## 목표와 검증 상태

| 환경 | 빌드 대상 | 실기 실행/입력/Shell 검증 |
|---|---|---|
| Windows 7 SP1 x86 | .NET Framework 4.8 / x86 | 미검증 |
| Windows 7 SP1 x64 | .NET Framework 4.8 / x86, x64 | 미검증 |
| Windows 10 x86 | .NET Framework 4.8 / x86 | 미검증 |
| Windows 10 x64 | .NET Framework 4.8 / x86, x64 | 미검증 |
| Windows 11 x64 | .NET Framework 4.8 / x86, x64 | 일반 사용자 데스크톱 미검증 |

개발 호스트 API 버전은 `10.0.26300.0`입니다. 설치된 Framework 어셈블리를 이용한 제한적 컴파일 및 프로세스 테스트를 수행했습니다. 2026-11-27 x86/x64 로컬 Release 회귀 테스트는 각각 26개 통과했습니다. [실행 기록](validation.md)을 참고하세요. 수정 후 GitHub CI·설치 EXE 생성·Sonar 분석기는 이 환경에서 검증하지 못했습니다. CI 성공은 Windows 7 실행 호환성 검증이 아닙니다.

## Framework와 DPI

Setup EXE는 .NET Framework 4.8 오프라인 설치본을 포함하도록 구성했습니다. 4.8 이상이 있으면 건너뛰고, 없으면 포함된 설치본을 실행합니다. 별도 런타임 다운로드는 필요하지 않으며 Framework 설치 시 UAC·재부팅이 필요할 수 있습니다. 앱은 사용자별 폴더에 설치하며 제거 후 메모 데이터는 보존합니다. Windows 7 SP1의 OS 업데이트/인증서/SHA-2 지원 등의 선행 요구조건은 [Microsoft 설치 안내](https://learn.microsoft.com/en-us/dotnet/framework/install/)를 따르세요. OS 업데이트는 설치 파일에 포함하지 않습니다.

설치 파일은 Inno Setup 6.2.2로 빌드합니다. 빌드 머신만 공식 런타임·컴파일러 다운로드를 위해 인터넷이 필요합니다. `scripts/package.ps1 -RuntimePath <net48.exe> -CompilerPath <ISCC.exe>`로 미리 준비한 파일을 지정할 수 있습니다. 런타임과 다운로드한 컴파일러 설치본은 Authenticode 서명과 게시자를 검증합니다. 결과는 `artifacts/windows_stricky_setup_x86.exe`, `windows_stricky_setup_x64.exe`, `SHA256SUMS.txt`입니다. 파일 생성 및 설치 실기는 아직 미검증입니다.

manifest는 Windows 7에서도 이해할 수 있는 **system DPI aware**를 사용합니다. 최신 DPI API를 동적으로라도 호출하지 않습니다. WPF DIP로 위치와 크기를 보관하고, `CompositionTarget.TransformFromDevice`를 통해 모니터 작업 영역 좌표를 같은 DIP 공간으로 변환합니다. 음수 좌표와 작업 영역 교집합을 지원하고 해상도/디스플레이 변경, 절전 복귀 및 창 재생성 시 화면 안으로 보정합니다.

서로 다른 DPI 모니터에서도 system DPI 모델을 일관되게 유지합니다. Windows가 보조 모니터에서 비트맵 확대할 수 있어 텍스트가 다소 흐려질 수 있습니다. per-monitor v2의 선명도나 실행 중 시스템 DPI 변경 즉시 재레이아웃은 보장하지 않습니다. 시스템 DPI를 바꾼 후 앱 재시작(운영체제에 따라 로그아웃)이 필요할 수 있습니다. 서로 다른 DPI/모니터 탈착 실기는 아직 검증하지 않았습니다.

P/Invoke 포인터/핸들은 IntPtr이며 사용하지 않는 Get/SetWindowLong·SetParent 선언은 제거했습니다. HWND가 무효화되어도 요청 모드를 보존하고 Normal fallback을 시도합니다. Windows 7에 없는 WebView/현대 .NET/최신 DPI API는 사용하지 않습니다.

## OS별 체크리스트

각 대상 OS/아키텍처별로 **별도의 사본**에 날짜, OS 빌드, Framework 버전, 배율, 모니터, 앱 버전 및 결과를 기록합니다. 아래는 모두 미실행 상태입니다. 실패는 재현 순서와 내용 없는 로그로 남깁니다. 저장 강제 종료 실험은 테스트 계정/백업 사본에서만 수행합니다.

- [ ] 4.8 이상 설치 환경: x86/x64 Setup 설치·최초 실행·트레이·제거·메모 보존
- [ ] 4.8 없는 Win7 SP1/Win10: 네트워크 차단 후 포함본 설치·UAC 취소·실패 코드·재부팅 후 재실행
- [ ] Win7: OS 필수 업데이트 누락 시 설치 실패 안내, 업데이트 적용 환경에서 재검증
- [ ] 새 메모, 한글/영문/IME, 줄바꿈/자동 줄바꿈, 복사/붙여넣기/실행 취소
- [ ] 제목 클릭 직접 편집·한글 IME·자동 저장, 옆 손잡이 드래그 이동, 가장자리 크기 변경
- [ ] 본문 왼쪽 위 입력, 하단 투명도·글자 크기 슬라이더 즉시 반영 및 재실행 후 유지
- [ ] 메모 메뉴·환경설정의 배경색/글자색 → 색상표 클릭·드래그·색조·RGB 동기화·미리보기·저장·취소, 작은 화면 스크롤
- [ ] 글씨체 선택·한글/영문 견본·메모별/새 메모 기본값·재실행/복원 후 유지·없는 글꼴 대체 표시
- [ ] 설치 EXE·프로세스·작업 표시줄·트레이·제거 항목의 새 아이콘
- [ ] 환경설정 버전 표시·온라인/오프라인 업데이트 확인·다운로드 페이지 열기
- [ ] 복제 시 새 ID/동일 내용, 삭제 확인의 기본값 아니오, 취소 및 실제 삭제
- [ ] ×/Alt+F4/작업 표시줄 닫기 → 삭제 확인(아니오 기본), 취소 시 유지·확인 시 삭제·재실행 후 삭제 유지
- [ ] 메모 관리 진입점 제거, 기존 숨긴 메모는 실행·모두 표시로 재표시
- [ ] 기본 색상 32종, 사용자 색상, 글자 색/크기/투명도 유지
- [ ] Normal 기본: 클릭·작업 표시줄·바로가기 재실행 시 전면 표시, 항상 위로 바뀌지 않음
- [ ] Always On Top: 일반 앱 위, Normal로 되돌릴 때 투명도/표시 상태 유지
- [ ] Desktop: 일반 앱 뒤/아이콘 호스트 위, 입력과 IME 편집 및 드래그
- [ ] Desktop 영역 밖 아이콘 클릭과 드래그, 메모를 이동해 가린 아이콘 접근
- [ ] Win+D 두 번, Win+M 후 복귀, 모드별 실제 노출/포커스 기록
- [ ] Explorer 재시작 시 프로세스/메모 HWND 생존, 3회 이하 복구 또는 Normal fallback
- [ ] Shell 없는 환경/유효하지 않은 호스트에서 fallback과 요청 모드 보존
- [ ] 해상도/주 모니터 변경, 음수 좌표 모니터, 모니터 탈착 후 작업 영역 재배치
- [ ] 100%/125%/150% 및 서로 다른 DPI의 모니터 간 이동
- [ ] 절전 복귀, Windows 재부팅 후 데이터/위치/표시 모드 유지
- [ ] 새 설치 자동 실행 자동 등록·다음 로그인 시 메모 표시·기존 끄기 설정 유지·제거 후 Run 삭제, 공백 경로 EXE
- [ ] Windows Stricky 이름으로 시작 메뉴 검색·바탕화면 바로가기 실행·업그레이드 시 예전 바로가기 정리
- [ ] 실행·재실행 시 기존 메모 모두 표시(개수 유지), 빈 목록일 때만 새 메모 1개 생성, 두 번째 프로세스 종료
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
- [ ] 한국어/영어 Windows 표시 언어에 따른 앱·설치 화면 자동 선택, 기타 언어의 영어 fallback

## 공식 참고

- [.NET Framework system requirements](https://learn.microsoft.com/en-us/dotnet/framework/get-started/system-requirements)
- [.NET Framework 설치 안내](https://learn.microsoft.com/en-us/dotnet/framework/install/)
- [WPF DPI 모델](https://learn.microsoft.com/en-us/windows/win32/hidpi/declaring-managed-apps-dpi-aware)
- [DPI awareness 지원 범위](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
