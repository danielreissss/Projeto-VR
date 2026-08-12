# Projeto VR Multiplayer — Consultório VR

Projeto Unity de realidade virtual para Meta Quest, com três avatares em um ambiente multiplayer e comunicação de voz em tempo real.

## Estado atual

Esta é a versão unificada dos projetos base, Projeto 1 e Projeto 2. A versão final reúne:

- cenário principal do consultório;
- área externa e recursos ambientais adicionais;
- avatares Meta/Oculus e avatares importados dos projetos auxiliares;
- sincronização de jogadores usando Netcode for GameObjects;
- conexão Host/Client por transporte Unity;
- chat de voz por microfone, relay do servidor e reprodução nos avatares remotos;
- diagnóstico de microfone, permissões Android, rede e spawn dos avatares;
- cenas de teste de conexão, matchmaking, spawn e voz.

O projeto base foi mantido como referência para pacotes e configurações XR. Os assets exclusivos dos outros projetos foram adicionados sem duplicar GUIDs do Unity.

## Requisitos

- Unity `6000.0.39f1`;
- módulo Android/Android SDK e suporte ao Meta Quest;
- Meta Quest 3 ou outro dispositivo Android XR compatível;
- Oculus/Meta XR SDK `85.0.0`;
- Netcode for GameObjects `1.14.1`;
- Unity Transport e Input System configurados pelo projeto;
- dois ou mais dispositivos para testar o multiplayer;
- rede local acessível pelos dispositivos quando o teste for feito por LAN.

Use a mesma versão do Unity indicada em `ProjectSettings/ProjectVersion.txt`. Versões diferentes podem atualizar arquivos de configuração e causar incompatibilidades nos pacotes XR.

## Pacotes principais

Os pacotes estão definidos em `Packages/manifest.json` e incluem:

- Meta XR Interaction SDK e Movement;
- XR Management e OpenXR;
- Input System;
- Netcode for GameObjects;
- Unity Transport;
- Multiplayer Tools e Multiplayer Services;
- Universal Render Pipeline;
- AI Navigation;
- Timeline e Visual Scripting.

O arquivo `Packages/packages-lock.json` registra as versões resolvidas. Não substitua os arquivos `Packages` de outro projeto sem comparar as versões, porque os projetos auxiliares usam combinações diferentes de SDK, URP e XR.

## Estrutura do projeto

```text
Assets/
├── Scenes/              Cenas principais e cenas de teste
├── VoiceChat/           VoiceSender, VoiceReceiver, VoiceManager e diagnóstico
├── Scripts/             Conexão, spawn, controle de rede e acompanhamento do rig
├── Avatares/            Avatares e materiais importados
├── AvatarNina/          Avatar adicional
├── Avatars/             Avatares Meta/Oculus e avatares adicionais
├── MetaXR/              Recursos do Meta XR
├── Oculus/              Configurações e recursos Oculus
├── Prefabs/             Prefabs reutilizáveis
├── Consultorio/         Recursos do cenário principal
├── AssetsExternos/      Cenários, materiais e modelos externos
└── Settings/            Configurações gráficas e URP
Packages/                Dependências do Unity
ProjectSettings/        XR, Android, input, cenas e configurações do projeto
```

As pastas geradas pelo Unity, como `Library`, `Temp`, `Logs`, `Obj`, `.utmp`, `Build` e `UserSettings`, não fazem parte do código-fonte versionado.

## Cenas

| Cena | Uso | Build Settings |
|---|---|---:|
| `Assets/Scenes/ConsultorioFinal.unity` | Cena principal integrada, com multiplayer, avatares e chat de voz | Ativa |
| `Assets/Scenes/Consultorio-v0.3.unity` | Versão anterior do consultório | Desativada |
| `Assets/Scenes/testeConectaIpAutomatico.unity` | Teste de conexão automática por IP | Desativada |
| `Assets/Scenes/testeMeta.unity` | Testes de integração Meta XR | Desativada |
| `Assets/Scenes/testeMeuMatchmaking.unity` | Testes de matchmaking | Desativada |
| `Assets/Scenes/testeSpawner.unity` | Testes de spawn e sincronização | Desativada |
| `Assets/Scenes/SampleScene.unity` | Cena padrão/teste do Unity | Desativada |

Para executar a experiência principal, abra `ConsultorioFinal.unity` e pressione Play no Editor, ou gere o build Android/Quest com essa cena habilitada.

## Chat de voz

O sistema de voz está em `Assets/VoiceChat`:

- `VoiceSender.cs`: solicita a permissão de microfone, captura o áudio e envia pacotes;
- `VoiceManager.cs`: registra a mensagem `VoicePacket`, faz o relay pelo servidor e associa voz aos avatares;
- `VoiceReceiver.cs`: recebe, decodifica e reproduz o áudio de cada jogador remoto;
- `VoiceDebugVR.cs`: mostra informações de microfone, rede, ownership, spawn e volume.

O fluxo de comunicação é:

```text
Microfone do jogador
        ↓
VoiceSender
        ↓  VoicePacket / Netcode
Host ou servidor faz o relay
        ↓
VoiceManager
        ↓
VoiceReceiver no avatar remoto
        ↓
Áudio espacializado no ambiente VR
```

No Quest, a permissão de microfone deve ser concedida no dispositivo. O sistema aguarda a permissão e a disponibilidade do dispositivo antes de iniciar a captura.

## Multiplayer

O projeto usa `NetworkManager` com Netcode for GameObjects. Há scripts para diferentes cenários de teste:

- `ConnectionManager.cs`: inicia Server, Host ou Client;
- `ManualConnection.cs`: conexão manual e logs de conexão;
- `LanDirectConnect.cs`: conexão direta por IP em rede local;
- `UniversityLANManager.cs`: interface simples para iniciar Host/Client em LAN;
- `TsConnectionManager.cs`: conexão com aprovação e dados de conexão;
- `PlayerNetworkScript.cs`: exemplos de sincronização e RPCs;
- `PlayerRigFollower.cs`: acompanha o rig `OVRCameraRig` do jogador local.

Para um teste básico:

1. Abra `ConsultorioFinal.unity`.
2. Execute um dispositivo como Host.
3. Execute o segundo dispositivo como Client, usando o IP do Host quando a cena solicitar.
4. Confirme o spawn dos avatares.
5. Fale próximo ao microfone e verifique o áudio no outro dispositivo.

Todos os dispositivos devem usar o mesmo build, a mesma cena e uma configuração compatível de rede.

## Build para Meta Quest

No Unity:

1. Abra `File > Build Profiles` ou `Build Settings`.
2. Selecione Android.
3. Confirme que `ConsultorioFinal.unity` está na lista e habilitada.
4. Verifique XR Plug-in Management/OpenXR e as configurações do Meta XR.
5. Confirme a permissão de microfone no Player Settings/manifest Android.
6. Gere e instale o APK no Quest.

Antes do build final, verifique o Console, o dispositivo de áudio selecionado e a conectividade entre os Quest.

## Integração e versionamento

Os arquivos `.meta` devem ser mantidos junto com seus assets. Eles preservam os GUIDs usados por cenas, prefabs e materiais.

Não versionar ou copiar para o repositório:

- `Library/`;
- `Temp/`;
- `Logs/`;
- `Obj/`;
- `.utmp/`;
- `UserSettings/`;
- builds gerados;
- a pasta local `Outros projetos/`, usada somente como fonte da unificação.

Para conferir as alterações antes de um commit:

```powershell
git status
git diff --check
git diff --stat
```

Para registrar uma versão validada:

```powershell
git add Assets Packages ProjectSettings README.md .gitignore
git commit -m "Atualiza documentação da versão final VR multiplayer"
git push origin daniel
```

## Validação pendente

A estrutura foi verificada sem GUIDs duplicados entre os assets integrados. A confirmação final de compilação e execução deve ser feita no Unity Editor com a licença ativa, abrindo a cena principal e testando Host, Client, spawn dos três avatares e chat de voz em dispositivos reais.

