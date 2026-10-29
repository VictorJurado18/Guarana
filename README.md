# Guaraná

Guaraná é um utilitário para Windows que mantém a sessão ativa enviando um evento da tecla F15 em intervalos regulares. Ele pode funcionar continuamente, seguir horários semanais ou ficar ativo por uma duração escolhida na hora.

![Ícone do Guaraná](Assets/guarana.png)

## Usar

Abra `Guarana.exe`. Na primeira execução, a janela de configurações aparece. Depois, o aplicativo fica na bandeja do Windows. Dê dois cliques no ícone para abrir as configurações; o menu do botão direito permite pausar, ativar por 30 minutos ou sair.

- **Modo contínuo:** sem horários programados, o Guaraná fica ativo até você pausar. A opção “Ativar por” cria uma sessão com fim definido; ao terminar, o modo manual fica pausado.
- **Horários programados:** marque os dias e configure início e fim no formato `HH:mm`. É possível adicionar mais de um período por dia. Um horário de fim igual ou anterior ao início significa que o período termina no dia seguinte. Fora dos períodos, nenhum F15 é enviado.
- **Ativação temporária:** prevalece sobre o horário programado. Quando termina, o aplicativo volta a seguir a programação.
- **Pausar/Retomar:** no modo programado, Pausar suspende a programação até você escolher Retomar.
- **Iniciar com o Windows:** opção desmarcada por padrão. Ao habilitar, o aplicativo copia o executável para `%LOCALAPPDATA%\Guarana\Guarana.exe` e cria uma entrada `Guarana` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Ao desabilitar, remove apenas essa entrada. Não requer administrador.

As preferências ficam em `%LOCALAPPDATA%\Guarana\settings.json`. Não há conexão de rede, telemetria ou atualização automática.

## Compilar

Requer SDK do .NET 10 no computador de desenvolvimento. O usuário final **não** precisa instalar .NET. Para gerar um executável único para Windows x64:

```powershell
dotnet publish Guarana.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

Para executar os testes da lógica de horários e estados:

```powershell
dotnet run --project tests/Guarana.Tests.csproj -c Release
```

O arquivo distribuído é `dist/Guarana.exe`; não é necessário instalador. Ele deve permanecer no mesmo caminho se você não usar a opção de inicialização automática. Essa opção guarda uma cópia no perfil do usuário para manter o caminho estável.

## Limitações

O método F15 reproduz a abordagem do Caffeine 1.5. Alguns aplicativos podem reagir à tecla F15, e políticas de bloqueio definidas pela organização podem prevalecer. O aplicativo não altera as configurações de energia do Windows nem contorna bloqueios manuais. Se o executável não tiver assinatura digital, o Windows pode mostrar um aviso do SmartScreen.

Guaraná é uma implementação independente; não contém código nem recursos extraídos do Caffeine.
