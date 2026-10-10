# Miki's World 2 ("The Storm Master") — análise do JAR

J2ME / MIDP 1.0 / CLDC 1.0, Sumea, v1.2.2. Decompilado com CFR 0.152 em `decompiled/MikisWorld2/`
(recursos em `extracted/MikisWorld2/`). Veja [`MikisWorld-analysis.md`](MikisWorld-analysis.md) para o jogo 1: este
documento só descreve **o que muda**.

> Tudo abaixo foi **confirmado rodando o JAR original** no harness (`tools/harness` + `names_mw2.properties`) e comparando com o
> port (`Game2`): 20 traces de estado e 9 conjuntos de quadros idênticos (veja o [README](../README.md#validação)).
> **Todas as 12 classes foram lidas** (a tabela "Cobertura por classe" abaixo diz como cada uma foi verificada); não sobra nenhum item
> apenas "inferido": o último (a escada de arco-íris) foi conferido contra o código e o texto de ajuda do jogo.

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

## Cobertura por classe

| Classe (jogo 2) | Papel | Como foi verificada |
|---|---|---|
| `c` | jogo (tick, jogador, Mork, arco-íris, desenho, minigame) | lida por inteiro; **20 traces + 9 conjuntos de quadros idênticos** ao original |
| `d` | cutscenes | lida; quadros das cutscenes idênticos; teste de balões (`cutscene`) |
| `e` | colisão de tiles | lida; coberta pelos traces (a ordem em relação às plataformas é a invertida, ver abaixo) |
| `f` | layout / constantes | lida; as constantes impressas pelo original (`#consts`) conferem com o port |
| `i` | camada de tiles | lida; quadros idênticos, inclusive o cache em anel |
| `k` | hi-score | diff linha a linha com o jogo 1 + **teste diferencial contra a classe `k` do próprio JAR 2** (5 sementes × 400 passos) — achou uma diferença real, ver "Fluxo e menus" |
| `b` | som | diff com o jogo 1: mesmos 12 efeitos, só o título toca 3× e é **parado** ao entrar no jogo; decoder validado contra referência independente |
| `g` | vibração / luz de fundo | idêntica à do jogo 1 (sem equivalente no PC) |
| `h` | base do jogo (formata o placar, `hideNotify` abre o menu) | lida (é curta); o `cfr_renamed_9(int)` que o MIDlet chama com o recorde é vazio, ou seja, o recorde não aparece em lugar nenhum |
| `j` | base de tela e menu de comandos | diff normalizado com o jogo 1: o jogo 2 usa `FullCanvas` e **monta o menu à mão** (`List` + `Vector` ordenado pela prioridade dos comandos) em vez dos comandos nativos |
| `a` | tela-título e menus | lida; ver "Fluxo e menus" |
| `GameMIDlet` | ciclo de vida e troca título ↔ jogo | lida; as diferenças em relação ao jogo 1 são só de ciclo de vida do aparelho (pausa/retomada), sem equivalente no PC |

A parte de menus/formulários usa a API de interface do MIDP (`List`, `Form`, `TextBox`), que o harness não simula; ali a verificação é por leitura do
código e por testes do port (`apptest`), não por comparação com o original rodando.

## Fluxo e menus (o que a leitura de `GameMIDlet`, `a`, `j` e `k` mostrou)

Estas diferenças em relação ao que o port fazia foram encontradas na leitura e **corrigidas** (valem para os dois jogos, salvo indicação):

- **Ordem do menu.** Cada comando tem uma prioridade e o menu é ordenado por ela. No jogo 2 *High scores* tem prioridade 3 e *Instructions* 4 (no jogo 1 é o
  contrário): *Continue game, New game, High scores, Instructions, Sound, Vibration, About, Exit*.
- **Título.** *Qualquer* tecla abre o menu, e **5 s sem tecla** o abrem sozinho; o relógio recomeça sempre que o título volta a ser exibido (ao fechar o menu).
- **Nome vazio no hi-score.** A classe `k` do jogo 1 testa `name.equals(" ")`; a do jogo 2 testa `name.equals("")`, então no jogo 2 um nome vazio vira `" "`
  e no jogo 1 continua vazio. Foi o teste diferencial que achou isso.
- **Som do título.** O jogo 2 chama `Sound.stop` do título ao entrar no jogo (novo jogo ou *Continue game*); o jogo 1 deixa tocar.
- Sem mudança: texto do "About", `TextBox` do nome com no máximo 8 caracteres, formato "n. NOME - pontos" da lista de recordes, splash de 4 s
  (1 s preto, 2 s imagem, 1 s preto) que o port tem mas deixa desligado.

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
   nada que o use como invencibilidade. Na fase final ele acelera conforme o progresso (`ba`). Os quatro movimentos aparecem
   nos traces e o port os reproduz tick a tick.
2. **Arco-íris no lugar dos pássaros**: o tile 12 (e o 21, sua placa) é coletado e preenche até 4 posições de animação
   (`cm`/`ck`). Os arco-íris coletados no mundo viram **blocos de apoio** no estágio final (um par de tiles `Ledge` por arco-íris coletado); no estágio final cada arco-íris pego ressurge **4 s depois** no mesmo lugar, como plataforma (`ba` conta até 10 e os eventos
   agendados ficam em `bm[80]`). Isso é o "ladder"/"platforms" do texto do jogo ("Collect 10 rainbows to build a ladder up"; ajuda: "Collect rainbows,
   and they will form platforms for you in the final battle"). Conferido no código do port e nos traces do estágio final.
3. **Tiles renumerados**: portão **fechado** = 16–19 (já no mapa, marcado pelo tile 16) e **aberto** = 8–11, o inverso do jogo 1; a chave (7) troca
   um pelo outro em `(bu, bs)`; moeda 6/20 e portais 15/22 iguais. Os tijolos rosa (tile 5) **não são sólidos** (flag 0).
4. **Física**: movimento do jogador congelado durante cutscene/ending (`cY != 20 && != 12`); gravidade base `2`
   quando parado em vez de `0`; sobre plataforma o deslocamento vertical é zerado; **a colisão com tiles roda antes da
   varredura de plataformas** (ordem invertida em relação ao jogo 1). As tabelas de pulo e velocidades (`cE`, `bi`, `bW`)
   seguem a mesma fórmula, agora com `cy`/`cx` calculados do tile de 12 px. Há um novo `aK = cx*240/100`.
5. **Dano em um só lugar** (`R2`): projéteis dos Lords e Mork chamam a mesma rotina.
6. **Cheat e fluxo de jogo** iguais, exceto a tabela do código.

Sem alteração relevante (bytecode ≥ 93% igual): troca de área para subáreas/estágio final, varredura do nível,
atualização de plataformas móveis, câmera, e **todo o minigame** (100%).

## Estado do port

Feito como **segunda tradução literal** da classe `c` (`Game2`), reutilizando tudo o que é idêntico, e validado como o jogo 1:

| Original (jogo 2) | Port (`port/MikisWorld.Core/`) |
|---|---|
| `c` (jogo) | `Game2*.cs` (`Level`, `Update`, `Player`, `Bonus`, `Draw`, `Debug`) |
| `e` (colisão, com wrap) | `TileCollision2.cs` |
| `d` (cutscenes) | `CutscenePlayer2.cs` |
| `i` (tiles com cache em anel) | `TileLayer.cs` (dados, `LoadLayerV2`) + `TileCache2.cs` (desenho) |
| `f` (layout 128×128) | `Layout2` / `Tile2` em `Game2.cs` |
| `a`, `GameMIDlet`, `j` (menus) | o mesmo `App.cs` do jogo 1, via `IMikisGame` + `AppInfo` |
| `k`, `b`, `g`, `h` | `HallOfFame.cs`, `RingTone.cs`/`Sfx.cs`, vibração ignorada |

Reaproveitados sem mudança: `JavaRandom`, `DataReader`, `HallOfFame`, `SaveData`, `RingTone`, `Canvas`/`Bitmap`, o minigame (copiado de `Game.Bonus.cs`,
com bytecode idêntico) e o shell de menus.

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
(dano), volta ao estado 0 e espera 7 s; `R2` também arma `bw = 4000`. Nasce em `(F, D + 5 tiles)` no estágio final. Validado pelos traces.

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
vibração, hi-score, instruções, sobre) são as mesmas do jogo 1, e o jogo mostra `splash.png` antes do título (preto 1 s, imagem 2 s, preto 1 s).
O `App` do port tem o menu equivalente (Esc/Enter) e implementa o splash (`AppInfo.HasSplash`), mas ele está **desligado por padrão**
(decisão do autor do port). O título toca 3 vezes (`Sound.play(3)`), como no original.

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

## Execução do original (harness)

O jar 2 **roda headless** no mesmo harness do jogo 1 (`tools/harness`), usando um arquivo de nomes
(`tools/harness/names_mw2.properties`) que traduz os campos lógicos para os nomes ofuscados do jogo 2, e os shims da Nokia
copiados do jar 1 (o jogo 2 não os traz). As constantes impressas pelo original **confirmam a leitura**: tiles 12×12,
jogador 15×20, `cy = cx = 24`, velocidades `21`/`43`, queda `48`, subida dos arco-íris `57`, 76 moedas, Lords nas plataformas
40 e 6, jogador em `(40, 780)` e o Mork nascendo 10 tiles abaixo (`aW = 20640`).

`tools/record_traces2.sh` grava 20 cenários (principal, subáreas, estágio final, minigame e "die" sem vidas extras). Cobrem
os estados 0, 3, 4, 5, 6, 10, 11, 12 e 20, os quatro movimentos do Mork e até 9 arco-íris no estágio final. Como o Mork
mata um jogador aleatório em ~1500 ticks, a maioria roda com `-Dkeeplives=3` (repõe as vidas antes de cada tick; o
runner em C# precisa fazer o mesmo). Como no jogo 1, as subáreas 3 e 4 estouram o mesmo `ArrayIndexOutOfBounds` ao usar um
portal que não tem destino (bug do original reproduzido nos traces).

## Pendências e bugs do original

- **Bug do original (não reproduzido):** iniciar o minigame antes de carregar o nível principal trava a pintura — as imagens de tiles (`bK`) e o
  tamanho do tile (`b2`) só existem depois do primeiro `e()`, e o desenho do minigame faz `for (x = 0; x < largura; x += b2)` com `b2 = 0` (laço infinito) /
  usa `bK[13]` (`NullPointerException`). Isso só aparece se o bônus já estiver liberado e for o primeiro modo escolhido. O port sempre tem as imagens.
- **Reproduzido de propósito nos traces:** as subáreas 3 e 4 estouram `ArrayIndexOutOfBounds` ao usar um portal sem destino (mesmo bug do jogo 1).
- A imagem de *snapshot* do wipe é criada com **12×12** px (`Image.createImage(12, 12)`), então o wipe mostra um quadradinho deslizando;
  é fiel ao original e está nos quadros de referência.
- **Cobertura:** os inputs dos testes são aleatórios com teleportes e vidas repostas (o Mork mata um jogador aleatório em ~1500 ticks); partidas
  longas jogadas à mão ainda podem revelar casos não cobertos. O texto (fonte do aparelho) fica fora da comparação de pixels.
