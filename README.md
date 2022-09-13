# dotnet-basic

.NET Template standalone Project

Collector

-   BackgroundService로 동작
-   .env TEXT 내용 출력

## Setup

> [.NET CLI 개요](https://docs.microsoft.com/ko-kr/dotnet/core/tools/)

```
$Env:DOTNET_CLI_UI_LANGUAGE = "en"

dotnet new sln
dotnet new console -o app
dotnet sln add app
dotnet new xunit -o test
dotnet sln add test
dotnet add test/test.csproj reference app/app.csproj
dotnet new nugetconfig
dotnet restore
```

## 패키지 추가/제거

> .csproj 파일을 선택하고 오른쪽 클릭 -> `Visual Nuget: Manage Packages` 선택

```
dotnet add package dotenv.net
dotnet remove package Newtonsoft.Json
```

## Run

```
dotnet run --project app
```

## Run Tests

```
dotnet test
```

## EXE

```
dotnet publish \
  --output "./dist" \
  --configuration Release \
  --self-contained true
```

---

## References

-   [[Docs / Visual Studio / MSBuild / MSBuild reference] Common MSBuild project items](https://docs.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items)
-   [Dependency injection in .NET](https://docs.microsoft.com/ko-kr/dotnet/core/extensions/dependency-injection)
-   [Create a Windows Service using BackgroundService](https://docs.microsoft.com/ko-kr/dotnet/core/extensions/windows-service)
-   [dotenv.net](https://github.com/bolorundurowb/dotenv.net)
-   [Akka.NET](https://getakka.net/)
