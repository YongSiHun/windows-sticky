# Sticky Note

로컬 메모 앱 · 제목 직접 편집 · 색상·크기·투명도 슬라이더 · 자동 저장·백업

## Stack

- UI: C# 5 · WPF · .NET Framework 4.8
- Data: 로컬 JSON · 원자적 저장 · 정상본 백업
- QA/CI: STA 회귀 테스트 · Sonar S3776 ≤ 5 · 수동 GitHub Actions
- Pkg: x86/x64 Setup EXE · .NET 4.8 오프라인 설치본 포함
- UX: 한국어 설치 · 시스템 언어(ko/en) · 환경설정·메모 관리

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

GitHub: Actions → Build → Run workflow → sticky-note-setup-버전 다운로드.

Ver: `VERSION` 수정 → 빌드·패키징. 앱·설치 파일·CI 산출물에 같은 버전 적용.
