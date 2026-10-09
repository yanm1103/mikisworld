# Miki's World 2 ("The Storm Master") — análise do JAR

J2ME / MIDP 1.0 / CLDC 1.0, Sumea, v1.2.2. Decompilado com CFR 0.152 em `decompiled/MikisWorld2/`
(recursos em `extracted/MikisWorld2/`). Veja [`MikisWorld-analysis.md`](MikisWorld-analysis.md) para o jogo 1: este
documento só descreve **o que muda**.

> Itens marcados **(inferido)** vêm da leitura do código ainda não validada contra o jogo rodando.
> Segunda passada concluída em `c` (tick, jogador, troca de área, desenho), `d` (cutscenes), `f` (layout), `i` (tiles) e `j` (base).

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
| Fundo | `sky.png` + `clouds.png` (parallax) | **não existem**: o céu é o **tile 0** de `tiles.png` (azul opaco); a camada de tiles desenha todos os tiles, inclusive o 0 |
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
   jogador por eixos; ao chegar perto causa dano (`R2`), que tira uma vida, toca `damage`, vibra e arma o contador
   `aE = 21` (decrementado a cada passo), que controla a **faísca de dano** desenhada ao redor do jogador; não vi
   nada que o use como invencibilidade. Na fase final ele acelera conforme o progresso (`ba`).
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

## Detalhes (segunda passada)

### Máquina de estados e tempos
Os estados são os mesmos do jogo 1 (0, 3, 4, 5, 6, 10, 11, 12, 20, 21–25), mas os tempos mudaram:

| | Jogo 1 | Jogo 2 |
|---|---|---|
| Lord 1 (estado 4): piscar / tiro / libera plataformas / fim | 30 / 60 / 90, 110, 130 | **10 / 20 / 30, 40, 50** |
| Lord 2 (estado 5): piscar / libera plataformas / fim | 30 / 50, 70 | **10 / 30, 40** |
| Tela de fim (estado 12) | 14 s (texto após 7 s) | **7 s** |
| Primeiro encontro (aviso na tela) | — | "Mork is after you!" por 3 s, depois "Hurry up!" piscando até 6 s (`cL`, que parte de 10000 e é zerado ao fim da cutscene) |

O tick recebe dois parâmetros e **o segundo importa**: o tempo total acumulado em ms. O estágio final usa-o para agendar o
reaparecimento dos arco-íris (`bm[]` guarda `tempo + 4000`). No harness do jogo 1 esse parâmetro era sempre 0; para o jogo 2
ele precisa ser acumulado (20 ms por tick). A tela só é redesenhada quando o tick rodou (flag `b1`).

### Mork (perseguidor)
Não age em cutscene, na tela de fim nem nas subáreas. Estados `c0`: 0 = pensando; 1/2 = vai para esquerda/direita; 3/4 = sobe/desce.
No estado 0, a cada `bw` ms compara a posição do jogador com a sua (`aX`,`aW`, ponto fixo): se o jogador está muito acima
(mais que `1,5 telas`) ele **teletransporta** para baixo do jogador; senão escolhe o eixo de maior distância e anda
(`bw = 4 tiles * 16`), parando em `bw <= 0`. Move `cy`/`cx` (velocidade de tile) por passo, mais rápido no estágio final
(`× (200 + 200·ba/10)/200`). Ao sobrepor o jogador (metade da largura/altura do sprite de `lord.png`) chama `R2`
(dano), volta ao estado 0 e espera 7 s; `R2` também arma `bw = 4000`. Nasce em `(F, D + 5 tiles)` no estágio final. **(inferido; falta validar)**

### Arco-íris e estágio final
- Um arco-íris ocupa **2 tiles** (12 à esquerda, 21 à direita); coletar apaga os dois e acende um dos 4 sprites flutuantes
  (`cm`/`ck`, sobem `aK = cx·240/100` por passo até sair da tela). Fora do estágio final conta em `I` (como os pássaros).
- No estágio final (`dg == 2`), cada arco-íris coletado (até 10, `ba`) reagenda o reaparecimento do mesmo par de tiles 4 s depois;
  na 10ª coleta toca `gameend`, carrega `end.bin` e vai para a área −2. O HUD mostra o ícone (tiles 12+21) e `10 − ba`.
- Não existe mais a lista `bi` (células de saída) nem a limpeza dessas células ao entrar no estágio final. Os blocos laranja
  (tile 23) continuam sendo criados em `ci[]` (uma dupla por arco-íris que o jogador trouxe do mundo principal).
- Portão: **fechado** = tiles 16–19 (já no mapa, marcado pelo tile 16); a **chave** (7) o troca pelo **aberto** = 8–11 (que dá o
  `65535` de "interagir"). No jogo 1 era o inverso.

### Cutscenes (`d`)
Mesmo esqueleto do jogo 1, com diferenças: opcode **6** lê um `int` (cor RGB do balão) além do índice da fala; opcode **8** coloca
as 4 peças do portão fechado (16–19) em `(x, y)`; opcode **4** só toca/vibra para o id 2 (dano); tipos de sprite novos **11**
(2 blocos laranja) e **12** (3 nuvens), e não há mais os tipos 7–10 (`symbols.png`). A câmera da cutscene é deslocada
`(176 − 128)/2 = 24 px` no estado 20.

### Desenho
- Sem parallax. Mapa → nuvens-plataforma (sem o "afundar" ao pisar) → Mork/Lords → arco-íris flutuantes → faíscas de dano
  (`aE > 0`, um único efeito ao redor do jogador) → jogador → HUD → balões.
- Vidas = tile 24 (coração em moldura amarela); contador de moedas com os mesmos dígitos de `numbers.png`.
- A camada de tiles guarda uma imagem em cache e redesenha só os tiles alterados (`i.a(x, y, tile)`); o resultado visual é o
  mesmo de redesenhar tudo.
- **Minigame**: lógica idêntica, desenho novo: fundo de cor chapada `(60,179,210)` com uma fileira de nuvens (tile 13) embaixo,
  grade de células **20×23**, peças **9×9**; sem barras laterais nem os personagens do jogo 1.

### Shell (menus)
`j` agora estende `com.nokia.mid.ui.FullCanvas` (sem softkeys do sistema) e monta o menu num `List` ("Miki's World 2"):
as teclas virtuais `-6`/`-7` (softkeys) abrem o menu e `-5` (botão central) vale como `5`/FIRE. As opções (comandos, som,
vibração, hi-score, instruções, sobre) são as mesmas do jogo 1, e `GameMIDlet` acrescenta a tela `splash.png` antes do título.
O `App` do port já tem o menu equivalente (Esc/Enter).

### Layout 128×128 (classe `f`)
| Constante | Valor | Constante | Valor |
|---|---|---|---|
| tela | 128 × 128 | tile | 12 × 12 (25 imagens) |
| largura do jogador | 15 | janela de tiles | 12 × 12 |
| dígito `numbers` | 8 | dígito `time` | 14 |
| sprites do Lord | 36 (frame) / 24 (pose) | contador de moedas | (2, 115) |
| relógio do estágio final | (98, 2) | vidas | (111, 114), da direita para a esquerda |
| células do minigame | 20 × 23 | peças do minigame | 9 × 9 |
| barras | largura 5, tira 8 | (gameover.png) | centralizado |

## Ainda por ler / validar

Tudo acima vem de leitura; nada foi executado. Falta: o desenho do minigame por inteiro, as telas de menu/entrada de nome
(`a`, `GameMIDlet`) linha a linha, o `c.f()`/`d()` residuais, e **rodar o JAR original no harness** para confirmar Mork, arco-íris
e tempos (o harness precisa dos nomes de campo do jogo 2).
