# Miki's World 2 ("The Storm Master") — análise do JAR

J2ME / MIDP 1.0 / CLDC 1.0, Sumea, v1.2.2. Decompilado com CFR 0.152 em `decompiled/MikisWorld2/`
(recursos em `extracted/MikisWorld2/`). Veja [`MikisWorld-analysis.md`](MikisWorld-analysis.md) para o jogo 1: este
documento só descreve **o que muda**.

> Itens marcados **(inferido)** vêm da leitura do código ainda não validada contra o jogo rodando.
> Ainda não li o código de desenho (`R9`/`R10` de `c`), a troca de área (`R10(int)`) nem a classe `d` (cutscenes) por inteiro.

## Resumo

É o **mesmo motor**: o jogo 2 foi feito a partir do código do jogo 1. Cerca de 85% do bytecode da classe principal é
idêntico (39 de 50 métodos do jogo 1 têm correspondente ≥90%), e o minigame bônus, o carregamento do mapa, o parser
do `level.bin` e as classes de som, vibração, hi-score e a base de tela (`j`) são praticamente iguais.
O que muda é conteúdo e algumas mecânicas, listadas abaixo.

## Mapa de classes (jogo 1 → jogo 2)

Os nomes foram reembaralhados pelo ofuscador; o casamento abaixo é por similaridade de bytecode
(`tools/classmatch.py`), não por nome.

| Papel | Jogo 1 | Jogo 2 | Similaridade | Observação |
|---|---|---|---|---|
| MIDlet / fluxo | `GameMIDlet` | `GameMIDlet` | 88% | +2 strings de UI, tela de abertura (splash) |
| Tela-título e menus | `b` | `a` | 84% | mostra `splash.png`; `TextBox` no lugar de `TextField` para o nome |
| Som | `c` | `b` | 98% | mesmos 12 efeitos |
| **Jogo** | `d` | `c` | 86% | ver "Mecânicas novas" |
| Cutscenes | `e` | `d` | 78% | script binário parecido; ver abaixo |
| Colisão de tiles | `f` | `e` | 84% | |
| Layout / constantes | `g` (+`l`) | `f` | 56% | resolução e tamanhos diferentes; carrega os tiles como `Image[]` |
| Vibração | `h` | `g` | 100% | |
| Formatador de placar | `i` | `h` | 97% | |
| Camada de tiles | `a` | `i` | 52% | desenho por tile individual |
| Base de tela | `j` | `j` | 80% | |
| Hi-score | `k` | `k` | 98% | |

Não há mais shims da Nokia dentro do JAR (`com.nokia.*` fica por conta do aparelho) nem código de rede:
as strings "Refresh" / "Please wait, receiving scores..." existem em `en`, mas nenhuma classe as usa.

## Resolução e arte

| | Jogo 1 | Jogo 2 |
|---|---|---|
| Tela | 176×144 | **128×128** (`f.j()`, `f.l()`) |
| Tile | 14×14 | **12×12** (`f.R7()`, `f.m()`); `f.g()` fatia `tiles.png` em **25** imagens |
| Largura do jogador | 16 | **15** (`f.d()`) |
| `player.png` | 8 quadros | **6** quadros (`getWidth()/6`) |
| Fundo | `sky.png` + `clouds.png` (parallax) | **não existem** — o fundo é outro (a investigar) |
| `symbols.png` | sim | não existe |
| `splash.png` | não | sim |

Recursos **idênticos** aos do jogo 1: `bonus.ott`, `cage.ott`, `coin.ott`, `damage.ott`, `damage.png`, `gameover.ott`,
`gamestart.ott`, `key.ott`, `lord.ott`, `numbers.png`, `onceuponatime.ott`, `time.png`, `transportation.ott`.
Diferentes: `title.ott`, `gameend.ott`, `title.png`, `tiles.png`, `player.png`, `lord.png`, `items.png`, `end.png`, `grid.png`,
`cursor.png`, `bonus.png`, `gameover.png`, `icon.png`, `level.bin` (1801→3435 bytes), `begin.bin`, `end.bin`, `en`, `en.hlp`.

## Formatos

- **`level.bin`** (**validado**: `python tools/parse_level.py level.bin --v2` consome o arquivo até o último byte).
  Cabeçalho na mesma ordem do jogo 1 (flags de plataformas, Lords, posições iniciais, subáreas, linhas-limite, células do
  portão, total de moedas, posição do Lord final), com **duas diferenças**:
  1. não existe a lista de "células de saída" (`bi`) entre as células do portão e o total de moedas;
  2. o contêiner da camada de tiles é mais simples: `byte camadas`, `(camadas-1)*2` bytes ignorados e a camada 0 logo em
     seguida; e os tiles têm **8 bits** (`bpp=8`, 4 por `int`), não 4.

  Conteúdo: mapa **16×205**, **87 plataformas**, 2 subáreas, 76 moedas no mapa (78 declaradas), 16 tiles `12` (arco-íris),
  1 chave (7), 4 portais (15), `Lord1` na plataforma 40 e `Lord2` na 6, jogador inicia em `(40, 780)`.
- **`en`**: 55 strings (jogo 1: 33). As 18 primeiras são UI (jogo 1: 16) e `GameMIDlet.a(n)` passa a ler `case[18 + n]`.
  Texto novo: Mork, arco-íris, falas do Storm Master ("Halt!", "Thou shall not pass!"…), "Go get the tiara!".
- **`en.hlp`**: instruções reescritas; menciona **duas salas secretas** e o minigame ("coins" em vez de orbes).
- **Persistência**: `AppData`, `GameData`, `HallOfFame` — iguais às do jogo 1.
- **Cheat do bônus** (tabela invertida): `8 3 3 7 7 7 2 2 2 3 3 7 7 7 7` (jogo 1: `7 7 7 7 3 3 2 2 2 7 7 7 3 3 8`).

## Mecânicas novas ou alteradas (em `c`)

1. **Mork, um perseguidor** ("Mork is after you!"): campos `c0` (estado 0 = parado/vigiando, 1–4 = indo para esquerda/direita/
   cima/baixo), `aX`/`aW` (posição, ponto fixo <<4), `bw` (temporizador), `b5` (quadro de animação). Vai ao encontro do
   jogador por eixos; ao chegar perto causa dano (`R2`), que tira uma vida, toca `damage`, vibra e arma um contador
   `aE = 21`, decrementado a cada passo (provavelmente o piscar do jogador; **inferido**, nada o usa como invencibilidade
   no trecho que li). Na fase final ele acelera conforme o progresso (`ba`).
2. **Arco-íris no lugar dos pássaros**: o tile 12 (e o 21, sua placa) é coletado e preenche até 4 posições de animação
   (`cm`/`ck`); no estágio final os arco-íris coletados viram uma **escada** ("Collect 10 rainbows to build a ladder up",
   `ba` conta até 10, eventos em `bm[80]`). **(inferido)**
3. **Tiles renumerados**: portão = 8–11 (jogo 1: 16–19); a chave (7) abre o portão em `(bu, bs)`; moeda 6/20 e portais 15/22 iguais.
4. **Física**: movimento do jogador congelado durante cutscene/ending (`cY != 20 && != 12`); gravidade base `2`
   quando parado em vez de `0`; sobre plataforma o deslocamento vertical é zerado; **a colisão com tiles roda antes da
   varredura de plataformas** (ordem invertida em relação ao jogo 1). As tabelas de pulo e velocidades (`cE`, `bi`, `bW`)
   seguem a mesma fórmula, agora com `cy`/`cx` calculados do tile de 12 px. Há um novo `aK = cx*240/100`.
5. **Dano em um só lugar** (`R2`): projéteis dos Lords e Mork chamam a mesma rotina.
6. **Cheat e fluxo de jogo** iguais, exceto a tabela do código.

Sem alteração relevante (bytecode ≥ 93% igual): troca de área para subáreas/estágio final, varredura do nível,
atualização de plataformas móveis, câmera, e **todo o minigame** (100%).

## Plano de port

A forma mais segura de manter a validação bit a bit é uma **segunda tradução literal** da classe `c` (`Game2`), reutilizando
tudo o que é idêntico:

1. Generalizar o harness (`tools/harness/src/Trace.java`) para nomes de campo por jogo (o jogo 2 usa outras letras).
2. Parametrizar o que já é configurável em `Layout`/`Game` (resolução, tile, largura do jogador, tabelas de tiles, cheat).
3. Traduzir só os métodos que divergem: `UpdatePlayer`, início/reinício, desenho, entrada/inicialização, Mork e arco-íris.
4. Gerar traces e quadros do JAR 2 com o mesmo pipeline e exigir 100% de igualdade.
5. `App` ganha um seletor de jogo (ou dois executáveis) com assets, strings e cheat do respectivo JAR.

Reaproveitável sem mudança: `JavaRandom`, `DataReader`, `TileCollision`, `HallOfFame`, `SaveData`, `RingTone` (`.ott` iguais),
`Canvas`/`Bitmap`, o minigame inteiro, o player de cutscene (a confirmar: 78% de similaridade) e o shell de menus.

## Ainda por ler

`c`: `R9`/`R10` (desenho), `R10(int)` (troca de área), `R6` (projétil), `n()`/`i()`/`m()`; `d` (cutscenes, 78%);
`i` (camada de tiles, 52%); `j` (base, 80%); como o fundo é desenhado sem `sky.png`/`clouds.png`.
