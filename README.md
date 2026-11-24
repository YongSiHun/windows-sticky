# Sticky Note

Windows 바탕화면용 오프라인 스티키 노트입니다. C# / WPF / .NET Framework 4.8을 사용하며 실행 시 외부 라이브러리나 서버에 의존하지 않습니다. 기본 언어는 한국어이며 환경설정에서 영어를 선택할 수 있습니다.

## 실행

`sticky_note_windows_x86.zip` 또는 `sticky_note_windows_x64.zip`을 압축 해제하고 `StickyNote.exe`를 실행합니다. 관리자 권한은 필요하지 않습니다. x86 Windows에는 x86 패키지를 사용하세요. **.NET Framework 4.8이 미리 설치되어 있어야 합니다.** Windows 7은 **SP1** 및 Framework 설치에 필요한 운영체제 업데이트와 인증서/SHA-2 업데이트가 선행되어야 할 수 있습니다. 4.8.1 전용 앱이 아닙니다.

Windows 7 SP1, Windows 10, Windows 11의 x86/x64 실행을 목표로 구현했습니다. **Windows 7/10의 실제 실행 및 각 OS의 Desktop 동작은 아직 검증하지 못했습니다.** Windows 11은 x64 OS에서 x86 프로세스도 지원 대상으로 합니다. [검증 상태와 체크리스트](docs/compatibility.md)를 먼저 확인하세요.

## 사용

- 메모의 `+` 또는 트레이의 **새 메모**로 만듭니다. 본문에 바로 입력하며 줄바꿈·자동 줄바꿈·복사·붙여넣기·실행 취소를 지원합니다.
- 제목 부분을 드래그하여 이동하고 가장자리에서 크기를 바꿉니다. **⋯ → 제목 변경**으로 제목을 편집합니다.
- **⋯**에서 복제, 색상, 글자 크기, 투명도, 표시 모드를 바꿀 수 있습니다. 직접 입력 색상은 `#RRGGBB` 형식입니다.
- **×**는 숨기기입니다. 삭제는 메뉴/관리 화면에서 확인 후 실행합니다. 숨긴 메모는 **메모 관리**에서 다시 표시합니다.
- 트레이 더블 클릭은 관리 화면을 엽니다. 제목과 본문을 검색하고 전체 표시/숨기기를 할 수 있습니다.
- 트레이 **환경설정**에서 새 메모의 기본값, 자동 실행, 언어, 전체 백업/복원을 설정합니다. 언어 변경은 재시작 후 적용됩니다.
- 창을 모두 닫아도 트레이에서 실행됩니다. 트레이 **종료**로 저장 후 종료합니다. 트레이 초기화에 실패한 경우 관리 화면 닫기가 종료 동작을 대신합니다.

기본 모드는 **Desktop**입니다. Shell 연결 실패 시 **Normal**로 전환되며 메모 하단에 표시합니다. 저장된 사용자 요청은 유지됩니다. **Win+D / Win+M 지속 표시는 best effort이며 보장하지 않습니다.** 문제가 있는 PC에서는 **Normal** 또는 **Always On Top**을 선택하세요. [Desktop 설계 및 한계](docs/desktop_widget.md)

## 데이터와 복구

데이터는 `%LOCALAPPDATA%\StickyNote\`의 `notes.json`, `settings.json`, `backups\`, `logs\`에 저장됩니다. 실행 파일 교체 후에도 유지됩니다. 입력·이동·크기 변경은 500ms debounce로 저장하며, 이전 정상 파일은 `.bak`으로 유지합니다. 정상 종료 전에는 저장을 기다립니다. 마지막 저장 전 강제 종료된 변경분은 유실될 수 있습니다.

파일 손상 시 `.bak` 복구를 시도합니다. 손상 원본은 `.corrupt-*`로 보존하고, 복구 불가 또는 지원하지 않는 스키마이면 저장을 차단합니다. 이때 환경설정에서 정상 JSON 백업을 복원하세요. **자동 저장이 차단된 상태의 임시 편집 내용은 자동으로 저장되지 않습니다.** 필요한 내용은 백업 내보내기로 별도 보관할 수 있습니다. 복원 직전 자동 백업은 최근 10개를 유지하며, 원본 파일도 `.before-restore-*`로 보존합니다. 손상 원본 보존 파일은 자동으로 삭제하지 않습니다.

복원은 메모와 설정을 함께 교체합니다. 복원 도중 중단되면 다음 실행에서 기록된 트랜잭션을 재적용합니다. 잘못된 백업은 적용 전에 거부합니다. 로그에는 작업명, 예외 유형 및 네이티브 오류 번호만 기록하며 메모 내용은 기록하지 않습니다. JSON/백업은 일반 텍스트 파일입니다.

## 빌드와 테스트

Visual Studio 2022의 **.NET 데스크톱 개발**, MSBuild, .NET Framework 4.8 Developer Pack을 권장합니다. NuGet은 빌드용 공식 참조 어셈블리(`Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3)만 사용합니다.

Developer PowerShell에서:

```powershell
msbuild StickyNote.sln /restore /m /p:Configuration=Release /p:Platform=x86
.\tests\StickyNote.Tests\bin\x86\Release\StickyNote.Tests.exe
msbuild StickyNote.sln /restore /m /p:Configuration=Release /p:Platform=x64
.\tests\StickyNote.Tests\bin\x64\Release\StickyNote.Tests.exe
.\scripts\package.ps1
```

테스트는 외부 테스트 프레임워크 없이 실행하는 STA 콘솔 회귀 테스트입니다. 실패 시 종료 코드 1을 반환합니다. 저장·손상 복구·스키마 검증·백업·동시 저장·debounce·화면 보정·메모 작업·모드 전환·IPC를 다룹니다. `TestResults`에 격리된 데이터를 만들고 실제 사용자 데이터/자동 실행 레지스트리는 수정하지 않습니다. 네이티브 전환 테스트는 잠시 테스트 창을 엽니다. 수동 OS 검증은 별도입니다.

Debug 빌드에는 격리 실행 옵션이 있습니다. Release 빌드에는 포함되지 않습니다.

```powershell
.\src\StickyNote\bin\x64\Debug\StickyNote.exe --diagnostic-data-directory C:\project\windows-sticky\TestResults\interactive
```

제한된 이번 개발 환경에서는 Developer Pack/NuGet/VS Roslyn 구성이 없어서 아래 **검증 전용** 명령으로 설치된 Framework 어셈블리에 대해 컴파일했습니다. 배포 빌드에서는 위의 정식 4.8 참조 어셈블리 경로를 사용하세요.

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" StickyNote.sln /p:Configuration=Release /p:Platform=x64 /p:FrameworkPathOverride="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
```

## 배포와 유지보수

`scripts/package.ps1`은 `artifacts`에 두 ZIP과 `SHA256SUMS.txt`를 만듭니다. EXE와 config를 함께 배포합니다. DLL 병합은 하지 않습니다. `.github/workflows/build.yml`은 PR 및 main/master/develop push에서 두 아키텍처를 빌드/테스트합니다. `release.yml`은 `v*` 태그에서 빌드/테스트 후 ZIP, SHA-256, GitHub Release를 생성합니다. 워크플로는 작성했으며 GitHub에서 실제 실행하지 않았습니다.

[구조와 데이터 설계](docs/architecture.md), [Desktop 기술 검증](docs/desktop_widget.md), [호환성](docs/compatibility.md), [유지보수 규칙](AGENTS.md)을 참고하세요.
