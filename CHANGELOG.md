# Changelog

Todas as mudanças relevantes deste projeto ficam registradas aqui.
O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa [versionamento semântico](https://semver.org/lang/pt-BR/).

## [1.1.0] - 2026-10-29

### Adicionado
- Interface redesenhada: cartões, interruptores, chips de duração e editor de horários por períodos com seleção de dias.
- Tema claro e escuro acompanhando o Windows, barra de título no mesmo tom e cantos arredondados no Windows 11.
- Novo logo e ícones: olho aberto (ativo) e olho fechado (pausado).
- Salvamento automático das preferências, sem botão “Salvar”.
- Opção para impedir suspensão e tela apagada via `SetThreadExecutionState`.
- Opção para enviar F15 só quando o computador estiver ocioso (ligada por padrão).
- Ativação rápida por 15 min, 30 min, 1 h, 2 h, 4 h ou 8 h, também no menu da bandeja.
- Contagem regressiva da ativação temporária e aviso quando ela termina.
- Campos de horário aceitam `9`, `930` ou `9:30` e completam para `HH:mm`.
- Abrir o `.exe` de novo traz a janela da instância que já está rodando.
- Janela “Sobre” com versão, licença e link do repositório.
- Licença MIT e changelog.
- GitHub Actions: cada PR gera os `.exe` para teste; o merge de uma versão nova publica a release automaticamente.

### Corrigido
- Compatibilidade com o analisador do Windows Forms no .NET 10 (WFO1000).
- A ativação temporária se perdia ao reiniciar o aplicativo.
- “Pausar” ficava desabilitado fora do horário programado.
- A mensagem de falha ao enviar F15 não sumia depois que o envio voltava a funcionar.
- Segurar a tecla Win não impedia o pulso, que podia disparar atalhos do sistema.
- Um modificador pressionado fazia o pulso esperar o intervalo inteiro; agora tenta de novo em 2 s.
- A cópia usada na inicialização automática não era atualizada ao abrir uma versão nova.
- O ícone da bandeja era redesenhado a cada segundo, mesmo sem mudança de estado.

## [1.0.0]

- Primeira versão: pulso F15, modo contínuo, horários programados, ativação temporária e inicialização com o Windows.

[1.1.0]: https://github.com/VictorJurado18/Guarana/releases/tag/v1.1.0
[1.0.0]: https://github.com/VictorJurado18/Guarana/releases/tag/v1.0.0
