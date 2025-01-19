# rsvp-server

Maple server.

## firewalld config

Import through
```
firewall-cmd --permanent --new-service-from-file=maplestory.xml --name=maplestory
```