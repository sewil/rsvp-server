# Install rsvp on linux

## Requirements (tested environment)
- Ubuntu Server 24.04.2 like https://ubuntu.com/download/server/thank-you?version=24.04.2&architecture=amd64&lts=true

### Install dotnet
As explained here: https://learn.microsoft.com/nl-nl/dotnet/core/install/linux-scripted-manual#scripted-install

```
cd ~
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x ./dotnet-install.sh
./dotnet-install.sh --channel 9.0
```


### Checkout sources
```
mkdir rsvp
cd rsvp
git clone https://github.com/diamondo25/rsvp-scripts-2.git rsvp-scripts
git clone https://github.com/diamondo25/rsvp-server.git
git clone https://github.com/diamondo25/rsvp-data.git
```

#### Link sources
```
cd rsvp-server/DataSvr
chmod +x link.sh
./link.sh

```


### Install redis
```
sudo apt-get install -y redis-server
```

#### Configure redis
You should figure out how to set a password yourself, and then set it in the config files.

### Install mariadb
```
sudo apt-get isntall -y mariadb-server
```

#### Prepare database
```
sudo mariadb-admin create rsvp

# yea idk figure out how to
# - create the user rsvp for localhost
# - import the schema and then evolutions from WvsBeta.Database\evolutions\prepared\
```

### Build Server
```
cd rsvp-server


export DOTNET_ROOT=${HOME}/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools

dotnet build WvsBeta_REVAMP-linux.slnf
```
