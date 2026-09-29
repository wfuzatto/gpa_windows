# GPA Windows

Gerenciador de Políticas Administrativas locais para Windows.

O objetivo do **GPA Windows** é oferecer uma interface única para aplicar e fiscalizar diretivas locais em computadores Windows sem depender de domínio Active Directory.

## Funcionalidades atuais

### Personalização

- Bloquear alteração do papel de parede.
- Bloquear alteração do tema do Windows.

### Áudio

- Bloquear áudio do computador desabilitando endpoints PnP de áudio.
- Fiscalização periódica: novos endpoints que aparecerem enquanto a política estiver ativa também são desabilitados.
- Backup automático dos endpoints originalmente habilitados.
- Restauração dos dispositivos quando a política é removida.

> O bloqueio de endpoints pode afetar tanto reprodução quanto captura de áudio, incluindo microfones.

### Controle de aplicativos

- Inventário dos programas instalados usando as áreas de desinstalação do Registro e **App Paths**.
- Grade com checkbox **Permitir** para cada aplicativo.
- Aplicativo desmarcado é encerrado pelo serviço quando sua execução é detectada.
- Fiscalização rápida, aproximadamente a cada 750 ms.
- Componentes executados a partir da pasta do Windows são protegidos automaticamente.
- O próprio GPA Windows nunca é bloqueado.
- Botão **Adicionar EXE...** para programas portáteis.
- Botões **Permitir todos** e **Bloquear todos**.
- Modo estrito opcional para bloquear executáveis interativos desconhecidos que não estejam na allowlist.
- O modo estrito fica desligado por padrão para reduzir o risco de bloquear software necessário durante a configuração inicial.

Entradas de software instalado para as quais o Windows não informa um executável são exibidas no inventário, mas só podem ser bloqueadas depois que o EXE correspondente for localizado/adicionado.

### Rede / DNS

- Modo **DNS Allowlist**: somente domínios autorizados são resolvidos; os demais recebem resposta NXDOMAIN.
- Backup e restauração do arquivo `hosts`.
- Backup e restauração dos servidores DNS dos adaptadores.
- Desativação de DNS-over-HTTPS em Microsoft Edge e Google Chrome quando o modo DNS Allowlist estiver ativo.
- Resolvedor DNS local em `127.0.0.1:53`.

### Persistência

- Serviço do Windows usando o mesmo executável com `--service`.
- Reaplicação periódica das políticas.
- Recuperação automática do serviço em caso de falha.
- Instalação/remoção do serviço pela própria interface.
- Executável elevado por UAC.
- Publicação self-contained e single-file para Windows x64.

## Arquitetura

```
GpaWindows.exe
  ├─ Interface WinForms
  ├─ PolicyEngine
  ├─ RegistryPolicyService
  ├─ AudioPolicyService
  ├─ AppInventoryService
  ├─ AppControlService
  ├─ NetworkPolicyService
  ├─ DnsFilterService (127.0.0.1:53)
  └─ Windows Service mode (--service)
```

### Controle de aplicativos

No modo normal, somente executáveis explicitamente desmarcados na lista são bloqueados.

No **modo estrito**, processos interativos fora da pasta do Windows precisam constar como permitidos na allowlist. Serviços executados na sessão 0 não entram nessa fiscalização rápida. Esse modo deve ser testado antes de ser aplicado em larga escala.

### Por que não usar apenas o arquivo hosts?

O arquivo `hosts` consegue criar ou sobrescrever resoluções específicas, mas não possui uma regra do tipo "bloquear qualquer domínio que não esteja nesta lista". Por isso o modo estrito usa um resolvedor DNS local: os domínios permitidos são encaminhados ao DNS upstream e qualquer outro nome recebe NXDOMAIN.

O `hosts` ainda é protegido nesse modo para evitar que entradas antigas permitam contornar a allowlist.

## Requisitos

- Windows 10/11 x64.
- Privilégios de administrador.
- PowerShell com os cmdlets PnP nativos do Windows.
- .NET 8 SDK somente para desenvolvimento. A publicação final é self-contained.

## Build

```powershell
dotnet restore .\gpa_windows.sln
dotnet build .\gpa_windows.sln -c Release
dotnet test .\gpa_windows.sln -c Release
```

## Diagnóstico e logs

O executável registra falhas de inicialização em `%ProgramData%\GPAWindows\logs\startup.log`.
Para executar verificações não destrutivas sem abrir a interface:

```powershell
.\GpaWindows.exe --diagnostic
Get-Content "$env:ProgramData\GPAWindows\diagnostic-report.txt"
```

O relatório inclui versão, Windows, arquitetura, privilégios, serviço, UDP/53,
inventário de aplicativos, endpoints de áudio e permissões de rollback. O modo
diagnóstico não aplica políticas.

## Publicar EXE único

```powershell
dotnet publish .\src\GpaWindows\GpaWindows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Saída esperada:

```
src\GpaWindows\bin\Release\net8.0-windows\win-x64\publish\GpaWindows.exe
```

## Estado e rollback

O programa altera Registro do Windows, dispositivos de áudio, DNS dos adaptadores e o arquivo `hosts`. O estado necessário para rollback é salvo em:

```
%ProgramData%\GPAWindows\policy.json
```

A opção **Restaurar políticas** tenta retornar os valores gerenciados, os endpoints de áudio e as configurações DNS/hosts ao estado anterior.

## Próximos módulos previstos

- Bloqueio de Painel de Controle/Configurações.
- Bloqueio de USB.
- Controle de Windows Update.
- Bloqueio de CMD/PowerShell/Regedit/Gerenciador de Tarefas.
- Kiosk mode.
- Perfis exportáveis/importáveis.
- Senha administrativa do GPA Windows.
- Auditoria e log de alterações.
- Agente central opcional para administrar várias estações.
