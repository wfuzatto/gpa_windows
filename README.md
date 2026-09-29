# GPA Windows

Gerenciador de Políticas Administrativas locais para Windows.

O objetivo do **GPA Windows** é oferecer uma interface única para aplicar e fiscalizar diretivas locais em computadores Windows sem depender de domínio Active Directory.

## Primeira versão

- Bloquear alteração do papel de parede.
- Bloquear alteração do tema do Windows.
- Modo **DNS Allowlist**: somente domínios autorizados são resolvidos; os demais recebem resposta NXDOMAIN.
- Backup e restauração do arquivo `hosts`.
- Backup e restauração dos servidores DNS dos adaptadores.
- Desativação de DNS-over-HTTPS em Microsoft Edge e Google Chrome quando o modo DNS Allowlist estiver ativo.
- Serviço do Windows usando o mesmo executável com `--service`, para reaplicar as diretivas periodicamente.
- Instalação/remoção do serviço pela própria interface.
- Executável elevado por UAC.
- Publicação self-contained e single-file para Windows x64.

## Arquitetura

```
GpaWindows.exe
  ├─ Interface WinForms
  ├─ PolicyEngine
  ├─ RegistryPolicyService
  ├─ NetworkPolicyService
  ├─ DnsFilterService (127.0.0.1:53)
  └─ Windows Service mode (--service)
```

### Por que não usar apenas o arquivo hosts?

O arquivo `hosts` consegue criar ou sobrescrever resoluções específicas, mas não possui uma regra do tipo "bloquear qualquer domínio que não esteja nesta lista". Por isso o modo estrito usa um resolvedor DNS local: os domínios permitidos são encaminhados ao DNS upstream e qualquer outro nome recebe NXDOMAIN.

O `hosts` ainda é protegido nesse modo para evitar que entradas antigas permitam contornar a allowlist.

## Requisitos

- Windows 10/11 x64.
- Privilégios de administrador.
- .NET 8 SDK somente para desenvolvimento. A publicação final é self-contained.

## Build

```powershell
dotnet restore .\gpa_windows.sln
dotnet build .\gpa_windows.sln -c Release
```

## Publicar EXE único

```powershell
dotnet publish .\src\GpaWindows\GpaWindows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Saída esperada:

```
src\GpaWindows\bin\Release\net8.0-windows\win-x64\publish\GpaWindows.exe
```

## Observações de segurança

O programa altera Registro do Windows, DNS dos adaptadores e o arquivo `hosts`. Antes da primeira aplicação das políticas de rede, o estado atual é salvo em `%ProgramData%\GPAWindows\policy.json`.

A opção **Restaurar políticas** tenta retornar os valores gerenciados e as configurações DNS/hosts ao estado anterior.

## Próximos módulos previstos

- Bloqueio de Painel de Controle/Configurações.
- Bloqueio de USB.
- Restrições de execução por aplicativo.
- Controle de Windows Update.
- Bloqueio de CMD/PowerShell/Regedit/Gerenciador de Tarefas.
- Kiosk mode.
- Perfis exportáveis/importáveis.
- Senha administrativa do GPA Windows.
- Auditoria e log de alterações.
- Agente central opcional para administrar várias estações.
