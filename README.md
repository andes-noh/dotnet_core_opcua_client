# dotnet-opcua-client

.NET Template standalone Project

## Collector

-   opcua 연결
    -   Configuration(User 인증) -> Create Session -> Read Value
    ***
-   데이터 수집

    -   single

    ```
        // progName
        NodeId nodeId = new NodeId("ns=2;s=/Channel/ProgramInfo/progName");
        DataValue dv = opcUaClient.ReadValue(nodeId);

        // 수집 데이터 출력
        Console.WriteLine($"single data: " + dv.ToString());
    ```

    -   multi

    ```
        // nodeId 지정
        List<NodeId> nodeIds = new List<NodeId>();
        nodeIds.Add(new NodeId("ns=2;s=/Channel/State/acProg"));
        nodeIds.Add(new NodeId("ns=2;s=/Channel/ProgramInfo/progName"));
        nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/driveLoad"));
        nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/actSpeed"));
        nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/speedOvr"));
        nodeIds.Add(new NodeId("ns=2;s=/Channel/MachineAxis/feedRateOvr")

        // 파라미터 전달
        List<DataValue> dataValues = opcUaClient.ReadValues(nodeIds.ToArray()

        // 수집 데이터 출력
        for (int i = 0; i < dataValues.Count; i++)
        {
            Console.WriteLine($"data{i + 1}: " + dataValues[i].ToString());
        }
    ```

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
-   [OpcUaHelper](https://github.com/dathlin/OpcUaHelper)
-   [OpcUaClient](https://m.blog.naver.com/yeo2697/222083701071)
