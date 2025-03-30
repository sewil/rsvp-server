# rsvp-server

This repository contains the sourcecode of the RSVP server.

*Requirements*:

- Windows (for this guide, you can compile it to another OS using dotnet SDK!)
- Visual Studio 2022 or higher with .net 9.0 SDK installed
- Up-to-date client (see Discord for latest WzMss.dll)

## Prepared installation
You can find a download to the server installation in the [Discord #announcements chat](https://discord.gg/2P8w9KSuD3).

## Manual Setup

Check out all 3 repos in a single folder. Make sure rspv-scripts-2 is called rsvp-scripts.

```sh
mkdir rsvp
cd rsvp
git clone https://github.com/diamondo25/rsvp-scripts-2.git rsvp-scripts
git clone https://github.com/diamondo25/rsvp-server.git
git clone https://github.com/diamondo25/rsvp-data.git
```


### Building servers
1. Open rsvp-server `WvsBeta_REVAMP.sln`
2. Build the solution (all projects) through Build -> Build Solution
3. All binaries are now available under the rsvp-server/BinSvr folder

### Setup data links
The server will use the same datafiles as the client.

To link the datafiles, go to the rsvp-server/DataSvr folder and run `link.bat`.

### Using the launcher to manage the server
The launcher (WvsBeta.Launcher) was developed to make setting up the server easy. It is recommended to run this if you do LAN play. For production servers, I would recommend investing time in running the binaries standalone and with a dedicated Redis and MariaDB installation.

#### Opening the launcher
1. Go to rsvp-server/BinSvr
2. Launch `WvsBeta.Launcher.exe`

#### Configure PublicIP
The PublicIP field is given to the client when they change channel or connect to channel or shop. It is therefor required that it is configured correctly. You can automatically configure this by using the Launcher "LAN mode" option.

1. Open the Launcher
2. Go to Configure LAN Mode
3. Select the interface of which IP you want to use. It is usually called something like "Ethernet", and has an IP starting with 192.168, 10.0, or 172).
4. Apply settings

#### Starting the server
1. Open the Launcher
2. (Only for initial setup) Click the 'Reinstall' button for M
MariaDB
3. Make sure MariaDB and Redis are started
4. Start Login, Center, Game, and Shop. *This is not possible if the launcher did not detect a running MariaDB and Redis instance.*

#### Add accounts
1. In the Launcher, go to the User Manager
2. Press Add
3. Fill in the information (except ID column). Note: GM level is max 3 (admin).
4. The information should be saved when switching between each column.


### Putting it public
1. Port forward to your pc.
2. Use canyouseeme.org to test port forwards
3. Use the launcher or manually change the PublicIP fields in the configuration files.

### Starting the client
Install RSVP v24. It should automatically connect to localhost.