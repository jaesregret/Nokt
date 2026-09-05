# Nokt

Small programming language built from scratch with .NET 8.

## Funcionalidades

- `say` para imprimir expressões.
- `let` para declarar ou atualizar variáveis.
- Atribuição de variáveis existentes, como `x = 20`.
- Strings, inteiros, booleanos (`true` e `false`) e referências a variáveis.
- Operadores aritméticos `+`, `-`, `*` e `/`, com precedência e parênteses.
- Concatenação de strings usando `+`.
- Comparações `==`, `!=`, `>`, `<`, `>=` e `<=`.
- Operadores lógicos `and`, `or` e `not`.
- Controle de fluxo com `if`, `else` e `while`, usando indentação de quatro espaços.
- Comentários iniciados por `#`.
- Erros de léxico, sintaxe e execução com linha, coluna e token/valor quando disponíveis.
- xUI inicial para janelas Windows Forms com `window`, `size`, `text`, `input` e `button`.
- Blocos de botão executados quando o usuário clica no botão.

## Uso

```text
dotnet run --project src/nk/Nokt.csproj -- Examples/features.nk
```

Para executar o exemplo gráfico no Windows:

```text
dotnet run --project src/nk/Nokt.csproj -- Examples/ui.nk
```

Exemplos legados: `Examples/hello.nk` e `Examples/vars.nk`.
