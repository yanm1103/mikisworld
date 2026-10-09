# Miki's World — PC port

Port para PC dos jogos J2ME da Sumea (`MikisWorld.jar`, `MikisWorld2.jar`).
Alvo: **C# / .NET 8** + Raylib-cs, com a lógica e o desenho traduzidos de forma literal a partir do bytecode
original e validados contra ele (ver [Validação](#validação)).

Port não oficial por TwistZero (github.com/yanm1103). Os jogos originais são © 2002 Sumea; este repositório
contém apenas código novo e **não distribui os jogos**: o programa lê os recursos direto dos JARs que você fornece.

## Progresso

> Esta tabela é atualizada **a cada commit**. Percentuais são estimativas de esforço, não de linhas de código.
> Pesos: decompilação 10 · lógica 30 · renderização 20 · janela/entrada 10 · menus/saves 15 · áudio 15.
> Última atualização: commit "Documentação" (2026-10-09).

### MikisWorld.jar (Miki's World 1: The Lost Tiara)

| Parte | Status | Notas |
|---|---|---|
| Decompilação e análise | **100%** | [`docs/MikisWorld-analysis.md`](docs/MikisWorld-analysis.md) |
| Lógica do jogo (física, plataformas, itens, Lords, estágio final, cutscenes) | **100%** | 17/17 traces idênticos ao original (86.624 ticks) |
| Minigame bônus (5×4, reação em cadeia + IA) | **100%** | idêntico ao original, incluindo a IA e o RNG |
| Renderização (cena, parallax, HUD, wipes, cutscenes, minigame) | **100%** | 930 quadros pixel-idênticos ao original (texto excluído) |
| Janela, escala, teclado | **97%** | escala inteira (com *snap*) ou proporcional com filtro nearest/bilinear, tela cheia sem bordas, volume 0–100 %; falta gamepad e remapear teclas |
| Tela-título, menus, hi-scores, opções, salvamento | **90%** | hi-scores idênticos ao original (5 sementes × 400 ops); fluxo coberto por testes automáticos; falta a opção "Vibrator" (sem equivalente no PC) |
| Áudio (`.ott`) | **100%** | decoder validado contra referência independente (12/12); **conferido de ouvido** com o ajuste de −1 oitava |
| **Total estimado** | **~98%** | |

### MikisWorld2.jar (Miki's World 2: The Storm Master)

Mesmo motor do jogo 1 (~85% do bytecode da classe principal é idêntico), com resolução 128×128, tiles de 12 px,
um perseguidor novo (Mork), arco-íris no lugar dos pássaros e outro formato de camada de tiles. Detalhes em
[`docs/MikisWorld2-analysis.md`](docs/MikisWorld2-analysis.md).

| Parte | Status | Notas |
|---|---|---|
| Decompilação e análise | **~95%** | decompilado, lido e confirmado rodando o jar original no harness; `level.bin` validado (`parse_level.py --v2`) |
| Lógica do jogo (Mork, arco-íris, física alterada) | **100%** | `Game2`: 20/20 traces idênticos ao original (~100 mil ticks), incluindo Mork, Lords, subáreas e estágio final |
| Renderização (128×128, tiles individuais, fundo novo) | **100%** | ~600 quadros pixel-idênticos ao original (texto excluído), incluindo o cache em anel dos tiles e o minigame |
| Menus, hi-scores, salvamento | **90%** | o mesmo `App` serve aos dois jogos (código secreto próprio, save separado); splash de 4 s implementado mas desligado por padrão |
| Áudio | **100%** | 12/12 sons validados contra a referência; só `title.ott` e `gameend.ott` diferem do jogo 1; o título toca 3×; **conferido de ouvido** |
| Janela, escala, teclado | **97%** | mesma janela e opções do jogo 1; escolha do jogo (`--game 1\|2` ou tela de seleção) |
| **Total estimado** | **~97%** | mesmos pesos do jogo 1 |

### Histórico

| Commit | O que mudou | Total MW1 · MW2 |
|---|---|---|
| inicial | Decompilação, harness do original, núcleo em C# validado por traces | ~45% · — |
| Renderizador e janela | Renderizador por software validado por quadros; janela Raylib lendo o JAR | ~69% · — |
| Menus e salvamento | Título, menu, instruções, hi-scores, entrada de nome, lista de modo, save em JSON | ~82% · — |
| Áudio | Decoder de Nokia Ring Tone + sintetizador + reprodução no Raylib; `play.bat` | ~95% · — |
| Decomp do jogo 2 | MikisWorld2.jar decompilado, lido e comparado por bytecode; parser de nível v2; harness roda o jar 2 | ~95% · ~9% |
| Game2 (lógica) | Tradução literal da classe do jogo 2 (Mork, arco-íris, colisão com wrap); 20/20 traces | ~95% · ~39% |
| Game2 (desenho) | Renderizador 128×128 validado por quadros (inclui quirks: snapshot 12×12 do wipe, cache em anel) | ~95% · ~59% |
| Dois jogos no mesmo programa | `App` genérico (`IMikisGame`), escolha do jogo, áudio e save por JAR; correção dos scripts de teste | ~95% · ~93% |
| Correção dos balões | O balão da introdução ficava na cutscene final (flags não eram recriadas a cada `Load`); teste de regressão | ~95% · ~93% |
| Janela com escala inteira | *Snap* ao múltiplo inteiro ao redimensionar, `F11`, `+`/`-` | ~96% · ~94% |
| Sons uma oitava abaixo | Padrão −1 oitava (`--octave N`); áudio conferido de ouvido nos dois jogos | ~98% · ~97% |
| Documentação | README e análises atualizados; splash do jogo 2 opcional; créditos | ~98% · ~97% |
| Opções de janela e volume | Tela *Options* (volume 0–100 % na amplitude da onda, tela cheia, escala inteira, filtro nearest/bilinear); os dois JARs passam a ser obrigatórios | ~98% · ~97% |

## Como rodar

Precisa dos **dois** arquivos, `MikisWorld.jar` e `MikisWorld2.jar` (em `JAR/`), e do .NET 8 SDK. Se faltar algum, o programa não inicia:
ele diz qual falta e onde procurar. Uma tela inicial deixa escolher o jogo (`1`/`2`); `--game 1` ou `--game 2` pula essa tela.

**Windows, no repositório:** dê dois cliques em `play.bat` (ou rode `play.bat`/`play.bat --scale 5` num terminal).
Ele usa o SDK portátil de `tools/dotnet` e recompila sozinho se o código mudou.

Com um .NET 8 SDK instalado no sistema, o equivalente é:

```
dotnet run --project port/MikisWorld.Game -- [--game 1|2] [--scale N] [--octave N] [caminho/para/o.jar]
```

**Controles.** Setas/WASD (andar, pular, interagir) e `Enter`/`Espaço` (pular cutscene), ou o teclado numérico como no
celular (`4`/`6` andar, `2` pular, `1`/`3` pulo diagonal, `8` interagir, `5` confirmar). `Esc` abre/fecha o menu (na partida
ele pausa e mostra "Continue game"); `Enter` confirma, e no campo de nome digite normalmente.

**Código secreto do minigame bônus** (digitado durante a partida; também é liberado coletando todas as moedas):

| Jogo | Código |
|---|---|
| 1 | `7 7 7 7 3 3 2 2 2 7 7 7 3 3 8` |
| 2 | `8 3 3 7 7 7 2 2 2 3 3 7 7 7 7` |

**Opções** (menu → *Options*, vale para os dois jogos e é salvo em `%APPDATA%/MikisWorld/settings.json`; a tela cheia não é salva):

| Opção | O que faz |
|---|---|
| Sound | volume de 0 a 100 % (←/→, passos de 5). Escala a **amplitude da onda quadrada** gerada; 0 % é mudo. Ao parar de mexer toca um blip para você ouvir o nível |
| Fullscreen | tela cheia sem bordas (também `F11`) |
| Integer scale | ligado: a imagem é ampliada por um fator **inteiro** (pixels nítidos, barras pretas) e a janela "gruda" no múltiplo inteiro ao redimensionar (`+`/`-` mudam a escala). Desligado: a imagem preenche a janela mantendo a proporção |
| Filter | só aparece com *Integer scale* desligado: `nearest` (pixels duros) ou `bilinear` (suavizado) |

**Salvamento.** Bônus liberado e hi-scores ficam em `%APPDATA%/MikisWorld/save.json` (`save2.json` no jogo 2); as opções acima, em `settings.json`.

**Som.** Só um som toca por vez, como no aparelho. `--octave N` ajusta a oitava (padrão `-1`; `0` = como decodificado). O volume é o da tela *Options*.
`--export-sounds pasta` grava um WAV por som; `--sound-test` testa o dispositivo de áudio.

## Estrutura

```
docs/                      análise dos dois jogos e dos formatos de arquivo
port/MikisWorld.Core/      lógica + renderização por software (sem dependência de janela/áudio)
                             Game*.cs (jogo 1) · Game2*.cs (jogo 2) · App.cs (menus) · RingTone.cs (áudio) · Canvas.cs
port/MikisWorld.Game/      frontend Raylib (janela, entrada, texto, áudio); lê os recursos direto dos JARs
port/MikisWorld.TraceTest/ replay de traces/quadros e testes de fluxo, sons, hi-scores e cutscenes
tools/harness/             roda os JARs originais sem tela (stubs do MIDP, Graphics real) e grava traces/quadros
tools/record_*.sh          gravam as referências a partir dos JARs originais (traces, quadros, hi-scores, sons)
tools/run_*.sh             comparam o C# com as referências;  tools/run_tests.sh  roda tudo
tools/gen_inputs.py        gera scripts de input determinísticos
tools/parse_level.py       parser independente do level.bin (--v2 para o jogo 2)
tools/decode_ott.py        decodificador independente dos sons (referência)
tools/classmatch.py        casa classes ofuscadas dos dois JARs por bytecode (também fieldmap.py)
```

Não versionados (ver `.gitignore`): `extracted/`, `decompiled/`, `traces/`, `sons/`, `tools/dotnet/`, `tools/cfr.jar`.
`JAR/` **não** está no `.gitignore`: os JARs são material da Sumea, então decida você se quer versioná-los.

## Reproduzindo o ambiente de validação

1. `jar xf JAR/MikisWorld.jar` em `extracted/MikisWorld/` e `jar xf JAR/MikisWorld2.jar` em `extracted/MikisWorld2/`.
2. SDK .NET 8 portátil: `dotnet-install.ps1 -Channel 8.0 -InstallDir tools/dotnet`.
3. (Opcional) Decompilador: CFR 0.152 → `tools/cfr.jar`, de `repo1.maven.org/maven2/org/benf/cfr/0.152/`.
4. Referências, a partir dos JARs originais (JDK 11+ e Python):
   `tools/record_traces.sh`, `tools/record_traces2.sh`, `tools/record_frames.sh`, `tools/record_frames2.sh`,
   `tools/record_hall.sh`, `tools/record_ott.sh`.
5. `tools/run_tests.sh` roda tudo e deve terminar com `ALL VALIDATIONS PASSED`: 17/17 traces e 10/10 conjuntos de quadros do
   jogo 1, 20/20 traces e 9/9 conjuntos de quadros do jogo 2, hi-scores, decoder de sons (24/24), balões de cutscene e fluxo do app.

## Validação

Os originais são executados sem tela (`tools/harness`) com passo fixo de 20 ms, `java.util.Random` com semente fixa e uma
sequência de teclas + teleportes. O estado completo é gravado a cada tick e o C# precisa reproduzir **exatamente** as mesmas
linhas. Para o desenho, o harness implementa `Graphics`/`Image` de verdade sobre `BufferedImage` e grava quadros (176×144 e
128×128); o renderizador C# precisa gerar os mesmos pixels. Cada teste foi conferido com **controles negativos** (trace de
outra partida, semente errada) para garantir que ele reprova diferenças.

**Bugs do original reproduzidos de propósito** (o C# falha no mesmo tick): cair abaixo do mapa, e usar um portal sem destino
nas subáreas (`ArrayIndexOutOfBounds`).
**Bugs do original que o port não reproduz:** no jogo 2, iniciar o minigame antes de carregar o nível principal trava a pintura
(`NullPointerException` / laço infinito, porque as imagens e o tamanho dos tiles ainda não existem).

Limites conhecidos: os inputs são aleatórios com teleportes (não partidas jogadas) e o texto (fonte do aparelho) fica fora da
comparação de pixels; partidas longas jogadas à mão podem revelar o que os testes aleatórios não cobrem (foi assim que se achou
o balão que vazava para a cutscene final).

## Áudio

Os 12 efeitos/músicas de cada jogo são Nokia Ring Tones (`.ott`). O decoder (`RingTone.cs`) foi escrito a partir do layout de
bits do formato Smart Messaging e validado contra um decoder independente em Python (`tools/decode_ott.py`). A síntese é uma
onda quadrada simples, como o buzzer do celular, e só um som toca por vez.

Dois pontos foram **inferidos** (a especificação oficial não estava acessível): um padrão com valor de loop *n* toca *n+1* vezes,
e a oitava de cada nota. Como quase todos os sons decodificados ficavam entre 0,7 e 2,6 kHz (agudos demais), o padrão do port é
**uma oitava abaixo** — escolha confirmada de ouvido nos dois jogos. `--octave N` permite ajustar.
