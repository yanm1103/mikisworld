# Miki's World 1 ("The Lost Tiara") — análise do JAR

J2ME / MIDP 1.0 / CLDC 1.0, Nokia UI API. Sumea, v1.1.2. Tela alvo 176x144.
Fonte decompilada (CFR 0.152): `decompiled/MikisWorld/` — nomes ofuscados; identificadores ilegais
(`else`, `new`, `do`…) foram trocados por `cfr_renamed_N` para o código ser sintaticamente válido.
Recursos extraídos: `extracted/MikisWorld/`.

> O que está abaixo começou como leitura do código decompilado e foi **confirmado rodando o JAR original** num harness sem tela
> (`tools/harness`) e comparando, tick a tick e pixel a pixel, com o port em C# (veja o [README](../README.md#validação)).
> Itens marcados **(inferido)** são interpretações de nomes/semântica que não aparecem nos testes (por exemplo, o motivo de um campo).
> Para o jogo 2, veja [`MikisWorld2-analysis.md`](MikisWorld2-analysis.md).

## Mapa de classes

| Classe | Papel |
|---|---|
| `GameMIDlet` | ciclo de vida, carrega strings (`/en`, `/en.hlp`), alterna entre tela-título (`b`) e jogo (`d`), thread principal |
| `j` | Canvas base: loop com delta-time suavizado (média de 5 amostras, teto 500 ms), comandos de menu, flags de som/vibração em RMS `AppData` |
| `i` extends `j` | stub; só formata o placar (`"" + n`) |
| `b` extends `j` | tela-título + menus (Sobre, Ajuda, Hi-scores, entrada de nome) |
| **`d` extends `i`** | **o jogo todo (~2600 linhas)**: física, colisão com plataformas, itens, inimigos, estados, render, minigame bônus |
| `a` | camada de tiles (mapa 4 bpp, scroll, wrap opcional) |
| `f` | colisão tile-a-tile com swept AABB por eixo (flags por tile: bits 1/2/4/8/16 = bloqueios por direção) |
| `e` | player de **cutscenes** com script binário (`begin.bin`, `end.bin`) |
| `c` | áudio (12 efeitos `.ott` — Nokia Ring Tone) |
| `h` | vibração e luz de fundo (Nokia `DeviceControl`) |
| `k` | tabela de hi-scores em RMS (`HallOfFame`) |
| `g`, `l` | só constantes de layout (HUD, tamanho de tela) |
| `com.nokia.mid.*` | shims da API Nokia embutidos no JAR (`DirectGraphicsImplemented` é uma implementação sobre MIDP puro) |

## Persistência (RMS)

- `AppData` — 3 bytes `{?, som, vibração}`.
- `GameData` — 1 byte: `1` = minigame bônus liberado.
- `HallOfFame` — 1 tabela × 10 entradas (nome UTF + int), ordenação "maior é melhor".
- No port, os três viram um único `save.json` (`%APPDATA%/MikisWorld/`), e a tabela de hi-scores (`HallOfFame.cs`) foi comparada com a classe `k` original.

## Máquina de estados de `d` (campo `cK`)

| cK | Estado |
|---|---|
| 0 | jogando normal |
| 3 | game over (some após 3 s) |
| 4 / 5 | encontro com o "Lord" 1 / 2 (pisca, projéteis, libera plataformas ao fim) |
| 6 | fase final (contagem regressiva `cQ`, começa em 67000 ms) |
| 7 | tratado como "jogando" (só atualiza o jogador); **nenhum dos traces e nenhum caminho do código entra nele** |
| 10 / 11 | wipe de tela (35 frames) saindo / entrando |
| 12 | tela de fim (`end.png` por 7 s, depois 4 linhas de texto até 14 s; FIRE antecipa) |
| 20 | cutscene (script `e`) |
| 21 | menu "Jogo principal / Bônus" (`List` LCDUI) |
| 22 / 23 / 24 / 25 | minigame bônus: vez do jogador / animação da explosão / IA pensando / IA jogando |

Área atual (`c2`): `-1` cutscene inicial, `-2` final, `0` mundo principal, `1` sala/subárea (entrada por porta), `2` fase final.

## Plataforma

- Ponto fixo **4 bits** (`<<4`) para posição do jogador; tabela de salto parabólica `S[14] = {67,64,58,48,36,23,7,-7,-23,-36,-48,-58,-64,-67}` (escalada por `ck/16`).
- Escala de resolução: `cl = 16*tileW/8`, `ck = 16*tileH/8`. **Todas** as coordenadas do `level.bin` são multiplicadas por `cl/16` ou `ck/16` (o jogo foi feito para vários tamanhos de tela). No port em resolução fixa isso vira constante.
- Vel. horizontal: `be = 2*cl*90/100` (andando e correção no ar) e `cr = cl*90/100` (metade; usada no pulo reto quando a tecla lateral é solta/pressionada).
- Estados do jogador (`n`): 0 parado, 1/2 andando esq/dir, 3/5 pulo diagonal esq/dir, 4 pulo vertical, 6 caindo.
- Plataformas (`cm[i] = {x, y, largura, flags}`): flags bit0 = **oculta/inativa** (não desenha nem colide; os Lords limpam o bit ao fim do encontro e as plataformas aparecem), bits1-2 = movimento (2 = dir, 4 = esq), bits3-5 = amplitude em tiles (0 = wrap-around), bits6-7 = velocidade.
- Tiles do mapa (`tiles.png`, folha 9x3 de 14x14; id = coluna + 9*linha): `1` flor (decoração), `2-4` topo de grama (plataforma de mão única), `5` terra (sólido), `6/20` moeda (2 frames), `7` chave, `8` marca a posição do portão, `12` **gaiola com pássaro** (tocar = liberta o pássaro, toca `cage.ott`, `H++`), `13` nuvem (marcador de plataforma; varrido no load e apagado, a plataforma é desenhada com este tile), `14` quadrado vermelho de editor (não usado), `15/22` **portal giratório** (aperte 8 para entrar numa subárea), `16-19` portão dourado 2x2 (aparece ao pegar a chave; 8 leva ao estágio final), `21` placa acima de cada gaiola, `23` bloco laranja (o estágio final ganha 1 por pássaro libertado), `24` coração (HUD de vidas).
- Coletar todas as moedas (`a1 == c7`, 55) **libera o minigame bônus**. A cada 15 moedas ganha uma vida (máx. 6; começa com 3).
- Inimigos: até 3 projéteis simultâneos (`bH`/`by`), spawnados pelos Lords; caem, quicam lateralmente ao tocar plataforma; encostar tira 1 vida + vibra.
- Cheat de teclado → libera bônus: `7 7 7 7 3 3 2 2 2 7 7 7 3 3 8` (tabela `aG`; códigos 55,55,55,55,51,51,50,50,50,55,55,55,51,51,56).
- Controles: `1`/`2`/`3` pulo (diag. esq / vertical / diag. dir), `4`/`6` ou ←/→ andar, `↑` pula, `8` ou `↓` interage (porta/gaiola), `5` ou FIRE pula cutscene/texto.

## Minigame bônus (estados 21–25)

Tabuleiro **5×4**, estilo "Chain Reaction/Atoms". Cada célula: bits 0-2 = nº de orbes, bit 3 = dono (0 jogador, 1 CPU).
Massa crítica = 4 − (1 se coluna da borda) − (1 se linha da borda): cantos 2, bordas 3, centro 4. Ao atingir, a célula zera e empurra 1 orbe a cada vizinho (fila circular `R[40]`, reação em cadeia, animada em 10 frames).
**IA (`cfr_renamed_12`)**: simula cada jogada possível, ordena as 3 melhores pelo saldo de orbes e escolhe a melhor com 60% de chance (40%: aleatória entre as 3); no empate cai para cantos/bordas pré-definidos (`bB`).
Vence quem zera os orbes do outro. Barras laterais (`bg`/`al`) mostram a proporção.

## Formatos de arquivo

### `level.bin` — **validado** (`tools/parse_level.py` consome o arquivo até o último byte)
Big-endian (`DataInputStream`). Ordem: `byte N`, `N` flags de plataforma, `byte lord1Plataforma`,
`byte cnt + cnt bytes` (3 índices de 10 bits empacotados), `short x,y` do lord 1, idem lord 2, `short×2` start principal,
`short×2` start final, `byte K` + `K×(short,short)` spawns de subárea, 2 `short` de limite vertical, `K` `short`, `byte`+`short[]` (células do portão), `byte`+`short[]` (células de saída), `byte` total de moedas, `short×2` pos do Lord final, depois o **mapa**:
`byte layers`, `int[layers]` offsets, e na camada 0: `short w, short h, byte bpp, int[]` com 8 tiles de 4 bits por int (MSB primeiro).
Mapa: **16×205 tiles**, 82 plataformas, 2 subáreas, 55 moedas, 1 chave, 4 gaiolas, 11 portas.

### `begin.bin` / `end.bin` — script de cutscene (classe `e`)
`byte N` (nº de atores), `N` bytes de tipo de sprite, `byte` tempo do 1º evento, depois eventos `{byte op; args…; byte deltaTempo (−1 = fim)}`:
`0` mostra ator (id,x,y) · `1` esconde · `2` posição da câmera · `3` move ator (dx,dy,frame) · `4` toca som (0 gaiola, 1 Lord, 2 dano) · `5` sem efeito · `6/7` liga/desliga balão de texto (índice de string).
O tempo do 1º evento é lido e **descartado** (o loader zera o contador logo depois). As flags de balões são **recriadas a cada `Load`**: um balão ligado na introdução não aparece na cutscene final.
Tipos de sprite: 0-1 jogadora (`player.png`), 2-3 Lord (`lord.png`), 4-5 itens, 6 dano, 7-10 símbolos (`symbols.png`).

### `en` / `en.hlp`
`en`: `byte N` + `N×UTF` (33 strings: 0-15 = UI/menus, 16-32 = os 17 textos in-game `GameMIDlet.a(i)`: dicas, falas das cutscenes, créditos finais). `en.hlp`: 1 `UTF` com o texto de ajuda.

### `*.ott` — Nokia Ring Tone (Smart Messaging) — **decoder validado**
Bits em ordem MSB: `0x02`, comando `0x25` (7 bits) + 1 bit de alinhamento, comando `0x1D` (7 bits), tipo de canção (3 bits, 1 = básica),
título (4 bits de tamanho + 8 bits por caractere), nº de padrões (8 bits). Cada padrão: cabeçalho (3 bits id, 2 bits id do padrão,
4 bits de *loop*), nº de instruções (8 bits) e as instruções (id de 3 bits): `1` nota (4 bits nota 0-12, 3 bits duração 1/1…1/32,
2 bits especificador: normal, pontuada ×3/2, dupla pontuada ×9/4, tercina ×2/3), `2` escala (2 bits), `3` estilo (2 bits),
`4` andamento (5 bits → tabela de 32 BPM de 25 a 900), `5` volume (4 bits).

Os sons: `title`, `onceuponatime`, `gamestart`, `gameend`, `gameover`, `lord`, `transportation`, `bonus`, `cage` (gaiola com pássaro), `coin`,
`damage`, `key`. O decoder em C# (`RingTone.cs`) bate nota a nota com um decoder independente em Python (`tools/decode_ott.py`).
**(inferido)** um padrão com valor de *loop* n toca n+1 vezes; e a escala 0 é a oitava com Lá = 440 Hz. Como isso deixava quase todos
os sons agudos demais (0,7–2,6 kHz) numa onda quadrada, o port toca **uma oitava abaixo** por padrão (`--octave`), o que foi
confirmado de ouvido.

## Estado do port

Concluído e validado (veja o README para os números): lógica, minigame, desenho, menus/hi-scores/salvamento, áudio e janela.
Onde cada coisa está no C# (`port/MikisWorld.Core/`):

| Original | Port |
|---|---|
| `d` (jogo) | `Game*.cs` (`Game.Level`, `Game.Update`, `Game.Player`, `Game.Bonus`, `Game.Draw`) |
| `a` (tiles) | `TileLayer.cs` (dados) + desenho em `Game.Draw.cs` |
| `f` (colisão) | `TileCollision.cs` |
| `e` (cutscenes) | `CutscenePlayer.cs` |
| `k` (hi-scores) | `HallOfFame.cs` + `SaveData.cs` |
| `c` (som) | `RingTone.cs` + `Sfx.cs` (+ `AudioHost` no frontend) |
| `GameMIDlet`, `b`, `j` | `App.cs` (menus, formulários, fluxo) e o frontend `MikisWorld.Game` |

## Bugs do original

- Cair abaixo do mapa causa `ArrayIndexOutOfBounds` na colisão; entrar num portal sem subárea correspondente (há 4 portais e só 2 subáreas)
  faz o mesmo em `d.for(int)`. O port reproduz os dois **de propósito** nos traces (que usam teleportes); não verifiquei se uma partida normal consegue provocá-los.
- O "texto Let me tell you a story.." aparecia na cutscene final por um bug **do port** (flags não recriadas), já corrigido — não é do original.
