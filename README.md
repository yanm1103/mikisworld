# Miki's World — PC port

Port para PC dos jogos J2ME da Sumea (`MikisWorld.jar`, `MikisWorld2.jar`).
Alvo: **C# / .NET 8** + Raylib-cs, com a lógica e o desenho traduzidos de forma literal a partir do bytecode
original e validados contra ele (ver [Validação](#validação)).

## Progresso

> Esta tabela é atualizada **a cada commit**. Percentuais são estimativas de esforço, não de linhas de código.
> Pesos: decompilação 10 · lógica 30 · renderização 20 · janela/entrada 10 · menus/saves 15 · áudio 15.
> Última atualização: commit "Dois jogos no mesmo programa" (2026-10-09).

### MikisWorld.jar (Miki's World 1: The Lost Tiara)

| Parte | Status | Notas |
|---|---|---|
| Decompilação e análise | **100%** | [`docs/MikisWorld-analysis.md`](docs/MikisWorld-analysis.md) |
| Lógica do jogo (física, plataformas, itens, Lords, estágio final, cutscenes) | **100%** | 17/17 traces idênticos ao original (86.624 ticks) |
| Minigame bônus (5×4, reação em cadeia + IA) | **100%** | idêntico ao original, incluindo a IA e o RNG |
| Renderização (cena, parallax, HUD, wipes, cutscenes, minigame) | **100%** | 930 quadros pixel-idênticos ao original (texto excluído) |
| Janela, escala, teclado | **90%** | falta: gamepad, tela cheia, remapear teclas |
| Tela-título, menus, hi-scores, opções, salvamento | **90%** | hi-scores idênticos ao original (5 sementes × 400 ops); fluxo coberto por 26 checks automáticos; falta a opção "Vibrator" (sem equivalente no PC) |
| Áudio (`.ott`) | **85%** | decoder validado contra referência independente (12/12); sintetizador de onda quadrada tocado pelo Raylib; falta conferir de ouvido (o número de repetições dos loops é inferido) |
| **Total estimado** | **~95%** | |

### MikisWorld2.jar

Mesmo motor do jogo 1 (~85% do bytecode da classe principal é idêntico), com resolução 128×128, tiles de 12 px,
um perseguidor novo (Mork), arco-íris no lugar dos pássaros e outro formato de camada de tiles. Detalhes em
[`docs/MikisWorld2-analysis.md`](docs/MikisWorld2-analysis.md).

| Parte | Status | Notas |
|---|---|---|
| Decompilação e análise | **~85%** | decompilado e lido (tick, jogador, Mork, arco-íris, desenho, cutscenes, shell); `level.bin` validado (`parse_level.py --v2`); **o jar roda no harness** e 20 traces de referência foram gravados (`tools/record_traces2.sh`). Falta o desenho do minigame e a validação visual |
| Lógica do jogo (Mork, arco-íris, física alterada) | **100%** | `Game2`: 20/20 traces idênticos ao original (~100 mil ticks), incluindo Mork, Lords, subáreas e estágio final |
| Renderização (128×128, tiles individuais, fundo novo) | **100%** | ~600 quadros pixel-idênticos ao original (texto excluído), incluindo o cache em anel dos tiles e o minigame |
| Menus, hi-scores, salvamento | **90%** | o mesmo `App` serve aos dois jogos (splash de 4 s, 128×128, código secreto próprio); fluxo coberto por testes automáticos |
| Áudio | **85%** | 12/12 sons validados contra a referência; só `title.ott` e `gameend.ott` diferem do jogo 1; o título toca 3× |
| Janela, escala, teclado | **90%** | mesma janela do jogo 1; escolha do jogo (`--game 1|2` ou tela de seleção) |
| **Total estimado** | **~93%** | mesmos pesos do jogo 1 |

### Histórico

| Commit | O que mudou | Total MW1 |
|---|---|---|
| inicial | Decompilação, harness do original, núcleo em C# validado por traces | ~45% |
| Renderizador e janela | Renderizador por software validado por quadros; janela Raylib lendo o JAR | ~69% |
| Menus e salvamento | Título, menu, instruções, hi-scores, entrada de nome, lista de modo, save em JSON | ~82% |
| Áudio | Decoder de Nokia Ring Tone + sintetizador + reprodução no Raylib; `play.bat` | ~95% |
| Decomp do jogo 2 | MikisWorld2.jar decompilado, lido e comparado por bytecode; parser de nível v2; harness roda o jar 2 e grava 20 traces | MW1 ~95% · MW2 ~9% |
| Game2 (lógica) | Tradução literal da classe do jogo 2 (Mork, arco-íris, colisão com wrap); 20/20 traces idênticos | MW1 ~95% · MW2 ~39% |
| Game2 (desenho) | Renderizador 128×128 validado por quadros contra o original (inclui quirks: snapshot 12×12 do wipe, cache em anel) | MW1 ~95% · MW2 ~59% |
| Dois jogos no mesmo programa | `App` genérico (`IMikisGame`), splash, escolha do jogo, áudio e save por JAR; correção dos scripts de teste | MW1 ~95% · MW2 ~93% |

## Como rodar

Precisa de `MikisWorld.jar` e/ou `MikisWorld2.jar` (não versionados; coloque em `JAR/`) e do .NET 8 SDK.
Se os dois estiverem em `JAR/`, uma tela inicial deixa escolher (`1`/`2`); `--game 1` ou `--game 2` pula essa tela.

**Windows, no repositório:** dê dois cliques em `play.bat` (ou rode `play.bat`/`play.bat --scale 5` num terminal).
Ele usa o SDK portátil de `tools/dotnet` e recompila sozinho se o código mudou.

Com um .NET 8 SDK instalado no sistema, o equivalente é:

```
dotnet run --project port/MikisWorld.Game -- [--game 1|2] [--scale N] [caminho/para/MikisWorld.jar]
```

Controles: setas/WASD (andar, pular, interagir), `Enter`/`Espaço` (pular cutscene), ou o teclado numérico
como no celular (`4`/`6` andar, `2` pular, `1`/`3` pulo diagonal, `8` interagir, `5` confirmar).
`Esc` abre/fecha o menu (na partida ele pausa e mostra "Continue game"); `Enter` confirma, e no campo de nome
digite normalmente. Opções e hi-scores ficam em `%APPDATA%/MikisWorld/save.json` (`save2.json` no jogo 2).

**Janela:** a imagem é sempre ampliada por um fator **inteiro** (sem pixels irregulares); ao soltar a borda depois de redimensionar,
a janela "gruda" no múltiplo inteiro mais próximo. `+` / `-` mudam a escala e `F11` alterna tela cheia sem bordas (com barras
pretas, mantendo o fator inteiro).
Código secreto para liberar o minigame bônus: `7 7 7 7 3 3 2 2 2 7 7 7 3 3 8`.

## Estrutura

```
docs/                      análise do jogo e dos formatos de arquivo
port/MikisWorld.Core/      lógica + renderização por software (sem dependência de janela/áudio)
port/MikisWorld.Game/      frontend Raylib (janela, entrada, texto); lê os recursos direto do JAR
port/MikisWorld.TraceTest/ replay de traces e comparação de quadros com o original
tools/harness/             roda o JAR original sem tela (stubs do MIDP, Graphics real) e grava traces/quadros
tools/gen_inputs.py        gera scripts de input determinísticos
tools/parse_level.py       parser independente do level.bin
tools/record_traces.sh     grava traces do original        tools/run_traces.sh   compara com o C#
tools/record_frames.sh     grava quadros do original       tools/run_frames.sh   compara com o C#
```

Não versionados (ver `.gitignore`): `JAR/`, `extracted/`, `decompiled/`, `traces/`, `tools/dotnet/`, `tools/cfr.jar`.

## Reproduzindo o ambiente de validação

1. `jar xf JAR/MikisWorld.jar` numa pasta vazia `extracted/MikisWorld/`.
2. SDK .NET 8 portátil: `dotnet-install.ps1 -Channel 8.0 -InstallDir tools/dotnet`.
3. (Opcional) Decompilador: CFR 0.152 → `tools/cfr.jar`, de `repo1.maven.org/maven2/org/benf/cfr/0.152/`.
4. `tools/record_traces.sh` e `tools/record_frames.sh` — rodam o JAR original (JDK 11+ e Python).
5. `tools/record_hall.sh` (tabelas de hi-score da classe `k` original) e `tools/record_ott.sh` (referência dos sons).
6. `tools/run_tests.sh` roda tudo e deve terminar com `ALL VALIDATIONS PASSED`
   (`17/17 traces identical`, `10/10 frame sets identical`, hi-scores e fluxo do app).

## Validação

O original é executado sem tela (`tools/harness`) com passo fixo de 20 ms, `java.util.Random` com semente fixa e
uma sequência de teclas + teleportes. O estado completo é gravado a cada tick e o C# precisa reproduzir
**exatamente** as mesmas linhas. Para o desenho, o harness implementa `Graphics`/`Image` de verdade sobre
`BufferedImage` e grava quadros 176×144; o renderizador C# precisa gerar os mesmos pixels. Bugs do original
(por exemplo, cair abaixo do mapa) são reproduzidos de propósito: o C# falha no mesmo tick.

Limites conhecidos: os inputs são aleatórios com teleportes (não partidas jogadas) e o texto (fonte do aparelho)
fica fora da comparação de pixels.

## Áudio

Os 12 efeitos/músicas são Nokia Ring Tones (`.ott`). O decoder (`RingTone.cs`) foi escrito a partir do layout de bits
do formato Smart Messaging e validado contra um decoder independente em Python (`tools/decode_ott.py`). A síntese é uma
onda quadrada simples, como o buzzer do celular. Como no original, só um som toca por vez.
Para ouvir fora do jogo: `play.bat --export-sounds sons` grava um WAV por som. `--sound-test` testa o dispositivo de áudio.
Inferido (não confirmado pela especificação): um padrão com valor de loop *n* toca *n+1* vezes.
