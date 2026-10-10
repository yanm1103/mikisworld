# Miki's World Collection — port para PC

Port para PC dos dois jogos de celular da Sumea (2002): **Miki's World 1: The Lost Tiara** e **Miki's World 2: The Storm Master**.
A lógica de cada jogo é a do original, traduzida literalmente e validada contra o código do celular, com menus, opções, controles de PC e gamepad.

Port não oficial por TwistZero (github.com/yanm1103). Os jogos originais são © 2002 Sumea; este repositório
contém apenas código novo e **não distribui os jogos**: o programa lê os recursos direto dos arquivos `.jar` que você fornece.

## Como jogar

1. Pegue o `MikisWorldCollection.exe` (para gerá-lo, veja "Informações técnicas" no fim desta página).
2. Coloque `MikisWorld.jar` e `MikisWorld2.jar` numa pasta chamada **`JAR`** ao lado do exe. Os **dois** são necessários; se faltar um, o programa diz qual.
3. Abra o exe. A **tela inicial** deixa escolher o jogo (`1`/`2`, setas + `Enter`, D-pad + `A` ou clique) e já configura o que vale para os dois:
   *Movement*, *Vibration*, *Fullscreen*, *Integer scale*, *Filter* e *Sound*. Tudo é salvo na hora.

Parâmetros úteis: `--game 1` ou `--game 2` (pula a tela inicial), `--scale N` (tamanho inicial da janela) e `--logs` (mostra o log do programa, para relatar problemas).

## Controles

| | Teclado | Gamepad |
|---|---|---|
| Andar | setas ← → ou `A` `D` | D-pad ou analógico esquerdo |
| Pular | ↑ ou `W` | `A` |
| Pulo diagonal | `Q` / `E` (ou ↑ junto com uma direção) | `A` junto com uma direção |
| Entrar em portais / interagir | ↓ ou `S` | `X` |
| Confirmar / pular cutscene | `Enter` ou `Espaço` | `Y` (nos menus e cutscenes, `A` também) |
| Menu / pausa | `Esc` | `Start` |

- **Remapear tudo:** menu → *Options* → *Keyboard keys* ou *Gamepad* (duas entradas por ação; `Backspace`/`X` limpa; *Defaults* restaura).
- **Teclado numérico do celular:** os dígitos continuam funcionando como no aparelho (`4`/`6` andar, `2` pular, `1`/`3` diagonal, `8` interagir, `5` confirmar) e **não** são remapeáveis, porque o código secreto é digitado com eles.
- **Mouse:** os menus são clicáveis (passar o mouse destaca, clique esquerdo ativa, direito volta, a roda rola as instruções).
- **Tela-título:** qualquer tecla (ou clique) abre o menu, e ele abre sozinho depois de 5 s, como no original.
- **Nome nos recordes com controle:** ↑/↓ trocam a letra, → acrescenta uma, ←/`B` apaga e `A`/`Start` salva.
- **Instruções na tela** mostram as teclas ou botões que você configurou (conforme o último dispositivo usado).

## Opções

Menu → *Options* (também na tela inicial). Valem para os dois jogos.

| Opção | O que faz |
|---|---|
| Sound | volume de 0 a 100 % (←/→, passos de 5); 0 % é mudo |
| Fullscreen | tela cheia sem bordas (também `F11`) |
| Integer scale | ligado: a imagem é ampliada por um fator **inteiro** (pixels nítidos, barras pretas) e a janela se ajusta a múltiplos inteiros ao redimensionar (`+`/`-` mudam o zoom). Desligado: a imagem preenche a janela |
| Filter | só com *Integer scale* desligado: `nearest` (pixels duros) ou `bilinear` (suavizado) |
| Movement | `new` (padrão) ou `original` — veja abaixo |
| Vibration | o **controle** vibra quando o Miki é atingido (a opção "Vibrator" do original). Só no Windows, com controles compatíveis com Xbox (ou outros via Steam Input/DS4Windows); no teclado não faz nada |

## O que é diferente do original

- **Movement `new`:** segurar ↑ não faz o Miki pular sem parar (cada pulo precisa de um novo aperto); a direção que você segura continua valendo depois de soltar outra tecla, então dá para mudar de lado caindo, ao pousar e **no meio de um pulo diagonal**. `original` é o jogo como era no celular, que zerava todo o movimento ao soltar qualquer tecla.
- **Ritmo:** o celular rodava a lógica bem mais devagar que um PC. O pulo dura o dobro do tempo (mesma altura e distância), a animação de andar do Miki é duas vezes mais lenta e, no jogo 2, o **Mork anda a 2/3 da velocidade**.
- **Menus e controles de PC:** gamepad, mouse, teclas remapeáveis, volume, tela cheia e zoom inteiro.
- **Corrigido:** apertar "entrar no portal" várias vezes durante a transição causava uma transição dupla e podia travar o jogo (um bug do original; no `Movement: original` continua assim, de propósito).

## Código secreto do minigame bônus

Digite durante a partida (o bônus também é liberado coletando todas as moedas):

| Jogo | Código |
|---|---|
| 1 | `7 7 7 7 3 3 2 2 2 7 7 7 3 3 8` |
| 2 | `8 3 3 7 7 7 2 2 2 3 3 7 7 7 7` |

## Seus arquivos

Tudo fica em `%APPDATA%/MikisWorld/`: `save.json` (jogo 1) e `save2.json` (jogo 2) com o bônus liberado e os recordes, `settings.json` com as opções, `bindings.json` com os controles remapeados.

## Se o jogo travar

A janela fica aberta com o erro: `Enter` volta à tela inicial, `C` copia o texto e `Esc` fecha. O detalhe também é gravado em `%APPDATA%/MikisWorld/crash.log`. Se for relatar o problema, rode com `--logs`.

<details>
<summary><b>Informações técnicas (para desenvolvedores)</b>: progresso, histórico, como compilar, estrutura, validação</summary>

### Progresso

> Esta tabela é atualizada **a cada commit**. Percentuais são estimativas de esforço, não de linhas de código.
> Pesos: decompilação 10 · lógica 30 · renderização 20 · janela/entrada 10 · menus/saves 15 · áudio 15.
> Última atualização: commit "Executável MikisWorldCollection" (2026-10-09). Marco de referência: tag `base-1.0`.

#### MikisWorld.jar (Miki's World 1: The Lost Tiara)

| Parte | Status | Notas |
|---|---|---|
| Decompilação e análise | **100%** | [`docs/MikisWorld-analysis.md`](docs/MikisWorld-analysis.md) |
| Lógica do jogo (física, plataformas, itens, Lords, estágio final, cutscenes) | **100%** | 17/17 traces idênticos ao original (86.624 ticks) |
| Minigame bônus (5×4, reação em cadeia + IA) | **100%** | idêntico ao original, incluindo a IA e o RNG |
| Renderização (cena, parallax, HUD, wipes, cutscenes, minigame) | **100%** | 930 quadros pixel-idênticos ao original (texto excluído) |
| Janela, escala, entrada | **100%** | escala inteira (com *snap*) ou proporcional com filtro nearest/bilinear, tela cheia sem bordas, volume 0–100 %; teclado e **gamepad remapeáveis** (tela *Options*), movimento original/new, tela inicial com opções |
| Tela-título, menus, hi-scores, opções, salvamento | **100%** | hi-scores idênticos ao original (5 sementes × 400 ops, nas classes `k` dos dois JARs); fluxo coberto por testes automáticos; a opção "Vibrator" vibra o **controle** quando o Miki é atingido (no teclado não há o que vibrar) |
| Áudio (`.ott`) | **100%** | decoder validado contra referência independente (12/12); **conferido de ouvido** com o ajuste de −1 oitava |
| **Total estimado** | **100%** | tudo o que o original faz está portado e validado (ver "Limites conhecidos" em *Validação*) |

#### MikisWorld2.jar (Miki's World 2: The Storm Master)

Mesmo motor do jogo 1 (~85% do bytecode da classe principal é idêntico), com resolução 128×128, tiles de 12 px,
um perseguidor novo (Mork), arco-íris no lugar dos pássaros e outro formato de camada de tiles. Detalhes em
[`docs/MikisWorld2-analysis.md`](docs/MikisWorld2-analysis.md).

| Parte | Status | Notas |
|---|---|---|
| Decompilação e análise | **100%** | as 12 classes lidas e classificadas (tabela "Cobertura por classe" em [`docs/MikisWorld2-analysis.md`](docs/MikisWorld2-analysis.md)); `level.bin` validado (`parse_level.py --v2`); hi-score testado contra a classe `k` do próprio JAR 2. Menus/formulários (API de interface do MIDP) são verificados por leitura + `apptest`, não por execução do original |
| Lógica do jogo (Mork, arco-íris, física alterada) | **100%** | `Game2`: 20/20 traces idênticos ao original (~100 mil ticks), incluindo Mork, Lords, subáreas e estágio final |
| Renderização (128×128, tiles individuais, fundo novo) | **100%** | ~600 quadros pixel-idênticos ao original (texto excluído), incluindo o cache em anel dos tiles e o minigame |
| Menus, hi-scores, salvamento | **100%** | o mesmo `App` serve aos dois jogos (código secreto próprio, save separado); splash de 4 s implementado mas desligado por padrão |
| Áudio | **100%** | 12/12 sons validados contra a referência; só `title.ott` e `gameend.ott` diferem do jogo 1; o título toca 3×; **conferido de ouvido** |
| Janela, escala, entrada | **100%** | mesma janela, opções e controles (remapeáveis) do jogo 1; escolha do jogo (`--game 1\|2` ou tela de seleção) |
| **Total estimado** | **100%** | mesmos pesos do jogo 1 |

#### Histórico

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
| Gamepad e remapeamento | Teclado e controle remapeáveis (2 entradas por ação, `bindings.json`), analógico esquerdo, entrada de nome pelo controle; roteador de entrada com testes | ~99% · ~98% |
| Ajustes de engine e tela inicial | Pulo e animação do protagonista mais lentos, opção *Movement* original/revamped (sem pulo contínuo ao segurar ↑, direção segurada vale ao cair/pousar), tela inicial com jogos + opções compartilhadas; `EngineTweaks` desligado nos testes (traces continuam idênticos) | ~99% · ~98% |
| Fechando a decomp do jogo 2 | Leitura de `GameMIDlet`, `a`, `j`, `k`, `h`, `b`, `g`; teste diferencial do hi-score contra o `k` do jogo 2 (achou o nome vazio); ordem do menu do jogo 2, título abre com qualquer tecla / após 5 s, som do título parado ao entrar no jogo 2 | ~99% · ~99% |
| Correções de bugs reportados | Virar no ar em pulo diagonal (revamped); aperto de portal durante a transição ignorado (causa da transição dupla e do crash de `IndexOutOfRange`); tela cheia da tela inicial vale no jogo; *Exit* volta à seleção de jogo; zoom inteiro na tela inicial; crash log e tela de erro | ~99% · ~99% |
| Recordes e instruções | Linha nova destacada nos recordes, Instructions e dicas seguem os controles configurados e o último dispositivo (teclado/controle), *Movement*: "new", entrada reiniciada ao sair da tela inicial | ~99% · ~99% |
| Vibração do controle | "Vibrator" do original vira vibração do gamepad (XInput) ao levar dano; opção *Vibration* nas Options e na tela inicial | ~99% · ~99% |
| Mork mais lento | No jogo 2 o Mork se move a 2/3 da velocidade (`EngineTweaks.MorkSpeedPercent`); desligado nos testes, que seguem idênticos ao original | ~99% · ~99% |
| Mouse nos menus | Menus clicáveis (hover, clique, clique direito, roda); texto do jogo ("Hurry up!") não aparece mais por cima do menu; progresso em 100% nos dois jogos | 100% · 100% |
| Executável e `--logs` | `tools\publish.bat` gera `MikisWorldCollection.exe`, autônomo e de arquivo único; programa de janela, log só com `--logs`; README separado em parte para jogadores e parte técnica | 100% · 100% |
| Código comum em `GameBase` | Estado, entrada, movimento, minigame, projéteis, câmera e varredura de nível dos dois jogos passam para uma classe base (~1000 linhas duplicadas a menos); o que difere de verdade no original continua em cada jogo; traces e quadros idênticos | 100% · 100% |

### Como rodar a partir do código

Precisa do .NET 8 SDK e dos JARs em `JAR/`.

**Windows, no repositório:** dê dois cliques em `play.bat` (ou rode `play.bat`/`play.bat --scale 5` num terminal).
Ele usa o SDK portátil de `tools/dotnet` e recompila sozinho se o código mudou.

Com um .NET 8 SDK instalado no sistema, o equivalente é:

```
dotnet run --project port/MikisWorld.Game -- [--game 1|2] [--scale N] [--octave N] [--settings arq.json] [--bindings arq.json] [--logs] [caminho/para/o.jar]
```

**Executável.** `tools\publish.bat` gera `dist\MikisWorldCollection.exe`: um arquivo só (~35 MB), autônomo (não precisa instalar o .NET); `dist/` não é versionado.
Os JARs não vão junto: coloque `MikisWorld.jar` e `MikisWorld2.jar` numa pasta `JAR` ao lado do exe. É um programa de janela (não abre console).

**Logs.** Só com `--logs`: o log do raylib e o do programa (JARs usados, caminhos de configuração, início e fim de cada jogo). Aberto pelo Explorer, `--logs` abre uma
janela de console; num terminal, escreve nele. Sem `--logs` nada é impresso; travamentos vão para a tela de erro e para `crash.log`.

**Som.** Só um som toca por vez, como no aparelho. `--octave N` ajusta a oitava (padrão `-1`; `0` = como decodificado). O volume é o da tela *Options*.
`--export-sounds pasta` grava um WAV por som; `--sound-test` testa o dispositivo de áudio; `--rumble-test` vibra os controles XInput por meio segundo.

**Ajustes de engine (sempre ligados no programa).** O pulo dura o dobro do tempo (cada passo do arco dura dois ticks: mesma altura e mesma distância),
a animação de andar do protagonista mostra cada sprite pelo dobro do tempo, e no jogo 2 o Mork anda a 2/3 da velocidade original (cada movimento dele tem a mesma
distância e duração em "passos", só leva proporcionalmente mais tempo). No jogo 1 isso também desfaz uma oscilação do original: andando no chão o
estado alterna entre "andando" e "caindo" a cada tick e o sprite de ar piscava. Nos testes de validação (traces/quadros) esses ajustes ficam desligados (`EngineTweaks`).

**Movement `new`, em detalhe.** (1) Segurar ↑ não repete o pulo (um aperto até 100 ms antes de pousar vale); (2) só a tecla solta é liberada (a direção ainda segurada vale), inclusive
no meio de um pulo diagonal; (3) um aperto de "entrar no portal" durante a transição é ignorado, e portais sem destino também. O travamento que isso evita é do **original**: o aperto
extra fica guardado e, depois da transição, o jogo usa o portal da área que acabou de sair, que na área principal não tem destino (`IndexOutOfRange`).

**Arquivos de configuração.** `settings.json` (opções, sem a tela cheia), `bindings.json`, `save.json`/`save2.json` e `crash.log` em `%APPDATA%/MikisWorld/`; `--settings`/`--bindings`/`--save` trocam os caminhos.

### Estrutura

```
docs/                      análise dos dois jogos e dos formatos de arquivo
port/MikisWorld.Core/      lógica + renderização por software (sem dependência de janela/áudio)
                             GameBase*.cs (o que os dois jogos têm em comum: estado, entrada, movimento do jogador, minigame bônus, projéteis, câmera…) · Game*.cs (jogo 1) · Game2*.cs (jogo 2) · App.cs (menus) · RingTone.cs (áudio) · Canvas.cs · InputBindings.cs (remapeamento) · EngineTweaks.cs
port/MikisWorld.Game/      frontend Raylib (janela, texto, áudio); lê os recursos direto dos JARs
                             InputRouter.cs (teclado/controle → eventos do celular) · DevicePoller.cs (lê o Raylib) · Launcher.cs (tela inicial)
port/MikisWorld.TraceTest/ replay de traces/quadros e testes de fluxo, sons, hi-scores, cutscenes, roteador de entrada e ajustes de engine
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

### Reproduzindo o ambiente de validação

1. `jar xf JAR/MikisWorld.jar` em `extracted/MikisWorld/` e `jar xf JAR/MikisWorld2.jar` em `extracted/MikisWorld2/`.
2. SDK .NET 8 portátil: `dotnet-install.ps1 -Channel 8.0 -InstallDir tools/dotnet`.
3. (Opcional) Decompilador: CFR 0.152 → `tools/cfr.jar`, de `repo1.maven.org/maven2/org/benf/cfr/0.152/`.
4. Referências, a partir dos JARs originais (JDK 11+ e Python):
   `tools/record_traces.sh`, `tools/record_traces2.sh`, `tools/record_frames.sh`, `tools/record_frames2.sh`,
   `tools/record_hall.sh`, `tools/record_ott.sh`.
5. `tools/run_tests.sh` roda tudo e deve terminar com `ALL VALIDATIONS PASSED`: 17/17 traces e 10/10 conjuntos de quadros do
   jogo 1, 20/20 traces e 9/9 conjuntos de quadros do jogo 2, hi-scores, decoder de sons (24/24), balões de cutscene e fluxo do app.

### Validação

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

### Áudio

Os 12 efeitos/músicas de cada jogo são Nokia Ring Tones (`.ott`). O decoder (`RingTone.cs`) foi escrito a partir do layout de
bits do formato Smart Messaging e validado contra um decoder independente em Python (`tools/decode_ott.py`). A síntese é uma
onda quadrada simples, como o buzzer do celular, e só um som toca por vez.

Dois pontos foram **inferidos** (a especificação oficial não estava acessível): um padrão com valor de loop *n* toca *n+1* vezes,
e a oitava de cada nota. Como quase todos os sons decodificados ficavam entre 0,7 e 2,6 kHz (agudos demais), o padrão do port é
**uma oitava abaixo** — escolha confirmada de ouvido nos dois jogos. `--octave N` permite ajustar.

</details>
