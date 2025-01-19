------- UP -------

ALTER TABLE users 
CHANGE COLUMN `ban_expire` `ban_expire` datetime NOT NULL DEFAULT '2000-01-01 00:00:00',
CHANGE COLUMN `last_login` `last_login` datetime NOT NULL DEFAULT '2000-01-01 00:00:00',
CHANGE COLUMN `quiet_ban_expire` `quiet_ban_expire` datetime NOT NULL DEFAULT '2000-01-01 00:00:00',
CHANGE COLUMN `creation_date` `creation_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP;


------- DOWN -------

ALTER TABLE users 
CHANGE COLUMN `ban_expire` `ban_expire` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
CHANGE COLUMN `last_login` `last_login` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
CHANGE COLUMN `quiet_ban_expire` `quiet_ban_expire` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
CHANGE COLUMN `creation_date` `creation_date` datetime NOT NULL DEFAULT '0000-00-00 00:00:00';
