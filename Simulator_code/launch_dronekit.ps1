$wslDistro = "Ubuntu"
$remoteCmd = 'cd /home/antosilver && ./setup_commands.sh; exec bash'

wsl.exe -d $wslDistro -- bash -ic "$remoteCmd"