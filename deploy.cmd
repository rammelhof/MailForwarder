@echo off
robocopy "%~dp0publish" "\\server.lan\Share\Apps\MailForwarder\publish" /MIR /R:3 /W:1 /XD Logs
robocopy "%~dp0\" "\\server.lan\Share\Apps\MailForwarder" "*.cmd"