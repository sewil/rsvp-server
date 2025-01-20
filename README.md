# Preparation

## Codebase

Check out all 3 repos in a single folder. Make sure rspv-scripts-2 is called rsvp-scripts.

```sh
mkdir rsvp
cd rsvp
git clone https://github.com/diamondo25/rsvp-scripts-2.git rsvp-scripts
git clone https://github.com/diamondo25/rsvp-server.git
git clone https://github.com/diamondo25/rsvp-data.git
```

## Database
1. Install MariaDB on your local machine and write down the root password
2. Install a SQL editor like SQLyog Community Edition
3. Open the SQL editor and connect to your MariaDB.
4. Create a schema/database called "rsvp"
5. Load the two SQL files from the rsvp-server/SQLs folder, first the schema, then evolutions.
6. Create an account for the servers to connect with:
```sql
CREATE USER 'rsvp'@'localhost' IDENTIFIED BY 'mypassword'; 
FLUSH PRIVILEGES; 
GRANT ALTER, ALTER ROUTINE, CREATE, CREATE ROUTINE, CREATE TEMPORARY TABLES, CREATE VIEW, DELETE, DROP, EVENT, EXECUTE, INDEX, INSERT, LOCK TABLES, REFERENCES, SELECT, SHOW VIEW, TRIGGER, UPDATE ON `rsvp`.* TO 'rsvp'@'localhost' WITH GRANT OPTION; 
```

### Accounts
Add an account through SQL using the following query:
```sql
INSERT INTO `users` (`username`, `password`, `email`) VALUES ('Diamondo25', 'yomama', '');
```

## Redis
1. Download [Redis for Windows](https://github.com/tporadowski/redis/releases)
2. (optional) configure the redis password in the Redis config.
3. Launch the Redis server 

## Building servers
1. Open rsvp-server WvsBeta_REVAMP.sln
2. (!!!) Change the authentication password for your server: open WvsBeta.Common/Constants.cs and edit the 'AUTH_KEY' value.
3. Build the solution (all projects) through Build -> Build Solution

## Configuring servers
1. Open rsvp-server/DataSvr/Database.img in a text editor
2. Update the login information to the database

### Updating Redis password
For each server, update the redis password in its config file.

```
redis = {
	password = redis-password
}
```

## Data linking
Run "link.bat" in DataSvr to link the rsvp-data git repo folder, and rsvp-scripts git repo folder.

## firewalld config (linux only)

Import through
```
firewall-cmd --permanent --new-service-from-file=maplestory.xml --name=maplestory
```

# Running the server

Make sure that:
- MariaDB is running
- Redis is running
- DataSvr config files are OK
- Datafiles are linked

Now you can launch the servers through their bat scripts (Launch X.bat).

## Putting it public
1. Port forward to your pc.
2. Use canyouseeme.org to test port forwards
3. Edit the config files 'PublicIP' field to your WAN IP.

# Starting the client
Install RSVP v22. Create a shortcut to the exe with additional arguments like `Maple.exe 127.0.0.1 8484`