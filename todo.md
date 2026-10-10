# Bugs

1. ~~Terminar o jogo não exibe o high score mais.~~ (feito: a lista aparece sempre ao fim da partida, sem destaque se a pontuação não entrou; aguardando teste)
2. ~~O fullscreen do jogo tem prioridade e não deixa outras coisas entrarem por cima quando aberto.~~ (feito: o topmost do raylib é desfeito ao ligar o fullscreen; aguardando teste)
3. ~~O scroll do mouse só funciona no menu dos jogos e não no do seletor de jogo.~~ (feito: a roda move a seleção no seletor)
4. ~~Gaiolas de pássaros e moedas não possuem hitbox visível no modo hitboxes on.~~ (feito: moedas, chaves e gaiolas/arco-íris têm caixa amarelo-clara)
5. ~~A opção logs não faz nada no menu de debug.~~ (feito: o console agora é anexado/aberto mesmo quando o programa herda um handle de console, como no play.bat; ao ligar imprime "logs on"; aguardando teste)
6. ~~Não existe hoje uma opção nas telas de rebind de "Voltar os controles para o padrão"~~ (feito: a linha já existia mas se chamava "Defaults" e era discreta; agora é "Reset to defaults", em rosa, com aviso "Defaults restored"; a dica não corta mais no jogo 2)

# Coisas a serem adicionadas de imediato

## Post-processing
1. Adicionar outros filtros fora bilinear e nearest, tipo xBRZ, ScaleFX, e se possível, DTP e CRT-Royale
2. Permitir filtros misturado com integer scaling
3. Ao mudar o filtro, caso estiver dentro de uma fase, manter o jogo pausado mas esconder o menu e exibir apenas a opção que está sendo alterada do filtro no canto superior direito, com setinhas pra esquerda e pra direita, ou seja, ainda podendo modificar o filtro enquanto consegue enxergar o resultado em tempo real.
4. Adicionar um toggle para um efeito de ghosting, pois o jogo original no celular nokia possuia um artefato de ghosting (talvez pela tela ser LCD? não sei)

## Funcionalidades

1. Adicionar opção no modo debug pra liberar o modo Bônus
2. No bônus, seria bom ter o mouse funcionando pra clicar na onde eu quiser colocar uma moeda/token.
3. Eu removi o texto indicando que a configuração é salva automaticamente. Ao fechar as configurações, caso alguma config tenha sido alterada, exibir um diálogo no canto inferior direito escrito "Saved." Na tela de seleção de jogos não precisa pois não faria muito sentido.
4. Não dá pra rebindar os botões na tela inicial.

# Suporte aos mods

Para depois das correções acima...
