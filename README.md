# Projeto de Iniciação Científica - Unity + VR 

Projeto desenvolvido em Unity para exploração de ambientes virtuais, integrando chat de voz e rastreamento de movimento em rede local (LAN).

**Instituição:** Universidade Federal de Lavras (UFLA)
**Equipe:** Daniel Reis, Nina, Thales e Gabriel

---

## ⚙️ Como baixar e rodar o projeto
1. Clone este repositório no seu computador ou no laboratório.
2. Abra o Unity Hub e adicione o projeto a partir do disco.
3. **Aviso Importante:** A pasta `Library` está ignorada no repositório para economizar espaço. Ao abrir o projeto pela primeira vez, o Unity recriará essa pasta automaticamente (pode demorar alguns minutos).

---

## 🖥️ Uso no Computador do Laboratório
O computador do laboratório já possui o **GitHub Desktop** (interface visual) e o **Git Bash** (terminal) instalados. Escolha a ferramenta que preferir para clonar o projeto e fazer os commits:

### Opção 1: Pelo GitHub Desktop (Interface Visual)
1. Abra o GitHub Desktop e certifique-se de estar logado na sua conta.
2. Vá em **File > Clone repository**, selecione a aba *URL* e cole: `https://github.com/danielreissss/Projeto-VR.git`.
3. No menu superior, mude a *Current Branch* de `main` para a sua branch (`daniel`, `thales` ou `gabriel`) **antes** de abrir o Unity.
4. Quando terminar de trabalhar, escreva o resumo das alterações no canto inferior esquerdo, clique em **Commit** e, em seguida, em **Push origin** no topo da tela.

### Opção 2: Pelo Git Bash (Terminal)
1. Abra o Git Bash na pasta onde deseja guardar o projeto e clone o repositório:
   `git clone https://github.com/danielreissss/Projeto-VR.git`
2. Entre na pasta do projeto:
   `cd Projeto-VR`
3. Mude para a sua branch isolada:
   `git checkout [seu-nome]`
4. Para salvar e enviar o progresso do dia para a nuvem, rode:
   * `git add .`
   * `git commit -m "Resumo do que foi feito"`
   * `git push origin [seu-nome]`
     
##  Fluxo de Trabalho (Branches):
Nunca trabalhe diretamente na branch `main`. Utilize a branch com o seu nome para desenvolver e testar suas alterações de forma isolada:
* `main` (Estável - Apenas para testes finais no Quest 3 e laboratório)
* `daniel` (Desenvolvimento)
* `thales` (Desenvolvimento)
* `gabriel` (Desenvolvimento)

Para mudar para a sua branch antes de programar, rode no terminal:
`git checkout [seu-nome]`

---

## Configuração de Rede para Testes LAN:
Para que o chat de voz e o rastreamento funcionem entre o PC e o Quest 3:
1. Certifique-se de que o PC (Host) e o Quest 3 (Client) estão na mesma rede Wi-Fi.
2. Adicione uma exceção no Firewall do Windows para o Unity Editor (Rede Privada e Pública) ou desative temporariamente o Firewall de Rede Privada.
