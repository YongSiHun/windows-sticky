# Windows Stricky

로컬 메모 앱 · 제목 직접 편집 · 마우스 색상표·글씨체 선택 · 자동 저장·백업

## Stack

- UI: C# 5 · WPF · .NET Framework 4.8
- Data: 로컬 JSON · 원자적 저장 · 정상본 백업
- QA/CI: STA 회귀 테스트 · Sonar S3776 ≤ 5 · 수동 GitHub Actions
- Pkg: x86/x64 Setup EXE · .NET 4.8 오프라인 설치본 포함
- UX: 한국어 설치 · 시스템 언어(ko/en) · 일반 모드 기본 · Windows 로그인 시 자동 실행

실행하면 저장된 메모를 모두 열고, 메모가 없을 때만 새 메모를 만듭니다. `+`로 메모를 추가하고, `×`는 확인 후 삭제합니다. 메모 관리 화면은 없습니다. 하단에 투명도·글자 크기 이름과 값이 표시됩니다. 메모 메뉴에서 배경색·글자색·글씨체를 바꾸고 환경설정에서 새 메모의 기본 모양을 지정합니다.

설치 프로그램은 시작 메뉴와 바탕화면에 `Windows Stricky` 바로가기를 만들고 현재 사용자의 자동 실행을 기본 등록합니다. 환경설정에서 자동 실행을 끌 수 있습니다. 기존 메모·설정은 이전과 같은 저장 경로를 사용합니다.

## OS

- Win7 SP1 · Win10: x86/x64
- Win11: x64 OS · x86/x64 앱
- Setup: 별도 런타임 다운로드 불필요 · 미설치 시 포함본 설치(UAC·재부팅 가능)
- Win7: SP1·.NET 설치용 OS 업데이트 필요
- Desktop: Shell별 제한 · 실패 시 Normal 표시 · 요청 모드 유지
- 실기 검증 상태: [호환성](docs/compatibility.md) · [검증](docs/validation.md)

## Cmd

Dev: VS 2022 MSBuild · .NET 4.8 Dev Pack. Setup 빌드 시 인터넷 필요.

```powershell
.\scripts\build.ps1   # x86/x64 restore·빌드·테스트·CC 검사
.\scripts\package.ps1 # artifacts/ Setup EXE·SHA256
```

GitHub: Actions → Build → Run workflow → windows-stricky-setup-버전 다운로드.

Ver: 루트 `VERSION`이 버전 관리 파일입니다(`major.minor.patch`). Visual Studio/MSBuild 직접 빌드와 `scripts/build.ps1`은 이 값을 그대로 사용하며 파일을 변경하지 않습니다. x86/x64 앱·설치 파일·CI 산출물 모두 같은 파일의 버전을 사용합니다.

버전을 변경하려면 `VERSION`을 직접 수정해 커밋한 뒤 빌드하세요. 빌드 성공·실패·재빌드 여부와 관계없이 버전이 자동으로 증가하지 않습니다.
