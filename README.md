# DoorBerry
Use a Raspberry Pi as a VoIP door intercom system.

I use it with my FritzBox to alert all my DECT Phones.
Tested on a Pi 2B+ and a Pi Zero with raspbian (and mono).

## Software
Makes use of [Asterisk](https://www.asterisk.org/) and [raspberry sharp](https://github.com/raspberry-sharp/).
Now why would anyone use C# for his door bell? Because I didn't know C# and wanted to practise :stuck_out_tongue_winking_eye:

### C# - Handle the Door

1. Adjust the hardcoded stuff in Asterisk.cs to match you config :sweat_smile:
2. Make all the DLLs and the EXE
```sh
cd Raspberry.IO/
mcs -t:library *.cs -r:System -out:Raspberry.IO.dll

cd Raspberry.IO.Interop/
mcs -t:library *.cs */*.cs -r:System -out:Raspberry.IO.Interop.dll

cd Raspberry.System/
mcs -t:library *.cs */*.cs -r:System -out:Raspberry.System.dll

cd Raspberry.IO.GeneralPurpose/
mcs -t:library *.cs */*.cs -r:System,System.Configuration,Raspberry.IO.Interop,Raspberry.IO,Raspberry.System -lib:../Raspberry.IO.Interop/,../Raspberry.IO/,../Raspberry.System/ -out:Raspberry.IO.GeneralPurpose.dll

mcs /t:exe Main.cs Asterisk.cs Notifier.cs /r:System,Raspberry.IO.GeneralPurpose,Raspberry.IO.Interop,Raspberry.System,Raspberry.IO
```
3. Throw everything on the Pi.
4. There install mono:
```sh
sudo apt-get install libmono-corlib4.5-cil, libmono-i18n-west4.0-cil, libmono-i18n4.0-cil, libmono-security4.0-cil, libmono-system-configuration4.0-cil, libmono-system-security4.0-cil, libmono-system-xml4.0-cil, libmono-system4.0-cil, mono-4.0-gac, mono-gac, mono-runtime, mono-runtime-common, mono-runtime-sgen
```
5. Execute it (best at each startup)
```sh
#!/bin/sh
### BEGIN INIT INFO
# Provides:             door
# Required-Start:       $start
# Required-Stop:        $shutdown
# Default-Start:        2 3 4 5
# Default-Stop:
# Short-Description:    Doorberry project
### END INIT INFO

mono /etc/door/Main.exe > /etc/door/tuer.log &

exit 0
```

The Programm will try to connect to a Asterisk manager. It will use it to signal the Door and will wait for a DTMF (button "5") in order to open the Door.

### Asterisk - Handle VoIP

Last time I checked it ran with: Asterisk 11.13.1
1. Change username and password for your VoIP registrar in sip.conf
2. Change username and password for your manager in username.conf
3. Remember the hardcoded stuff in Asterisk.cs :sweat_smile:
4. Turn off oss and use alsa in modules.conf
5. Adjust alsa.conf to match your (sound) setup
6. Turn on manager for localhost in manager.conf

### More Scripting

If you want to use a script (additionaly to VoIP) to open the door use Port 14000:
```sh
echo -n "AUF" | nc -q 1 localhost 14000
```
Also clients connected to port 11000 will receive a newline when the bell is detected.

## Hardware

### Hat
Door Bell, Buzzer and Voice is handled by a little Hat I made for the Pi. Please find it [here](https://github.com/User65k/DoorBerryHat).

### USB Soundcard
The Pi does not have a microphone on its own.