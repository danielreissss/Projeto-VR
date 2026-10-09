# Rodando o cenário no servidor Linux (rede 5G do DCC)

Topologia:

```
Notebook (rede DCC) --ssh/scp--> Servidor Linux 172.30.22.31 --cabo--> Roteador 5G --wifi/5G--> Meta Quest
```

O servidor roda o cenário `ConsultorioFinal` como **servidor dedicado** (sem tela, sem avatar),
na porta **UDP 7777**. Os óculos entram como **clientes** apontando para o IP do servidor.
O relay do chat de voz já é feito pelo servidor.

## 1. Gerar os builds (no notebook, com Unity 6000.0.39f1)

Módulos necessários no Unity Hub: **Android Build Support** e **Linux Dedicated Server Build Support**.

No editor, menu **Build**:

- `Build > Servidor Linux (Dedicated Server)` → `Builds/LinuxServer/`
- `Build > APK Meta Quest (Android)` → `Builds/Android/ConsultorioVR.apk`

Ou pela linha de comando:

```
Unity -batchmode -quit -projectPath . -executeMethod ServerBuild.BuildLinuxServer -logFile -
Unity -batchmode -quit -projectPath . -executeMethod ServerBuild.BuildAndroidApk -logFile -
```

O APK já vem configurado para conectar em `172.30.22.31` (campo `hostIpAddress` do
`LanDirectConnect` na cena). Se o IP for outro, veja a seção 4.

## 2. Subir o servidor

```
scp -r Builds/LinuxServer admin@172.30.22.31:~/consultorio
ssh admin@172.30.22.31
cd ~/consultorio
chmod +x ConsultorioServer.x86_64
./ConsultorioServer.x86_64 -logFile - -port 7777
```

Deve aparecer `--- LAN: Iniciando como SERVIDOR DEDICADO na porta 7777 ---`.
Para deixar rodando depois de fechar o ssh: `nohup ./ConsultorioServer.x86_64 -logFile server.log &`
(ou use `tmux`/`screen`).

Libere a porta no firewall, se houver:

```
sudo ufw allow 7777/udp        # ou: sudo firewall-cmd --add-port=7777/udp
```

Confira se está escutando: `ss -ulpn | grep 7777`.

## 3. Instalar o APK nos óculos

Com o modo desenvolvedor ativo e o Quest no USB do notebook:

```
adb install -r Builds/Android/ConsultorioVR.apk
```

(ou arraste o APK no Meta Quest Developer Hub / SideQuest). Conecte o Quest à rede do
roteador 5G e abra o app em **Biblioteca > Fontes desconhecidas**. Ele conecta sozinho.

## 4. Trocar o IP sem refazer o build

Crie um arquivo `server_ip.txt` com o IP e envie para a pasta do app:

```
echo 10.0.0.5 > server_ip.txt
adb push server_ip.txt /sdcard/Android/data/com.Danie.ConsultorioVR.DriveE/files/server_ip.txt
```

Ordem de prioridade do IP: argumento `-ip`, depois `server_ip.txt`, depois o valor da cena.

Argumentos aceitos pelo executável: `-server`, `-host`, `-client`, `-ip <endereço>`, `-port <porta>`.

## Verificações se não conectar

- Os óculos precisam alcançar o servidor: o IP `172.30.22.31` é o da rede DCC; pela rede 5G o
  servidor pode ter **outro IP**. Pergunte ao responsável qual IP o roteador 5G enxerga e
  use a seção 4.
- Porta UDP 7777 liberada no servidor e no roteador.
- Log do Quest: `adb logcat -s Unity`.
