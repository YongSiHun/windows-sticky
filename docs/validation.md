# 실행 검증

2026-11-26 · 로컬 호스트 API `10.0.26300.0` · x64 프로세스.

| 항목 | 결과 |
|---|---|
| VS MSBuild `/restore` x86/x64 | 실패: 사용자 NuGet 설정 폴더 접근 거부 |
| Framework MSBuild x86/x64 Release | 성공: 설치된 Framework 어셈블리 참조 |
| x86 회귀 테스트 | 18 통과 / 0 실패 |
| x64 회귀 테스트 | 18 통과 / 0 실패 |
| x64 Debug 빌드·격리 앱 시작 | 성공: 프로세스 응답 및 HWND 확인 |
| 사용자 화면 육안·입력 검증 | 미완료: computer-use 앱/창 목록에 격리 앱이 없음 |
| Sonar S3776 ≤ 5 실측 | 미완료: 분석기 복원 불가 |
| PowerShell 빌드·패키징 스크립트 구문 | 통과 |
| Setup EXE 생성 | 실패: 포함할 .NET 오프라인 설치본 다운로드 연결 차단 |
| GitHub Actions | 원격 미실행 |
| Win7/10/11 OS별 설치·화면·Shell 실기 | 미검증 |

정식 빌드 명령은 `scripts/build.ps1`입니다. `/restore`로 공식 4.8 참조 어셈블리와 빌드 전용 Sonar 분석기를 준비하고 양쪽 플랫폼의 회귀 테스트를 실행합니다. `SonarLint.xml`은 S3776 임계값 5, `.editorconfig`는 초과 시 오류를 지정합니다. 이 호스트에는 VS Roslyn 도구가 없고 NuGet·Microsoft 다운로드 서버 연결도 차단돼 있습니다. 분석기 통과나 정식 배포 빌드 성공을 주장하지 않습니다.

실제로 통과한 제한적 로컬 명령:

```powershell
foreach ($platform in @('x86', 'x64')) {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" StickyNote.sln /m /p:Configuration=Release /p:Platform=$platform /p:FrameworkPathOverride="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
    & ".\tests\StickyNote.Tests\bin\$platform\Release\StickyNote.Tests.exe"
}
```

이 경로는 설치된 런타임 어셈블리를 참조하며 Sonar 분석기를 실행하지 않습니다. CI의 공식 참조 어셈블리 빌드와 대상 OS 실행을 대체하지 않습니다.

회귀 범위: UTF-8/UTC JSON, 스키마 경계, 필드 검증, 원자적 저장·정상본, 잠금 실패, 손상 복구·덮어쓰기 차단, 동시 revision, 백업·복원 트랜잭션, journal 재생, 화면 좌표 보정, debounce, 메모 작업·검색, 언어 리소스, HWND 모드 전환, 단일 인스턴스 IPC, 손상 바이트 원본 보존, dispatcher 스냅샷 격리.

기존 `Expected 10, got 3` 오류는 동일한 시스템 시각으로 백업명이 충돌한 결과입니다. UTC+GUID 파일명으로 수정했고 고정 시각에서 연속 백업이 별개로 남으며 최종 10개가 보관되는지 검사합니다. 별도의 테스트 프레임워크나 런타임 패키지는 사용하지 않습니다.

격리 앱은 `--diagnostic-data-directory`가 있는 Debug 빌드로 실행했습니다. 확인 후 테스트 프로세스만 종료했습니다. 실제 메모 폴더·자동 실행 레지스트리·Explorer·OS 구성은 변경하지 않았습니다. 테스트 데이터와 진단 출력은 무시되는 `TestResults/`에 둡니다.

설치 파일은 런타임 포함·기존 Framework 건너뛰기·UAC 취소·재부팅·오프라인 설치·제거 후 데이터 보존을 별도 확인해야 합니다. 대상 OS와 DPI·IME·트레이·Desktop의 실제 표시 검증은 [호환성 체크리스트](compatibility.md), Shell 제약은 [Desktop 문서](desktop_widget.md)를 따릅니다.
