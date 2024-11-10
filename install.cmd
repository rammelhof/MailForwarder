sc.exe create "MailForwarder" binpath="%~dp0publish\MailForwarder.Service.exe" start=delayed-auto
sc.exe failure "MailForwarder" reset=0 actions=restart/60000/restart/60000/run/1000
sc.exe start "MailForwarder"